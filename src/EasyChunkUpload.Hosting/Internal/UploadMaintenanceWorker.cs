using EasyChunkUpload.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyChunkUpload.Hosting.Internal;

internal sealed class UploadMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<UploadMaintenanceOptions> options,
    TimeProvider timeProvider,
    ILogger<UploadMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunMaintenanceAsync(stoppingToken);

        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunMaintenanceAsync(stoppingToken);
        }
    }

    private async Task RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var maintenance = scope.ServiceProvider.GetRequiredService<IUploadMaintenanceService>();
            await maintenance.RunOnceAsync(options.Value.BatchSize, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "EasyChunkUpload maintenance cycle failed.");
        }
    }
}
