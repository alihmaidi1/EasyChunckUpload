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
        session.UpdatedAt = updatedAt;
        session.ExpiresAt = expiresAt;

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
            return new(current is null ? ChunkRegistrationOutcome.NotFound : ChunkRegistrationOutcome.InvalidState);
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
        entity.UpdatedAt = cancelledAt;
        entity.ExpiresAt = cancelledAt;
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
        return await dbContext.UploadSessions
            .AsNoTracking()
            .Where(value =>
                value.ArtifactsDeletedAt == null &&
                value.ExpiresAt != null &&
                value.ExpiresAt <= now &&
                value.State != UploadState.Completed &&
                value.State != UploadState.Completing)
            .OrderBy(value => value.ExpiresAt)
            .Take(batchSize)
            .Select(value => new MaintenanceCandidate(value.Id, value.State, value.LeaseExpiresAt))
            .ToListAsync(cancellationToken);
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

        entity.ArtifactsDeletedAt = deletedAt;
        entity.UpdatedAt = deletedAt;
        entity.LeaseOwner = null;
        entity.LeasePurpose = null;
        entity.LeaseExpiresAt = null;
        entity.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool Matches(UploadChunkEntity entity, UploadChunkRecord record) =>
        entity.ContentLength == record.ContentLength && entity.Sha256 == record.Sha256;
}
