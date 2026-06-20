using System.Runtime.ExceptionServices;
using EasyChunkUpload.Abstractions;

namespace EasyChunkUpload.Internal;

internal sealed class UploadLeaseHeartbeat
{
    private readonly CancellationTokenSource _operationCancellation;
    private readonly CancellationTokenSource _heartbeatCancellation;
    private readonly Task _heartbeatTask;
    private ExceptionDispatchInfo? _failure;

    public UploadLeaseHeartbeat(
        IUploadCompletionCoordinator coordinator,
        TimeProvider timeProvider,
        Guid uploadId,
        UploadLeasePurpose purpose,
        string owner,
        TimeSpan interval,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _heartbeatTask = RunAsync(
            coordinator,
            timeProvider,
            uploadId,
            purpose,
            owner,
            interval,
            duration,
            _heartbeatCancellation.Token);
    }

    public CancellationToken OperationToken => _operationCancellation.Token;

    public bool LeaseLost { get; private set; }

    public async Task StopAsync()
    {
        await _heartbeatCancellation.CancelAsync();
        await _heartbeatTask;
        _heartbeatCancellation.Dispose();
        _operationCancellation.Dispose();
    }

    public void ThrowIfFailed() => _failure?.Throw();

    private async Task RunAsync(
        IUploadCompletionCoordinator coordinator,
        TimeProvider timeProvider,
        Guid uploadId,
        UploadLeasePurpose purpose,
        string owner,
        TimeSpan interval,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(interval, timeProvider, cancellationToken);
                var renewed = await coordinator.TryRenewAsync(
                    uploadId,
                    purpose,
                    owner,
                    timeProvider.GetUtcNow(),
                    duration,
                    cancellationToken);
                if (renewed)
                {
                    UploadMetrics.LeasesRenewed.Add(1);
                    continue;
                }

                LeaseLost = true;
                UploadMetrics.LeasesLost.Add(1);
                await _operationCancellation.CancelAsync();
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _failure = ExceptionDispatchInfo.Capture(exception);
            await _operationCancellation.CancelAsync();
        }
    }
}
