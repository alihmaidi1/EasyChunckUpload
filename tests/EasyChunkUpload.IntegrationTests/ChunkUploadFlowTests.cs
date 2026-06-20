using System.Security.Cryptography;
using EasyChunkUpload.Abstractions;
using EasyChunkUpload.Hosting;
using EasyChunkUpload.Persistence.EntityFrameworkCore;
using EasyChunkUpload.Storage.FileSystem;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EasyChunkUpload.IntegrationTests;

public sealed class ChunkUploadFlowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "easy-chunk-upload-integration", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"easy-chunk-upload-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task FullFlow_WorksAcrossDifferentServiceProviders()
    {
        await using var firstProvider = CreateProvider();
        await using var secondProvider = CreateProvider();
        await EnsureDatabaseAsync(firstProvider);
        var chunks = Enumerable.Range(0, 12)
            .Select(index => Enumerable.Repeat((byte)(index + 1), index + 3).ToArray())
            .ToArray();
        var file = chunks.SelectMany(static value => value).ToArray();
        Guid uploadId;

        await using (var scope = firstProvider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IChunkUploadService>();
            var started = await service.StartAsync(new("shared-name.bin", file.Length, chunks.Length, Hash(file)));
            Assert.True(started.IsSuccess);
            uploadId = started.Value!.UploadId;

            for (var index = 0; index < chunks.Length; index++)
            {
                var chunk = chunks[index];
                var uploaded = await service.UploadChunkAsync(
                    uploadId,
                    index,
                    new MemoryStream(chunk),
                    chunk.Length,
                    Hash(chunk));
                Assert.True(uploaded.IsSuccess);
            }
        }

        UploadResult<UploadedFileDescriptor> completed;
        await using (var scope = secondProvider.CreateAsyncScope())
        {
            completed = await scope.ServiceProvider.GetRequiredService<IChunkUploadService>().CompleteAsync(uploadId);
        }

        Assert.True(completed.IsSuccess);
        var path = Path.Combine(_root, completed.Value!.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(file, await File.ReadAllBytesAsync(path));
        Assert.Equal(Hash(file), completed.Value.Sha256);
    }

    [Fact]
    public async Task Maintenance_CleansExpiredArtifactsAndLaterPurgesMetadata()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var provider = CreateProvider(
            timeProvider,
            addMaintenanceWorker: false,
            options =>
            {
                options.IncompleteUploadRetention = TimeSpan.FromHours(1);
                options.ExpiredSessionMetadataRetention = TimeSpan.FromDays(1);
            });
        await EnsureDatabaseAsync(provider);
        var bytes = "abc"u8.ToArray();
        var hash = Hash(bytes);
        Guid uploadId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IChunkUploadService>();
            var started = await service.StartAsync(new("expired.bin", bytes.Length, 1, hash));
            uploadId = started.Value!.UploadId;
            await service.UploadChunkAsync(uploadId, 0, new MemoryStream(bytes), bytes.Length, hash);
        }

        timeProvider.Advance(TimeSpan.FromHours(2));
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUploadMaintenanceService>().RunOnceAsync(100);
            var status = await scope.ServiceProvider.GetRequiredService<IChunkUploadService>().GetStatusAsync(uploadId);
            Assert.True(status.IsSuccess);
            Assert.Equal(UploadState.Cancelled, status.Value!.State);
        }

        var chunksDirectory = Path.Combine(_root, "chunks", uploadId.ToString("N"));
        Assert.False(Directory.Exists(chunksDirectory));

        timeProvider.Advance(TimeSpan.FromDays(2));
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUploadMaintenanceService>().RunOnceAsync(100);
            var status = await scope.ServiceProvider.GetRequiredService<IChunkUploadService>().GetStatusAsync(uploadId);
            Assert.False(status.IsSuccess);
            Assert.Equal(UploadErrorCode.NotFound, status.Error?.Code);
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private ServiceProvider CreateProvider(
        TimeProvider? timeProvider = null,
        bool addMaintenanceWorker = true,
        Action<UploadOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        var uploadBuilder = services.AddEasyChunkUpload(configure)
            .UseSharedFileSystem(options => options.RootPath = _root)
            .UseEntityFrameworkStore(options => options.UseSqlite($"Data Source={_databasePath}"));
        if (addMaintenanceWorker)
        {
            uploadBuilder.AddUploadMaintenanceWorker();
        }

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static async Task EnsureDatabaseAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
