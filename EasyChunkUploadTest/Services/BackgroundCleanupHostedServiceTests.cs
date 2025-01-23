using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Services.Cleanup;
using Microsoft.Extensions.Options;
using Moq;

namespace EasyChunkUploadTest.Services;
public class BackgroundCleanupHostedServiceTests
{

    private readonly Mock<ICleanupService> _mockCleanupService;
    private readonly IOptions<ChunkUploadSettings> _options;
    private readonly BackgroundCleanupHostedService _service;

    public BackgroundCleanupHostedServiceTests()
    {
        _mockCleanupService = new Mock<ICleanupService>();
        _options = Options.Create(new ChunkUploadSettings { CleanupInterval = 1 });
        _service = new BackgroundCleanupHostedService(_mockCleanupService.Object,_options);
    }


    [Fact]
    public async Task Should_CallCleanup_OnTimerInterval()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var callCount = 0;        
        _mockCleanupService.Setup(x => x.CleanUpExpiredUploadsAsync())            
            .Callback(() => callCount++)            
            .Returns(Task.CompletedTask);

        // Act
        var task = _service.StartAsync(cts.Token);
        await Task.Delay(4000); 
        await cts.CancelAsync();
        await task;

        // Assert
        _mockCleanupService.Verify(x => 
            x.CleanUpExpiredUploadsAsync(), 
            Times.Exactly(4)
        );
    }

    [Fact]
    public async Task Should_StopGracefully_OnCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(500); 

        // Act
        await _service.StartAsync(cts.Token);
        await Task.Delay(500);
        // Assert
        Assert.True(cts.IsCancellationRequested);


    }



}
