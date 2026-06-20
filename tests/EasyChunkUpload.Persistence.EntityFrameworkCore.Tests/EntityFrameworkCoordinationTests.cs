using EasyChunkUpload.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EasyChunkUpload.Persistence.EntityFrameworkCore.Tests;

public sealed class EntityFrameworkCoordinationTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"easy-chunk-upload-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task CompletionLease_HasSingleOwnerAcrossProviders()
    {
        await using var firstProvider = CreateProvider();
        await using var secondProvider = CreateProvider();
        await EnsureDatabaseAsync(firstProvider);
        var uploadId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var scope = firstProvider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IUploadSessionStore>();
            await store.CreateAsync(CreateSession(uploadId, now), CancellationToken.None);
        }

        bool firstAcquired;
        await using (var scope = firstProvider.CreateAsyncScope())
        {
            firstAcquired = await scope.ServiceProvider
                .GetRequiredService<IUploadCompletionCoordinator>()
                .TryAcquireAsync(uploadId, UploadLeasePurpose.Completion, "first", now, TimeSpan.FromMinutes(5), CancellationToken.None);
        }

        bool secondAcquired;
        await using (var scope = secondProvider.CreateAsyncScope())
        {
            secondAcquired = await scope.ServiceProvider
                .GetRequiredService<IUploadCompletionCoordinator>()
                .TryAcquireAsync(uploadId, UploadLeasePurpose.Completion, "second", now, TimeSpan.FromMinutes(5), CancellationToken.None);
        }

        Assert.True(firstAcquired);
        Assert.False(secondAcquired);
    }

    [Fact]
    public async Task CompletionLease_RenewalPreventsAcquisitionAfterOriginalExpiry()
    {
        await using var firstProvider = CreateProvider();
        await using var secondProvider = CreateProvider();
        await EnsureDatabaseAsync(firstProvider);
        var uploadId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var scope = firstProvider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IUploadSessionStore>();
            await store.CreateAsync(CreateSession(uploadId, now), CancellationToken.None);
            var coordinator = scope.ServiceProvider.GetRequiredService<IUploadCompletionCoordinator>();
            Assert.True(await coordinator.TryAcquireAsync(
                uploadId,
                UploadLeasePurpose.Completion,
                "first",
                now,
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None));
            Assert.True(await coordinator.TryRenewAsync(
                uploadId,
                UploadLeasePurpose.Completion,
                "first",
                now.AddMilliseconds(50),
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None));
        }

        await using var secondScope = secondProvider.CreateAsyncScope();
        var secondCoordinator = secondScope.ServiceProvider.GetRequiredService<IUploadCompletionCoordinator>();
        var acquired = await secondCoordinator.TryAcquireAsync(
            uploadId,
            UploadLeasePurpose.Completion,
            "second",
            now.AddMilliseconds(110),
            TimeSpan.FromMinutes(1),
            CancellationToken.None);

        Assert.False(acquired);
    }

    [Fact]
    public async Task CleanupFinalization_RequiresLeaseOwner()
    {
        await using var provider = CreateProvider();
        await EnsureDatabaseAsync(provider);
        var uploadId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IUploadSessionStore>();
        var coordinator = scope.ServiceProvider.GetRequiredService<IUploadCompletionCoordinator>();
        await store.CreateAsync(CreateSession(uploadId, now), CancellationToken.None);
        Assert.True(await store.CancelAsync(uploadId, now, CancellationToken.None));
        Assert.True(await coordinator.TryAcquireAsync(
            uploadId,
            UploadLeasePurpose.Cleanup,
            "owner",
            now.AddSeconds(1),
            TimeSpan.FromMinutes(5),
            CancellationToken.None));

        var wrongOwner = await store.TryMarkArtifactsDeletedAsync(
            uploadId,
            "other",
            now.AddSeconds(2),
            CancellationToken.None);
        var correctOwner = await store.TryMarkArtifactsDeletedAsync(
            uploadId,
            "owner",
            now.AddSeconds(2),
            CancellationToken.None);

        Assert.False(wrongOwner);
        Assert.True(correctOwner);
    }

    [Fact]
    public async Task MetadataPurge_RemovesOnlyExpiredIncompleteSessions()
    {
        await using var provider = CreateProvider();
        await EnsureDatabaseAsync(provider);
        var uploadId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IUploadSessionStore>();
        var coordinator = scope.ServiceProvider.GetRequiredService<IUploadCompletionCoordinator>();
        await store.CreateAsync(CreateSession(uploadId, now), CancellationToken.None);
        await store.CreateAsync(CreateSession(completedId, now) with
        {
            State = UploadState.Completed,
            ExpiresAt = null,
            ArtifactsDeletedAt = now.AddDays(-10)
        }, CancellationToken.None);
        Assert.True(await store.CancelAsync(uploadId, now.AddDays(-10), CancellationToken.None));
        Assert.True(await coordinator.TryAcquireAsync(
            uploadId,
            UploadLeasePurpose.Cleanup,
            "owner",
            now.AddDays(-10).AddSeconds(1),
            TimeSpan.FromMinutes(5),
            CancellationToken.None));
        Assert.True(await store.TryMarkArtifactsDeletedAsync(
            uploadId,
            "owner",
            now.AddDays(-10).AddSeconds(2),
            CancellationToken.None));

        var deleted = await store.DeleteExpiredMetadataAsync(now.AddDays(-1), 100, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Null(await store.GetAsync(uploadId, CancellationToken.None));
        Assert.NotNull(await store.GetAsync(completedId, CancellationToken.None));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddEasyChunkUpload()
            .UseEntityFrameworkStore(options => options.UseSqlite($"Data Source={_databasePath}"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task EnsureDatabaseAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    private static UploadSessionRecord CreateSession(Guid uploadId, DateTimeOffset now) => new(
        uploadId,
        "file.bin",
        1,
        1,
        new string('a', 64),
        UploadState.Uploading,
        now,
        now,
        now.AddHours(1),
        null,
        null,
        null,
        null,
        null,
        0,
        null);
}
