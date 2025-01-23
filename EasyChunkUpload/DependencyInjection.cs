using System.Data.Common;
using System.Reflection;
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUpload.Services.Cleanup;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUpload.Services.FileService;
using Microsoft.Extensions.DependencyInjection;
namespace EasyChunkUpload;

public static class DependencyInjection
{

    /// <summary>
    /// Extension methods to add and configure the Easy Chunk Upload service.
    /// </summary>
    public static IServiceCollection AddEasyChunkUploadConfiguration(this IServiceCollection services,ChunkUploadSettings chunkUploadSettings)
    {
        
        services.Configure<ChunkUploadSettings>(options=>{

            options.CleanupInterval=chunkUploadSettings.CleanupInterval;
            options.CompletedFilesExpiration=chunkUploadSettings.CompletedFilesExpiration;
            options.TempFolder=chunkUploadSettings.TempFolder;


        });

        if(!Directory.Exists(chunkUploadSettings.TempFolder))   {

            Directory.CreateDirectory(chunkUploadSettings.TempFolder);

        } 
        services.AddSingleton<IFileHelper,FileHelper>();
        services.AddSingleton<IChunkUpload,ChunkUpload>();
        services.AddSingleton<IFileService,FileService>();
        services.AddSingleton<ICleanupService,CleanupService>();
        services.AddHostedService<BackgroundCleanupHostedService>();
        return services;

    }

}
