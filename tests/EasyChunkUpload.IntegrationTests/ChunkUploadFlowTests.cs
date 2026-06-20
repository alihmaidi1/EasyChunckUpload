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

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEasyChunkUpload()
            .UseSharedFileSystem(options => options.RootPath = _root)
            .UseEntityFrameworkStore(options => options.UseSqlite($"Data Source={_databasePath}"))
            .AddUploadMaintenanceWorker();
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
}
