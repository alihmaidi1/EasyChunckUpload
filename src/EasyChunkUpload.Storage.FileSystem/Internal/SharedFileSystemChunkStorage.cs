using System.Buffers;
using System.Security.Cryptography;
using EasyChunkUpload.Abstractions;
using Microsoft.Extensions.Options;

namespace EasyChunkUpload.Storage.FileSystem.Internal;

internal sealed class SharedFileSystemChunkStorage : IChunkStorage
{
    private readonly string _rootPath;
    private readonly int _bufferSize;

    public SharedFileSystemChunkStorage(IOptions<FileSystemStorageOptions> options)
    {
        _rootPath = Path.GetFullPath(options.Value.RootPath);
        _bufferSize = options.Value.BufferSize;
        Directory.CreateDirectory(GetChunksRoot());
        Directory.CreateDirectory(GetCompletedRoot());
    }

    public async Task<ChunkStorageWriteResult> WriteChunkAsync(
        Guid uploadId,
        int chunkIndex,
        Stream content,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        var finalPath = GetChunkPath(uploadId, chunkIndex);
        if (File.Exists(finalPath))
        {
            var existing = await InspectFileAsync(finalPath, cancellationToken);
            return existing.Length == expectedLength && existing.Sha256 == expectedSha256
                ? new(ChunkStorageWriteOutcome.ExistingMatches, existing.Length, existing.Sha256)
                : new(ChunkStorageWriteOutcome.Conflict, existing.Length, existing.Sha256);
        }

        var directory = Path.GetDirectoryName(finalPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{chunkIndex:D8}.{Guid.NewGuid():N}.part");

        try
        {
            var written = await WriteAndHashAsync(content, temporaryPath, expectedLength, cancellationToken);
            if (written.Length != expectedLength)
            {
                return new(ChunkStorageWriteOutcome.SizeMismatch, written.Length, written.Sha256);
            }

            if (written.Sha256 != expectedSha256)
            {
                return new(ChunkStorageWriteOutcome.HashMismatch, written.Length, written.Sha256);
            }

            try
            {
                File.Move(temporaryPath, finalPath, overwrite: false);
                return new(ChunkStorageWriteOutcome.Created, written.Length, written.Sha256);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                var existing = await InspectFileAsync(finalPath, cancellationToken);
                return existing.Length == expectedLength && existing.Sha256 == expectedSha256
                    ? new(ChunkStorageWriteOutcome.ExistingMatches, existing.Length, existing.Sha256)
                    : new(ChunkStorageWriteOutcome.Conflict, existing.Length, existing.Sha256);
            }
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    public Task DeleteChunkAsync(Guid uploadId, int chunkIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryDeleteFile(GetChunkPath(uploadId, chunkIndex));
        return Task.CompletedTask;
    }

    public async Task<StorageObjectDescriptor> AssembleAsync(
        UploadSessionRecord session,
        IReadOnlyList<UploadChunkRecord> chunks,
        CancellationToken cancellationToken)
    {
        var completedPath = GetCompletedPath(session.Id);
        var storageKey = GetStorageKey(session.Id);
        if (File.Exists(completedPath))
        {
            var existing = await InspectFileAsync(completedPath, cancellationToken);
            return new(storageKey, existing.Length, existing.Sha256);
        }

        var directory = Path.GetDirectoryName(completedPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.part");

        try
        {
            var assembled = await AssembleAndHashAsync(session.Id, chunks, temporaryPath, cancellationToken);
            try
            {
                File.Move(temporaryPath, completedPath, overwrite: false);
                return new(storageKey, assembled.Length, assembled.Sha256);
            }
            catch (IOException) when (File.Exists(completedPath))
            {
                var existing = await InspectFileAsync(completedPath, cancellationToken);
                return new(storageKey, existing.Length, existing.Sha256);
            }
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    public Task DeleteCompletedFileAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetCompletedPath(uploadId);
        TryDeleteFile(path);
        TryDeleteEmptyDirectory(Path.GetDirectoryName(path)!);
        return Task.CompletedTask;
    }

    public Task DeleteUploadArtifactsAsync(
        Guid uploadId,
        bool includeCompletedFile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var chunksDirectory = GetChunkDirectory(uploadId);
        if (Directory.Exists(chunksDirectory))
        {
            Directory.Delete(chunksDirectory, recursive: true);
        }

        if (includeCompletedFile)
        {
            var completedPath = GetCompletedPath(uploadId);
            TryDeleteFile(completedPath);
            TryDeleteEmptyDirectory(Path.GetDirectoryName(completedPath)!);
        }

        return Task.CompletedTask;
    }

    private async Task<(long Length, string Sha256)> WriteAndHashAsync(
        Stream content,
        string destinationPath,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            _bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
        long length = 0;
        try
        {
            while (true)
            {
                var read = await content.ReadAsync(buffer.AsMemory(0, _bufferSize), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                length += read;
                if (length > maximumLength)
                {
                    return (length, string.Empty);
                }

                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            await destination.FlushAsync(cancellationToken);
            return (length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<(long Length, string Sha256)> AssembleAndHashAsync(
        Guid uploadId,
        IReadOnlyList<UploadChunkRecord> chunks,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            _bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
        long length = 0;
        try
        {
            foreach (var chunk in chunks.OrderBy(static value => value.ChunkIndex))
            {
                await using var source = new FileStream(
                    GetChunkPath(uploadId, chunk.ChunkIndex),
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    _bufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                while (true)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(0, _bufferSize), cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    length += read;
                    hash.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            await destination.FlushAsync(cancellationToken);
            return (length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task<(long Length, string Sha256)> InspectFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            _bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return (stream.Length, Convert.ToHexString(hash).ToLowerInvariant());
    }

    private string GetChunksRoot() => Path.Combine(_rootPath, "chunks");

    private string GetCompletedRoot() => Path.Combine(_rootPath, "completed");

    private string GetChunkDirectory(Guid uploadId) => Path.Combine(GetChunksRoot(), uploadId.ToString("N"));

    private string GetChunkPath(Guid uploadId, int chunkIndex) =>
        Path.Combine(GetChunkDirectory(uploadId), $"{chunkIndex:D8}.chunk");

    private string GetCompletedPath(Guid uploadId) =>
        Path.Combine(GetCompletedRoot(), uploadId.ToString("N"), "content");

    private static string GetStorageKey(Guid uploadId) => $"completed/{uploadId:N}/content";

    private static void TryDeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
        }
    }
}
