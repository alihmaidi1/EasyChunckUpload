using EasyChunkUpload.Abstractions;

namespace EasyChunkUpload.Persistence.EntityFrameworkCore.Internal;

internal sealed class UploadSessionEntity
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public long ContentLength { get; set; }

    public int TotalChunks { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public UploadState State { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string? StorageKey { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? LeaseOwner { get; set; }

    public UploadLeasePurpose? LeasePurpose { get; set; }

    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public long Version { get; set; }

    public DateTimeOffset? ArtifactsDeletedAt { get; set; }

    public ICollection<UploadChunkEntity> Chunks { get; } = new List<UploadChunkEntity>();
}

internal sealed class UploadChunkEntity
{
    public Guid UploadId { get; set; }

    public int ChunkIndex { get; set; }

    public long ContentLength { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public UploadSessionEntity Session { get; set; } = null!;
}
