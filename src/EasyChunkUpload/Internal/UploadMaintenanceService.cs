using EasyChunkUpload.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyChunkUpload.Internal;

internal sealed class UploadMaintenanceService(
    IUploadSessionStore store,
    IUploadCompletionCoordinator coordinator,
    IChunkStorage storage,
    IOptions<UploadOptions> options,
    TimeProvider timeProvider,
    ILogger<UploadMaintenanceService> logger) : IUploadMaintenanceService
{
    public async Task RunOnceAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var now = timeProvider.GetUtcNow();
        var recovered = await coordinator.RecoverExpiredCompletionLeasesAsync(now, cancellationToken);
        if (recovered > 0)
        {
            logger.LogWarning("Recovered {LeaseCount} expired completion leases.", recovered);
        }

        var candidates = await store.GetMaintenanceCandidatesAsync(now, batchSize, cancellationToken);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var owner = Guid.NewGuid().ToString("N");
            var acquired = await coordinator.TryAcquireAsync(
                candidate.UploadId,
                UploadLeasePurpose.Cleanup,
                owner,
                now,
                options.Value.CompletionLeaseDuration,
                cancellationToken);
            if (!acquired)
            {
                continue;
            }

            try
            {
                await storage.DeleteUploadArtifactsAsync(candidate.UploadId, includeCompletedFile: false, cancellationToken);
                await store.MarkArtifactsDeletedAsync(candidate.UploadId, timeProvider.GetUtcNow(), cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to clean upload {UploadId}.", candidate.UploadId);
                await coordinator.ReleaseAsync(
                    candidate.UploadId,
                    owner,
                    UploadState.Cancelled,
                    timeProvider.GetUtcNow(),
                    CancellationToken.None);
            }
        }
    }
}
