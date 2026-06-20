using EasyChunkUpload.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EasyChunkUpload.Persistence.EntityFrameworkCore.Internal;

internal sealed class EntityFrameworkUploadSessionStore(UploadDbContext dbContext) : IUploadSessionStore
{
    public async Task CreateAsync(UploadSessionRecord session, CancellationToken cancellationToken)
    {
        dbContext.UploadSessions.Add(session.ToEntity());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UploadSessionRecord?> GetAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.UploadSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == uploadId, cancellationToken);
        return entity?.ToRecord();
    }

    public async Task<UploadChunkRecord?> GetChunkAsync(
        Guid uploadId,
        int chunkIndex,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.UploadChunks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.UploadId == uploadId && value.ChunkIndex == chunkIndex,
                cancellationToken);
        return entity?.ToRecord();
    }

    public async Task<IReadOnlyList<UploadChunkRecord>> GetChunksAsync(
        Guid uploadId,
        CancellationToken cancellationToken)
    {
        var entities = await dbContext.UploadChunks
            .AsNoTracking()
            .Where(value => value.UploadId == uploadId)
            .OrderBy(value => value.ChunkIndex)
            .ToListAsync(cancellationToken);
        return entities.Select(static value => value.ToRecord()).ToArray();
    }

    public async Task<ChunkRegistrationResult> RegisterChunkAsync(
        UploadChunkRecord chunk,
        DateTimeOffset updatedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var session = await dbContext.UploadSessions.SingleOrDefaultAsync(
            value => value.Id == chunk.UploadId,
            cancellationToken);
        if (session is null)
        {
            return new(ChunkRegistrationOutcome.NotFound);
        }

        if (session.State is not (UploadState.Created or UploadState.Uploading) || session.LeaseOwner is not null)
        {
            return new(ChunkRegistrationOutcome.InvalidState);
        }

        var existing = await dbContext.UploadChunks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.UploadId == chunk.UploadId && value.ChunkIndex == chunk.ChunkIndex,
                cancellationToken);
        if (existing is not null)
        {
            return Matches(existing, chunk)
                ? new(ChunkRegistrationOutcome.AlreadyRegistered, existing.ToRecord())
                : new(ChunkRegistrationOutcome.Conflict, existing.ToRecord());
        }

        dbContext.UploadChunks.Add(chunk.ToEntity());
        session.State = UploadState.Uploading;
        session.UpdatedAt = updatedAt.UtcDateTime;
        session.ExpiresAt = expiresAt.UtcDateTime;
        session.Version++;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(ChunkRegistrationOutcome.Registered, chunk);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            existing = await dbContext.UploadChunks
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.UploadId == chunk.UploadId && value.ChunkIndex == chunk.ChunkIndex,
                    cancellationToken);
            if (existing is not null)
            {
                return Matches(existing, chunk)
                    ? new(ChunkRegistrationOutcome.AlreadyRegistered, existing.ToRecord())
                    : new(ChunkRegistrationOutcome.Conflict, existing.ToRecord());
            }

            var current = await dbContext.UploadSessions
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == chunk.UploadId, cancellationToken);
            if (current is null)
            {
                return new(ChunkRegistrationOutcome.NotFound);
            }

            if (current.State is not (UploadState.Created or UploadState.Uploading) || current.LeaseOwner is not null)
            {
                return new(ChunkRegistrationOutcome.InvalidState);
            }

            throw;
        }
    }

    public async Task<bool> CancelAsync(
        Guid uploadId,
        DateTimeOffset cancelledAt,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.UploadSessions.SingleOrDefaultAsync(value => value.Id == uploadId, cancellationToken);
        if (entity is null || entity.State is not (UploadState.Created or UploadState.Uploading or UploadState.Failed))
        {
            return false;
        }

        entity.State = UploadState.Cancelled;
        entity.UpdatedAt = cancelledAt.UtcDateTime;
        entity.ExpiresAt = cancelledAt.UtcDateTime;
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

    public async Task<IReadOnlyList<MaintenanceCandidate>> GetMaintenanceCandidatesAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.UploadSessions
            .AsNoTracking()
            .Where(value =>
                value.ArtifactsDeletedAt == null &&
                value.ExpiresAt != null &&
                value.ExpiresAt <= now.UtcDateTime &&
                value.State != UploadState.Completed &&
                value.State != UploadState.Completing)
            .OrderBy(value => value.ExpiresAt)
            .Take(batchSize)
            .Select(value => new { value.Id, value.State, value.LeaseExpiresAt })
            .ToListAsync(cancellationToken);
        return candidates
            .Select(static value => new MaintenanceCandidate(
                value.Id,
                value.State,
                value.LeaseExpiresAt.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(value.LeaseExpiresAt.Value, DateTimeKind.Utc))
                    : null))
            .ToArray();
    }

    public async Task MarkArtifactsDeletedAsync(
        Guid uploadId,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.UploadSessions.SingleOrDefaultAsync(value => value.Id == uploadId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.ArtifactsDeletedAt = deletedAt.UtcDateTime;
        entity.UpdatedAt = deletedAt.UtcDateTime;
        entity.LeaseOwner = null;
        entity.LeasePurpose = null;
        entity.LeaseExpiresAt = null;
        entity.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryMarkArtifactsDeletedAsync(
        Guid uploadId,
        string owner,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var updated = await dbContext.UploadSessions
            .Where(value =>
                value.Id == uploadId &&
                value.State == UploadState.Cancelled &&
                value.LeasePurpose == UploadLeasePurpose.Cleanup &&
                value.LeaseOwner == owner)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(value => value.ArtifactsDeletedAt, deletedAt.UtcDateTime)
                    .SetProperty(value => value.UpdatedAt, deletedAt.UtcDateTime)
                    .SetProperty(value => value.LeaseOwner, (string?)null)
                    .SetProperty(value => value.LeasePurpose, (UploadLeasePurpose?)null)
                    .SetProperty(value => value.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(value => value.Version, value => value.Version + 1),
                cancellationToken);
        return updated == 1;
    }

    public async Task<int> DeleteExpiredMetadataAsync(
        DateTimeOffset deletedBefore,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.UploadSessions
            .AsNoTracking()
            .Where(value =>
                value.State != UploadState.Completed &&
                value.ArtifactsDeletedAt != null &&
                value.ArtifactsDeletedAt <= deletedBefore.UtcDateTime)
            .OrderBy(value => value.ArtifactsDeletedAt)
            .Take(batchSize)
            .Select(value => value.Id)
            .ToArrayAsync(cancellationToken);
        if (ids.Length == 0)
        {
            return 0;
        }

        dbContext.ChangeTracker.Clear();
        var sessions = await dbContext.UploadSessions
            .Where(value => ids.Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        dbContext.UploadSessions.RemoveRange(sessions);
        await dbContext.SaveChangesAsync(cancellationToken);
        return sessions.Length;
    }

    private static bool Matches(UploadChunkEntity entity, UploadChunkRecord record) =>
        entity.ContentLength == record.ContentLength && entity.Sha256 == record.Sha256;
}
