using System.Collections.Concurrent;
using System.Security.Cryptography;
using EasyChunkUpload.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EasyChunkUpload.UnitTests;

public sealed class ChunkUploadServiceTests
{
    [Fact]
    public async Task StartAsync_RejectsInvalidHash()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IChunkUploadService>();

        var result = await service.StartAsync(new("file.bin", 3, 1, "invalid"));

        Assert.False(result.IsSuccess);
        Assert.Equal(UploadErrorCode.InvalidRequest, result.Error?.Code);
    }

    [Fact]
    public async Task UploadChunkAsync_IsIdempotentForMatchingChunk()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IChunkUploadService>();
        var bytes = "abc"u8.ToArray();
        var hash = Hash(bytes);
        var started = await service.StartAsync(new("file.bin", bytes.Length, 1, hash));

        var first = await service.UploadChunkAsync(started.Value!.UploadId, 0, new MemoryStream(bytes), bytes.Length, hash);
        var second = await service.UploadChunkAsync(started.Value.UploadId, 0, new MemoryStream(bytes), bytes.Length, hash);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(second.Value?.WasAlreadyUploaded);
    }

    [Fact]
    public async Task CompleteAsync_RejectsMissingChunks()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IChunkUploadService>();
        var bytes = "abcdef"u8.ToArray();
        var started = await service.StartAsync(new("file.bin", bytes.Length, 2, Hash(bytes)));
        var first = bytes[..3];
        await service.UploadChunkAsync(started.Value!.UploadId, 0, new MemoryStream(first), first.Length, Hash(first));

        var completed = await service.CompleteAsync(started.Value.UploadId);

        Assert.False(completed.IsSuccess);
        Assert.Equal(UploadErrorCode.IncompleteUpload, completed.Error?.Code);
    }

    private static ServiceProvider CreateProvider()
    {
        var state = new InMemoryState();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEasyChunkUpload();
        services.AddSingleton<IUploadSessionStore>(state);
        services.AddSingleton<IUploadCompletionCoordinator>(state);
        services.AddSingleton<IChunkStorage>(state);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class InMemoryState : IUploadSessionStore, IUploadCompletionCoordinator, IChunkStorage
    {
        private readonly ConcurrentDictionary<Guid, UploadSessionRecord> _sessions = new();
        private readonly ConcurrentDictionary<(Guid UploadId, int Index), UploadChunkRecord> _chunks = new();

        public Task CreateAsync(UploadSessionRecord session, CancellationToken cancellationToken)
        {
            _sessions[session.Id] = session;
            return Task.CompletedTask;
        }

        public Task<UploadSessionRecord?> GetAsync(Guid uploadId, CancellationToken cancellationToken)
        {
            _sessions.TryGetValue(uploadId, out var session);
            return Task.FromResult(session);
        }

        public Task<UploadChunkRecord?> GetChunkAsync(Guid uploadId, int chunkIndex, CancellationToken cancellationToken)
        {
            _chunks.TryGetValue((uploadId, chunkIndex), out var chunk);
            return Task.FromResult(chunk);
        }

        public Task<IReadOnlyList<UploadChunkRecord>> GetChunksAsync(Guid uploadId, CancellationToken cancellationToken)
        {
            IReadOnlyList<UploadChunkRecord> chunks = _chunks.Values
                .Where(value => value.UploadId == uploadId)
                .OrderBy(value => value.ChunkIndex)
                .ToArray();
            return Task.FromResult(chunks);
        }

        public Task<ChunkRegistrationResult> RegisterChunkAsync(
            UploadChunkRecord chunk,
            DateTimeOffset updatedAt,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            if (!_sessions.TryGetValue(chunk.UploadId, out var session))
            {
                return Task.FromResult(new ChunkRegistrationResult(ChunkRegistrationOutcome.NotFound));
            }

            if (session.State is not (UploadState.Created or UploadState.Uploading))
            {
                return Task.FromResult(new ChunkRegistrationResult(ChunkRegistrationOutcome.InvalidState));
            }

            if (_chunks.TryGetValue((chunk.UploadId, chunk.ChunkIndex), out var existing))
            {
                var outcome = existing.ContentLength == chunk.ContentLength && existing.Sha256 == chunk.Sha256
                    ? ChunkRegistrationOutcome.AlreadyRegistered
                    : ChunkRegistrationOutcome.Conflict;
                return Task.FromResult(new ChunkRegistrationResult(outcome, existing));
            }

            _chunks[(chunk.UploadId, chunk.ChunkIndex)] = chunk;
            _sessions[chunk.UploadId] = session with
            {
                State = UploadState.Uploading,
                UpdatedAt = updatedAt,
                ExpiresAt = expiresAt,
                Version = session.Version + 1
            };
            return Task.FromResult(new ChunkRegistrationResult(ChunkRegistrationOutcome.Registered, chunk));
        }

        public Task<bool> CancelAsync(Guid uploadId, DateTimeOffset cancelledAt, CancellationToken cancellationToken)
        {
            if (!_sessions.TryGetValue(uploadId, out var session))
            {
                return Task.FromResult(false);
            }

            _sessions[uploadId] = session with { State = UploadState.Cancelled, UpdatedAt = cancelledAt };
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<MaintenanceCandidate>> GetMaintenanceCandidatesAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MaintenanceCandidate>>([]);

        public Task MarkArtifactsDeletedAsync(Guid uploadId, DateTimeOffset deletedAt, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> TryAcquireAsync(Guid uploadId, UploadLeasePurpose purpose, string owner, DateTimeOffset now, TimeSpan duration, CancellationToken cancellationToken)
        {
            if (!_sessions.TryGetValue(uploadId, out var session) || session.State is not (UploadState.Created or UploadState.Uploading))
            {
                return Task.FromResult(false);
            }

            _sessions[uploadId] = session with
            {
                State = UploadState.Completing,
                LeaseOwner = owner,
                LeasePurpose = purpose,
                LeaseExpiresAt = now.Add(duration)
            };
            return Task.FromResult(true);
        }

        public Task<bool> MarkCompletedAsync(Guid uploadId, string owner, UploadedFileDescriptor file, CancellationToken cancellationToken)
        {
            var session = _sessions[uploadId];
            _sessions[uploadId] = session with
            {
                State = UploadState.Completed,
                StorageKey = file.StorageKey,
                CompletedAt = file.CompletedAt,
                LeaseOwner = null,
                LeasePurpose = null,
                LeaseExpiresAt = null
            };
            return Task.FromResult(true);
        }

        public Task ReleaseAsync(Guid uploadId, string owner, UploadState targetState, DateTimeOffset updatedAt, CancellationToken cancellationToken)
        {
            var session = _sessions[uploadId];
            _sessions[uploadId] = session with
            {
                State = targetState,
                UpdatedAt = updatedAt,
                LeaseOwner = null,
                LeasePurpose = null,
                LeaseExpiresAt = null
            };
            return Task.CompletedTask;
        }

        public Task<int> RecoverExpiredCompletionLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(0);

        public async Task<ChunkStorageWriteResult> WriteChunkAsync(Guid uploadId, int chunkIndex, Stream content, long expectedLength, string expectedSha256, CancellationToken cancellationToken)
        {
            var hash = await SHA256.HashDataAsync(content, cancellationToken);
            var actual = Convert.ToHexString(hash).ToLowerInvariant();
            return actual == expectedSha256
                ? new(ChunkStorageWriteOutcome.Created, expectedLength, actual)
                : new(ChunkStorageWriteOutcome.HashMismatch, expectedLength, actual);
        }

        public Task DeleteChunkAsync(Guid uploadId, int chunkIndex, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StorageObjectDescriptor> AssembleAsync(UploadSessionRecord session, IReadOnlyList<UploadChunkRecord> chunks, CancellationToken cancellationToken) =>
            Task.FromResult(new StorageObjectDescriptor($"completed/{session.Id:N}/content", session.ContentLength, session.Sha256));

        public Task DeleteCompletedFileAsync(Guid uploadId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteUploadArtifactsAsync(Guid uploadId, bool includeCompletedFile, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
