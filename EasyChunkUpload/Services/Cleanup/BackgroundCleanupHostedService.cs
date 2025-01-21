using Microsoft.Extensions.Hosting;
namespace EasyChunkUpload.Services.Cleanup;

public class BackgroundCleanupHostedService: IHostedService
{
    private Timer _timer;









    public Task StartAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
