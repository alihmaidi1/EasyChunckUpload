using EasyChunkUpload.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EasyChunkUpload.Persistence.EntityFrameworkCore.Internal;

internal sealed class EntityFrameworkUploadCompletionCoordinator(UploadDbContext dbContext)
    : IUploadCompletionCoordinator
{
    public async Task<bool> TryAcquireAsync(
        Guid uploadId,
        UploadLeasePurpose purpose,
        string owner,
        DateTimeOffset now,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.UploadSessions.SingleOrDefaultAsync(value => value.Id == uploadId, cancellationToken);
        if (entity is null || !CanAcquire(entity, purpose, now))
        {
            return false;
        }

        entity.State = purpose == UploadLeasePurpose.Completion ? UploadState.Completing : UploadState.Cancelled;
        entity.LeaseOwner = owner;
        entity.LeasePurpose = purpose;
        entity.LeaseExpiresAt = now.Add(duration).UtcDateTime;
        entity.UpdatedAt = now.UtcDateTime;
        entity.Version++;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<bool> MarkCompletedAsync(
        Guid uploadId,
        string owner,
        UploadedFileDescriptor file,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.UploadSessions.SingleOrDefaultAsync(value => value.Id == uploadId, cancellationToken);
        if (entity is null ||
            entity.State != UploadState.Completing ||
            entity.LeasePurpose != UploadLeasePurpose.Completion ||
            entity.LeaseOwner != owner)
        {
            return false;
        }

        entity.State = UploadState.Completed;
        entity.StorageKey = file.StorageKey;
        entity.CompletedAt = file.CompletedAt.UtcDateTime;
        entity.UpdatedAt = file.CompletedAt.UtcDateTime;
        entity.ExpiresAt = null;
        entity.LeaseOwner = null;
        entity.LeasePurpose = null;
        entity.LeaseExpiresAt = null;
        entity.Version++;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task ReleaseAsync(
        Guid uploadId,
        string owner,
        UploadState targetState,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var entity = await dbContext.UploadSessions.SingleOrDefaultAsync(value => value.Id == uploadId, cancellationToken);
        if (entity is null || entity.LeaseOwner != owner)
        {
            return;
        }

        entity.State = targetState;
        entity.UpdatedAt = updatedAt.UtcDateTime;
        entity.LeaseOwner = null;
        entity.LeasePurpose = null;
        entity.LeaseExpiresAt = null;
        entity.Version++;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
        }
    }

    public Task<int> RecoverExpiredCompletionLeasesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return dbContext.UploadSessions
            .Where(value =>
                value.State == UploadState.Completing &&
                value.LeasePurpose == UploadLeasePurpose.Completion &&
                value.LeaseExpiresAt <= now.UtcDateTime)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(value => value.State, UploadState.Uploading)
                    .SetProperty(value => value.LeaseOwner, (string?)null)
                    .SetProperty(value => value.LeasePurpose, (UploadLeasePurpose?)null)
                    .SetProperty(value => value.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(value => value.UpdatedAt, now.UtcDateTime)
                    .SetProperty(value => value.Version, value => value.Version + 1),
                cancellationToken);
    }

    public async Task<bool> TryRenewAsync(
        Guid uploadId,
        UploadLeasePurpose purpose,
        string owner,
        DateTimeOffset now,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var expectedState = purpose == UploadLeasePurpose.Completion
            ? UploadState.Completing
            : UploadState.Cancelled;
        var updated = await dbContext.UploadSessions
            .Where(value =>
                value.Id == uploadId &&
                value.State == expectedState &&
                value.LeasePurpose == purpose &&
                value.LeaseOwner == owner &&
                value.LeaseExpiresAt > now.UtcDateTime)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(value => value.LeaseExpiresAt, now.Add(duration).UtcDateTime)
                    .SetProperty(value => value.UpdatedAt, now.UtcDateTime)
                    .SetProperty(value => value.Version, value => value.Version + 1),
                cancellationToken);
        return updated == 1;
    }

    private static bool CanAcquire(
        UploadSessionEntity entity,
        UploadLeasePurpose purpose,
        DateTimeOffset now)
    {
        if (entity.LeaseOwner is not null && entity.LeaseExpiresAt > now.UtcDateTime)
        {
            return false;
        }

        return purpose switch
        {
            UploadLeasePurpose.Completion =>
                entity.State is UploadState.Created or UploadState.Uploading ||
                entity.State == UploadState.Completing && entity.LeaseExpiresAt <= now.UtcDateTime,
            UploadLeasePurpose.Cleanup =>
                entity.ArtifactsDeletedAt is null &&
                entity.ExpiresAt <= now.UtcDateTime &&
                entity.State is UploadState.Created or UploadState.Uploading or UploadState.Failed or UploadState.Cancelled,
            _ => false
        };
    }
}
