using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUpload.Services.Cleanup;
using EasyChunkUpload.Services.FileHelper;
using Microsoft.Extensions.DependencyInjection;
namespace EasyChunkUpload;

public static class DependencyInjection
{

    /// <summary>
    /// Extension methods to add and configure the Easy Chunk Upload service.
    /// </summary>
    public static IServiceCollection AddEasyChunkUploadConfiguration(this IServiceCollection services,Action<ChunkUploadSettings> chunkUploadSettings)
    {

        services.Configure(chunkUploadSettings);        
        services.AddSingleton<IFileHelper,FileHelper>();
        services.AddSingleton<IChunkUpload,ChunkUpload>();
        services.AddSingleton<ICleanupService,CleanupService>();
        services.AddHostedService<BackgroundCleanupHostedService>();
        return services;

    }

}
