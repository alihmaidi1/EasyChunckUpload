using EasyChunkUpload.Abstractions;
using System.Diagnostics;
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

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var now = timeProvider.GetUtcNow();
            var recovered = await coordinator.RecoverExpiredCompletionLeasesAsync(now, cancellationToken);
            UploadMetrics.CompletionLeasesRecovered.Add(recovered);
            if (recovered > 0)
            {
                logger.LogWarning("Recovered {LeaseCount} expired completion leases.", recovered);
            }

            var candidates = await store.GetMaintenanceCandidatesAsync(now, batchSize, cancellationToken);
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var owner = Guid.NewGuid().ToString("N");
                var candidateNow = timeProvider.GetUtcNow();
                var acquired = await coordinator.TryAcquireAsync(
                    candidate.UploadId,
                    UploadLeasePurpose.Cleanup,
                    owner,
                    candidateNow,
                    options.Value.CleanupLeaseDuration,
                    cancellationToken);
                if (!acquired)
                {
                    continue;
                }

                try
                {
                    var cleanupInterrupted = false;
                    var heartbeat = new UploadLeaseHeartbeat(
                        coordinator,
                        timeProvider,
                        candidate.UploadId,
                        UploadLeasePurpose.Cleanup,
                        owner,
                        options.Value.LeaseRenewalInterval,
                        options.Value.CleanupLeaseDuration,
                        cancellationToken);
                    try
                    {
                        await storage.DeleteUploadArtifactsAsync(
                            candidate.UploadId,
                            includeCompletedFile: false,
                            heartbeat.OperationToken);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        cleanupInterrupted = true;
                    }
                    finally
                    {
                        await heartbeat.StopAsync();
                    }

                    heartbeat.ThrowIfFailed();
                    if (heartbeat.LeaseLost)
                    {
                        logger.LogWarning("Cleanup lease was lost for upload {UploadId}.", candidate.UploadId);
                        continue;
                    }

                    if (cleanupInterrupted)
                    {
                        throw new OperationCanceledException("Upload cleanup was interrupted.");
                    }

                    var marked = await store.TryMarkArtifactsDeletedAsync(
                        candidate.UploadId,
                        owner,
                        timeProvider.GetUtcNow(),
                        cancellationToken);
                    if (marked)
                    {
                        UploadMetrics.UploadArtifactsCleaned.Add(1);
                    }
                    else
                    {
                        logger.LogWarning("Cleanup ownership was lost for upload {UploadId}.", candidate.UploadId);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await coordinator.ReleaseAsync(
                        candidate.UploadId,
                        owner,
                        UploadState.Cancelled,
                        timeProvider.GetUtcNow(),
                        CancellationToken.None);
                    throw;
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

            var deletedBefore = timeProvider.GetUtcNow().Subtract(options.Value.ExpiredSessionMetadataRetention);
            var purged = await store.DeleteExpiredMetadataAsync(deletedBefore, batchSize, cancellationToken);
            UploadMetrics.UploadMetadataPurged.Add(purged);
        }
        finally
        {
            stopwatch.Stop();
            UploadMetrics.MaintenanceDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
