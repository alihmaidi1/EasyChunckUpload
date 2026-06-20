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
        ToDateTimeOffset(entity.CreatedAt),
        ToDateTimeOffset(entity.UpdatedAt),
        ToDateTimeOffset(entity.ExpiresAt),
        entity.StorageKey,
        ToDateTimeOffset(entity.CompletedAt),
        entity.LeaseOwner,
        entity.LeasePurpose,
        ToDateTimeOffset(entity.LeaseExpiresAt),
        entity.Version,
        ToDateTimeOffset(entity.ArtifactsDeletedAt));

    public static UploadChunkRecord ToRecord(this UploadChunkEntity entity) => new(
        entity.UploadId,
        entity.ChunkIndex,
        entity.ContentLength,
        entity.Sha256,
        ToDateTimeOffset(entity.CreatedAt));

    public static UploadSessionEntity ToEntity(this UploadSessionRecord record) => new()
    {
        Id = record.Id,
        FileName = record.FileName,
        ContentLength = record.ContentLength,
        TotalChunks = record.TotalChunks,
        Sha256 = record.Sha256,
        State = record.State,
        CreatedAt = record.CreatedAt.UtcDateTime,
        UpdatedAt = record.UpdatedAt.UtcDateTime,
        ExpiresAt = record.ExpiresAt?.UtcDateTime,
        StorageKey = record.StorageKey,
        CompletedAt = record.CompletedAt?.UtcDateTime,
        LeaseOwner = record.LeaseOwner,
        LeasePurpose = record.LeasePurpose,
        LeaseExpiresAt = record.LeaseExpiresAt?.UtcDateTime,
        Version = record.Version,
        ArtifactsDeletedAt = record.ArtifactsDeletedAt?.UtcDateTime
    };

    public static UploadChunkEntity ToEntity(this UploadChunkRecord record) => new()
    {
        UploadId = record.UploadId,
        ChunkIndex = record.ChunkIndex,
        ContentLength = record.ContentLength,
        Sha256 = record.Sha256,
        CreatedAt = record.CreatedAt.UtcDateTime
    };

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value) =>
        value.HasValue ? ToDateTimeOffset(value.Value) : null;
}
