using EasyChunkUpload.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EasyChunkUpload.ConsumerTests;

public sealed class ConsumerCompatibilityTests
{
    [Fact]
    public void CoreRegistration_IsAvailableFromSupportedTargetFrameworks()
    {
        IServiceCollection services = new ServiceCollection();

        var builder = services.AddEasyChunkUpload();

        Assert.Same(services, builder.Services);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IChunkUploadService));
    }
}
