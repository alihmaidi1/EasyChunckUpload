using Microsoft.Extensions.DependencyInjection;

namespace EasyChunkUpload.Abstractions;

public interface IChunkUploadService
{
    Task<UploadResult<UploadSessionDescriptor>> StartAsync(StartUploadRequest request, CancellationToken cancellationToken = default);

    Task<UploadResult<ChunkReceipt>> UploadChunkAsync(
        Guid uploadId,
        int chunkIndex,
        Stream content,
        long contentLength,
        string sha256,
        CancellationToken cancellationToken = default);

    Task<UploadResult<UploadStatusDescriptor>> GetStatusAsync(Guid uploadId, CancellationToken cancellationToken = default);

    Task<UploadResult<IReadOnlyList<int>>> GetMissingChunksAsync(Guid uploadId, CancellationToken cancellationToken = default);

    Task<UploadResult<UploadedFileDescriptor>> CompleteAsync(Guid uploadId, CancellationToken cancellationToken = default);

    Task<UploadResult> CancelAsync(Guid uploadId, CancellationToken cancellationToken = default);
}

public interface IChunkStorage
{
    Task<ChunkStorageWriteResult> WriteChunkAsync(
        Guid uploadId,
        int chunkIndex,
        Stream content,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken);

    Task DeleteChunkAsync(Guid uploadId, int chunkIndex, CancellationToken cancellationToken);

    Task<StorageObjectDescriptor> AssembleAsync(
        UploadSessionRecord session,
        IReadOnlyList<UploadChunkRecord> chunks,
        CancellationToken cancellationToken);

    Task DeleteCompletedFileAsync(Guid uploadId, CancellationToken cancellationToken);

    Task DeleteUploadArtifactsAsync(Guid uploadId, bool includeCompletedFile, CancellationToken cancellationToken);
}

public interface IUploadSessionStore
{
    Task CreateAsync(UploadSessionRecord session, CancellationToken cancellationToken);

    Task<UploadSessionRecord?> GetAsync(Guid uploadId, CancellationToken cancellationToken);

    Task<UploadChunkRecord?> GetChunkAsync(Guid uploadId, int chunkIndex, CancellationToken cancellationToken);

    Task<IReadOnlyList<UploadChunkRecord>> GetChunksAsync(Guid uploadId, CancellationToken cancellationToken);

    Task<ChunkRegistrationResult> RegisterChunkAsync(
        UploadChunkRecord chunk,
        DateTimeOffset updatedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task<bool> CancelAsync(Guid uploadId, DateTimeOffset cancelledAt, CancellationToken cancellationToken);

    Task<IReadOnlyList<MaintenanceCandidate>> GetMaintenanceCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken);

    Task MarkArtifactsDeletedAsync(Guid uploadId, DateTimeOffset deletedAt, CancellationToken cancellationToken);

    async Task<bool> TryMarkArtifactsDeletedAsync(
        Guid uploadId,
        string owner,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken)
    {
        await MarkArtifactsDeletedAsync(uploadId, deletedAt, cancellationToken);
        return true;
    }

    Task<int> DeleteExpiredMetadataAsync(
        DateTimeOffset deletedBefore,
        int batchSize,
        CancellationToken cancellationToken) => Task.FromResult(0);
}

public interface IUploadCompletionCoordinator
{
    Task<bool> TryAcquireAsync(
        Guid uploadId,
        UploadLeasePurpose purpose,
        string owner,
        DateTimeOffset now,
        TimeSpan duration,
        CancellationToken cancellationToken);

    Task<bool> MarkCompletedAsync(
        Guid uploadId,
        string owner,
        UploadedFileDescriptor file,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        Guid uploadId,
        string owner,
        UploadState targetState,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task<int> RecoverExpiredCompletionLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<bool> TryRenewAsync(
        Guid uploadId,
        UploadLeasePurpose purpose,
        string owner,
        DateTimeOffset now,
        TimeSpan duration,
        CancellationToken cancellationToken) => Task.FromResult(true);
}

public interface IUploadMaintenanceService
{
    Task RunOnceAsync(int batchSize, CancellationToken cancellationToken = default);
}

public interface IEasyChunkUploadBuilder
{
    IServiceCollection Services { get; }
}
