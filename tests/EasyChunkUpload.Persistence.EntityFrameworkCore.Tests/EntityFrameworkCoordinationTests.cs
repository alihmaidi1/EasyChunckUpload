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
