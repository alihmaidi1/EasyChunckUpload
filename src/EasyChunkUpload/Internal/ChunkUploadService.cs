using System.Diagnostics;
using EasyChunkUpload.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyChunkUpload.Internal;

internal sealed class ChunkUploadService(
    IUploadSessionStore store,
    IUploadCompletionCoordinator coordinator,
    IChunkStorage storage,
    IOptions<UploadOptions> options,
    TimeProvider timeProvider,
    ILogger<ChunkUploadService> logger) : IChunkUploadService
{
    private readonly UploadOptions _options = options.Value;

    public async Task<UploadResult<UploadSessionDescriptor>> StartAsync(
        StartUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = UploadValidation.ValidateStart(request, _options);
        if (validationError is not null)
        {
            return UploadResult<UploadSessionDescriptor>.Failure(validationError.Code, validationError.Message);
        }

        var now = timeProvider.GetUtcNow();
        var session = new UploadSessionRecord(
            Guid.NewGuid(),
            request.FileName,
            request.ContentLength,
            request.TotalChunks,
            UploadValidation.NormalizeSha256(request.Sha256),
            UploadState.Created,
            now,
            now,
            now.Add(_options.IncompleteUploadRetention),
            null,
            null,
            null,
            null,
            null,
            0,
            null);

        await store.CreateAsync(session, cancellationToken);
        UploadMetrics.UploadsStarted.Add(1);
        logger.LogInformation("Started chunk upload {UploadId} for {FileName}.", session.Id, session.FileName);

        return UploadResult<UploadSessionDescriptor>.Success(ToDescriptor(session));
    }

    public async Task<UploadResult<ChunkReceipt>> UploadChunkAsync(
        Guid uploadId,
        int chunkIndex,
        Stream content,
        long contentLength,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var session = await store.GetAsync(uploadId, cancellationToken);
        if (session is null)
        {
            return UploadResult<ChunkReceipt>.Failure(UploadErrorCode.NotFound, "The upload session does not exist.");
        }

        var validationError = UploadValidation.ValidateChunk(session, chunkIndex, content, contentLength, sha256, _options);
        if (validationError is not null)
        {
            return UploadResult<ChunkReceipt>.Failure(validationError.Code, validationError.Message);
        }

        var normalizedHash = UploadValidation.NormalizeSha256(sha256);
        var existing = await store.GetChunkAsync(uploadId, chunkIndex, cancellationToken);
        if (existing is not null)
        {
            return existing.ContentLength == contentLength && existing.Sha256 == normalizedHash
                ? UploadResult<ChunkReceipt>.Success(ToReceipt(existing, true))
                : UploadResult<ChunkReceipt>.Failure(UploadErrorCode.ChunkConflict, "The chunk index is already associated with different content.");
        }

        var storageResult = await storage.WriteChunkAsync(
            uploadId,
            chunkIndex,
            content,
            contentLength,
            normalizedHash,
            cancellationToken);

        var storageFailure = MapStorageFailure(storageResult.Outcome);
        if (storageFailure is not null)
        {
            return UploadResult<ChunkReceipt>.Failure(storageFailure.Value.Code, storageFailure.Value.Message);
        }

        var now = timeProvider.GetUtcNow();
        var chunk = new UploadChunkRecord(uploadId, chunkIndex, contentLength, normalizedHash, now);
        var registration = await store.RegisterChunkAsync(
            chunk,
            now,
            now.Add(_options.IncompleteUploadRetention),
            cancellationToken);

        if (registration.Outcome is ChunkRegistrationOutcome.InvalidState or ChunkRegistrationOutcome.NotFound)
        {
            if (storageResult.Outcome == ChunkStorageWriteOutcome.Created)
            {
                await storage.DeleteChunkAsync(uploadId, chunkIndex, cancellationToken);
            }

            var code = registration.Outcome == ChunkRegistrationOutcome.NotFound
                ? UploadErrorCode.NotFound
                : UploadErrorCode.InvalidState;
            return UploadResult<ChunkReceipt>.Failure(code, "The upload session changed while the chunk was being stored.");
        }

        if (registration.Outcome == ChunkRegistrationOutcome.Conflict)
        {
            return UploadResult<ChunkReceipt>.Failure(UploadErrorCode.ChunkConflict, "The chunk index is already associated with different content.");
        }

        var storedChunk = registration.ExistingChunk ?? chunk;
        var wasAlreadyUploaded = registration.Outcome == ChunkRegistrationOutcome.AlreadyRegistered ||
                                 storageResult.Outcome == ChunkStorageWriteOutcome.ExistingMatches;

        UploadMetrics.ChunksStored.Add(wasAlreadyUploaded ? 0 : 1);
        UploadMetrics.BytesStored.Add(wasAlreadyUploaded ? 0 : contentLength);
        logger.LogInformation("Stored chunk {ChunkIndex} for upload {UploadId}.", chunkIndex, uploadId);
        return UploadResult<ChunkReceipt>.Success(ToReceipt(storedChunk, wasAlreadyUploaded));
    }

    public async Task<UploadResult<UploadStatusDescriptor>> GetStatusAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var session = await store.GetAsync(uploadId, cancellationToken);
        if (session is null)
        {
            return UploadResult<UploadStatusDescriptor>.Failure(UploadErrorCode.NotFound, "The upload session does not exist.");
        }

        var chunks = await store.GetChunksAsync(uploadId, cancellationToken);
        return UploadResult<UploadStatusDescriptor>.Success(new(
            session.Id,
            session.State,
            chunks.Count,
            session.TotalChunks,
            chunks.Sum(static chunk => chunk.ContentLength),
            session.ContentLength,
            session.UpdatedAt));
    }

    public async Task<UploadResult<IReadOnlyList<int>>> GetMissingChunksAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var session = await store.GetAsync(uploadId, cancellationToken);
        if (session is null)
        {
            return UploadResult<IReadOnlyList<int>>.Failure(UploadErrorCode.NotFound, "The upload session does not exist.");
        }

        var chunks = await store.GetChunksAsync(uploadId, cancellationToken);
        var present = chunks.Select(static chunk => chunk.ChunkIndex).ToHashSet();
        var missing = Enumerable.Range(0, session.TotalChunks).Where(index => !present.Contains(index)).ToArray();
        return UploadResult<IReadOnlyList<int>>.Success(missing);
    }

    public async Task<UploadResult<UploadedFileDescriptor>> CompleteAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var session = await store.GetAsync(uploadId, cancellationToken);
        if (session is null)
        {
            return UploadResult<UploadedFileDescriptor>.Failure(UploadErrorCode.NotFound, "The upload session does not exist.");
        }

        if (session.State == UploadState.Completed && session.StorageKey is not null && session.CompletedAt is not null)
        {
            return UploadResult<UploadedFileDescriptor>.Success(new(
                session.Id,
                session.FileName,
                session.ContentLength,
                session.Sha256,
                session.StorageKey,
                session.CompletedAt.Value));
        }

        var owner = Guid.NewGuid().ToString("N");
        var now = timeProvider.GetUtcNow();
        var acquired = await coordinator.TryAcquireAsync(
            uploadId,
            UploadLeasePurpose.Completion,
            owner,
            now,
            _options.CompletionLeaseDuration,
            cancellationToken);

        if (!acquired)
        {
            return UploadResult<UploadedFileDescriptor>.Failure(UploadErrorCode.LeaseUnavailable, "Another process owns completion for this upload.");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            session = await store.GetAsync(uploadId, cancellationToken)
                ?? throw new InvalidOperationException("The upload disappeared after the completion lease was acquired.");
            var chunks = (await store.GetChunksAsync(uploadId, cancellationToken))
                .OrderBy(static chunk => chunk.ChunkIndex)
                .ToArray();

            if (!HasAllChunks(session, chunks))
            {
                await coordinator.ReleaseAsync(uploadId, owner, UploadState.Uploading, timeProvider.GetUtcNow(), cancellationToken);
                return UploadResult<UploadedFileDescriptor>.Failure(UploadErrorCode.IncompleteUpload, "One or more chunks are missing.");
            }

            if (chunks.Sum(static chunk => chunk.ContentLength) != session.ContentLength)
            {
                await coordinator.ReleaseAsync(uploadId, owner, UploadState.Uploading, timeProvider.GetUtcNow(), cancellationToken);
                return UploadResult<UploadedFileDescriptor>.Failure(UploadErrorCode.SizeMismatch, "The combined chunk size does not match the file size.");
            }

            var storedFile = await storage.AssembleAsync(session, chunks, cancellationToken);
            if (storedFile.ContentLength != session.ContentLength || storedFile.Sha256 != session.Sha256)
            {
                await storage.DeleteCompletedFileAsync(uploadId, cancellationToken);
                await coordinator.ReleaseAsync(uploadId, owner, UploadState.Failed, timeProvider.GetUtcNow(), cancellationToken);
                UploadMetrics.UploadsFailed.Add(1);
                var code = storedFile.ContentLength != session.ContentLength
                    ? UploadErrorCode.SizeMismatch
                    : UploadErrorCode.HashMismatch;
                return UploadResult<UploadedFileDescriptor>.Failure(code, "The assembled file failed integrity validation.");
            }

            var completedAt = timeProvider.GetUtcNow();
            var descriptor = new UploadedFileDescriptor(
                session.Id,
                session.FileName,
                storedFile.ContentLength,
                storedFile.Sha256,
                storedFile.StorageKey,
                completedAt);

            if (!await coordinator.MarkCompletedAsync(uploadId, owner, descriptor, cancellationToken))
            {
                return UploadResult<UploadedFileDescriptor>.Failure(UploadErrorCode.LeaseUnavailable, "The completion lease was lost before the upload was committed.");
            }

            UploadMetrics.UploadsCompleted.Add(1);
            logger.LogInformation("Completed upload {UploadId} at {StorageKey}.", uploadId, descriptor.StorageKey);
            return UploadResult<UploadedFileDescriptor>.Success(descriptor);
        }
        catch
        {
            await coordinator.ReleaseAsync(uploadId, owner, UploadState.Uploading, timeProvider.GetUtcNow(), CancellationToken.None);
            UploadMetrics.UploadsFailed.Add(1);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            UploadMetrics.CompletionDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    public async Task<UploadResult> CancelAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        var cancelled = await store.CancelAsync(uploadId, timeProvider.GetUtcNow(), cancellationToken);
        if (!cancelled)
        {
            var session = await store.GetAsync(uploadId, cancellationToken);
            var code = session is null ? UploadErrorCode.NotFound : UploadErrorCode.InvalidState;
            return UploadResult.Failure(code, session is null
                ? "The upload session does not exist."
                : $"The upload cannot be cancelled while it is {session.State}.");
        }

        await storage.DeleteUploadArtifactsAsync(uploadId, includeCompletedFile: false, cancellationToken);
        await store.MarkArtifactsDeletedAsync(uploadId, timeProvider.GetUtcNow(), cancellationToken);
        logger.LogInformation("Cancelled upload {UploadId}.", uploadId);
        return UploadResult.Success();
    }

    private static bool HasAllChunks(UploadSessionRecord session, IReadOnlyList<UploadChunkRecord> chunks)
    {
        if (chunks.Count != session.TotalChunks)
        {
            return false;
        }

        for (var index = 0; index < chunks.Count; index++)
        {
            if (chunks[index].ChunkIndex != index)
            {
                return false;
            }
        }

        return true;
    }

    private static (UploadErrorCode Code, string Message)? MapStorageFailure(ChunkStorageWriteOutcome outcome) => outcome switch
    {
        ChunkStorageWriteOutcome.Conflict => (UploadErrorCode.ChunkConflict, "The stored chunk conflicts with the supplied content."),
        ChunkStorageWriteOutcome.HashMismatch => (UploadErrorCode.HashMismatch, "The chunk hash does not match the supplied SHA-256."),
        ChunkStorageWriteOutcome.SizeMismatch => (UploadErrorCode.SizeMismatch, "The chunk size does not match the supplied length."),
        _ => null
    };

    private static UploadSessionDescriptor ToDescriptor(UploadSessionRecord session) => new(
        session.Id,
        session.FileName,
        session.ContentLength,
        session.TotalChunks,
        session.Sha256,
        session.State,
        session.CreatedAt,
        session.ExpiresAt ?? session.CreatedAt);

    private static ChunkReceipt ToReceipt(UploadChunkRecord chunk, bool wasAlreadyUploaded) => new(
        chunk.UploadId,
        chunk.ChunkIndex,
        chunk.ContentLength,
        chunk.Sha256,
        wasAlreadyUploaded);
}
