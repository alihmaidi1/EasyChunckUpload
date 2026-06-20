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

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? StorageKey { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? LeaseOwner { get; set; }

    public UploadLeasePurpose? LeasePurpose { get; set; }

    public DateTime? LeaseExpiresAt { get; set; }

    public long Version { get; set; }

    public DateTime? ArtifactsDeletedAt { get; set; }

    public ICollection<UploadChunkEntity> Chunks { get; } = new List<UploadChunkEntity>();
}

internal sealed class UploadChunkEntity
{
    public Guid UploadId { get; set; }

    public int ChunkIndex { get; set; }

    public long ContentLength { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public UploadSessionEntity Session { get; set; } = null!;
}
