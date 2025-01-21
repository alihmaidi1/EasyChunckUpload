using EasyChunkUpload.ChunkExtension;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
namespace EasyChunkUpload.Services.Cleanup;

public class BackgroundCleanupHostedService: BackgroundService
{


    private readonly ICleanupService cleanupService;

    private int IntervalTime{get;set;}


    public BackgroundCleanupHostedService(ICleanupService cleanupService,IOptions<ChunkUploadSettings> options){

        this.cleanupService=cleanupService;
        IntervalTime=options.Value.CleanupInterval;

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(this.IntervalTime));

        while(!stoppingToken.IsCancellationRequested&&await timer.WaitForNextTickAsync(stoppingToken)){

            await cleanupService.CleanUpExpiredUploadsAsync();

        }



    }
}
