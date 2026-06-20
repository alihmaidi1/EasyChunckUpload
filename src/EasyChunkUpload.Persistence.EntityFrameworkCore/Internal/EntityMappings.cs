using EasyChunkUpload.Abstractions;

namespace EasyChunkUpload.Persistence.EntityFrameworkCore.Internal;

internal static class EntityMappings
{
    public static UploadSessionRecord ToRecord(this UploadSessionEntity entity) => new(
        entity.Id,
        entity.FileName,
        entity.ContentLength,
        entity.TotalChunks,
        entity.Sha256,
        entity.State,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.ExpiresAt,
        entity.StorageKey,
        entity.CompletedAt,
        entity.LeaseOwner,
        entity.LeasePurpose,
        entity.LeaseExpiresAt,
        entity.Version,
        entity.ArtifactsDeletedAt);

    public static UploadChunkRecord ToRecord(this UploadChunkEntity entity) => new(
        entity.UploadId,
        entity.ChunkIndex,
        entity.ContentLength,
        entity.Sha256,
        entity.CreatedAt);

    public static UploadSessionEntity ToEntity(this UploadSessionRecord record) => new()
    {
        Id = record.Id,
        FileName = record.FileName,
        ContentLength = record.ContentLength,
        TotalChunks = record.TotalChunks,
        Sha256 = record.Sha256,
        State = record.State,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt,
        ExpiresAt = record.ExpiresAt,
        StorageKey = record.StorageKey,
        CompletedAt = record.CompletedAt,
        LeaseOwner = record.LeaseOwner,
        LeasePurpose = record.LeasePurpose,
        LeaseExpiresAt = record.LeaseExpiresAt,
        Version = record.Version,
        ArtifactsDeletedAt = record.ArtifactsDeletedAt
    };

    public static UploadChunkEntity ToEntity(this UploadChunkRecord record) => new()
    {
        UploadId = record.UploadId,
        ChunkIndex = record.ChunkIndex,
        ContentLength = record.ContentLength,
        Sha256 = record.Sha256,
        CreatedAt = record.CreatedAt
    };
}
