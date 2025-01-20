using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Services.ChunkUpload;
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

        
        return services;

    }

}