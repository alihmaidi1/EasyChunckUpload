using EasyChunkUpload.Abstractions;
using EasyChunkUpload.Storage.FileSystem.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyChunkUpload.Storage.FileSystem;

public static class DependencyInjection
{
    public static IEasyChunkUploadBuilder UseSharedFileSystem(
        this IEasyChunkUploadBuilder builder,
        Action<FileSystemStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services
            .AddOptions<FileSystemStorageOptions>()
            .Configure(configure)
            .Validate(static options => !string.IsNullOrWhiteSpace(options.RootPath), "RootPath is required.")
            .Validate(static options => Path.IsPathFullyQualified(options.RootPath), "RootPath must be an absolute path.")
            .Validate(static options => options.BufferSize >= 4096, "BufferSize must be at least 4096 bytes.")
            .ValidateOnStart();

        builder.Services.TryAddSingleton<IChunkStorage, SharedFileSystemChunkStorage>();
        return builder;
    }
}
