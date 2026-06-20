namespace EasyChunkUpload.Abstractions;

public enum UploadState
{
    Created,
    Uploading,
    Completing,
    Completed,
    Cancelled,
    Failed
}

public enum UploadErrorCode
{
    InvalidRequest,
    NotFound,
    InvalidState,
    ChunkConflict,
    HashMismatch,
    SizeMismatch,
    IncompleteUpload,
    LeaseUnavailable
}

public enum UploadLeasePurpose
{
    Completion,
    Cleanup
}

public sealed record UploadError(UploadErrorCode Code, string Message);

public class UploadResult
{
    protected UploadResult(bool isSuccess, UploadError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public UploadError? Error { get; }

    public static UploadResult Success() => new(true, null);

    public static UploadResult Failure(UploadErrorCode code, string message) => new(false, new(code, message));
}

public sealed class UploadResult<T> : UploadResult
{
    private UploadResult(bool isSuccess, T? value, UploadError? error) : base(isSuccess, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static UploadResult<T> Success(T value) => new(true, value, null);

    public new static UploadResult<T> Failure(UploadErrorCode code, string message) => new(false, default, new(code, message));
}

public sealed record StartUploadRequest(
    string FileName,
    long ContentLength,
    int TotalChunks,
    string Sha256);

public sealed record UploadSessionDescriptor(
    Guid UploadId,
    string FileName,
    long ContentLength,
    int TotalChunks,
    string Sha256,
    UploadState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record ChunkReceipt(
    Guid UploadId,
    int ChunkIndex,
    long ContentLength,
    string Sha256,
    bool WasAlreadyUploaded);

public sealed record UploadStatusDescriptor(
    Guid UploadId,
    UploadState State,
    int UploadedChunks,
    int TotalChunks,
    long UploadedBytes,
    long ContentLength,
    DateTimeOffset UpdatedAt);

public sealed record UploadedFileDescriptor(
    Guid UploadId,
    string FileName,
    long ContentLength,
    string Sha256,
    string StorageKey,
    DateTimeOffset CompletedAt);

public sealed record UploadSessionRecord(
    Guid Id,
    string FileName,
    long ContentLength,
    int TotalChunks,
    string Sha256,
    UploadState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ExpiresAt,
    string? StorageKey,
    DateTimeOffset? CompletedAt,
    string? LeaseOwner,
    UploadLeasePurpose? LeasePurpose,
    DateTimeOffset? LeaseExpiresAt,
    long Version,
    DateTimeOffset? ArtifactsDeletedAt);

public sealed record UploadChunkRecord(
    Guid UploadId,
    int ChunkIndex,
    long ContentLength,
    string Sha256,
    DateTimeOffset CreatedAt);

public enum ChunkRegistrationOutcome
{
    Registered,
    AlreadyRegistered,
    Conflict,
    InvalidState,
    NotFound
}

public sealed record ChunkRegistrationResult(ChunkRegistrationOutcome Outcome, UploadChunkRecord? ExistingChunk = null);

public enum ChunkStorageWriteOutcome
{
    Created,
    ExistingMatches,
    Conflict,
    HashMismatch,
    SizeMismatch
}

public sealed record ChunkStorageWriteResult(ChunkStorageWriteOutcome Outcome, long ContentLength, string Sha256);

public sealed record StorageObjectDescriptor(string StorageKey, long ContentLength, string Sha256);

public sealed record MaintenanceCandidate(Guid UploadId, UploadState State, DateTimeOffset? LeaseExpiresAt);
