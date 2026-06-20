using System.Security.Cryptography;
using EasyChunkUpload.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EasyChunkUpload.Storage.FileSystem.Tests;

public sealed class SharedFileSystemChunkStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "easy-chunk-upload-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AssembleAsync_MergesMoreThanTenChunksInNumericOrder()
    {
        await using var provider = CreateProvider();
        var storage = provider.GetRequiredService<IChunkStorage>();
        var uploadId = Guid.NewGuid();
        var chunkBytes = Enumerable.Range(0, 12)
            .Select(index => Enumerable.Repeat((byte)index, index + 1).ToArray())
            .ToArray();
        var allBytes = chunkBytes.SelectMany(static value => value).ToArray();
        var records = new List<UploadChunkRecord>();

        for (var index = 0; index < chunkBytes.Length; index++)
        {
            var bytes = chunkBytes[index];
            var hash = Hash(bytes);
            await using var stream = new NonSeekableReadStream(bytes);
            var result = await storage.WriteChunkAsync(uploadId, index, stream, bytes.Length, hash, CancellationToken.None);
            Assert.Equal(ChunkStorageWriteOutcome.Created, result.Outcome);
            records.Add(new(uploadId, index, bytes.Length, hash, DateTimeOffset.UtcNow));
        }

        var session = new UploadSessionRecord(
            uploadId,
            "same-name.bin",
            allBytes.Length,
            records.Count,
            Hash(allBytes),
            UploadState.Completing,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            null);

        var completed = await storage.AssembleAsync(session, records, CancellationToken.None);
        var completedPath = Path.Combine(_root, completed.StorageKey.Replace('/', Path.DirectorySeparatorChar));

        Assert.Equal(allBytes, await File.ReadAllBytesAsync(completedPath));
        Assert.Equal(Hash(allBytes), completed.Sha256);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddEasyChunkUpload().UseSharedFileSystem(options => options.RootPath = _root);
        return services.BuildServiceProvider();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class NonSeekableReadStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
