using EasyChunkUpload.Abstractions;
using EasyChunkUpload.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyChunkUpload;

public static class DependencyInjection
{
    public static IEasyChunkUploadBuilder AddEasyChunkUpload(
        this IServiceCollection services,
        Action<UploadOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<UploadOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options
            .Validate(static value => value.MaxFileSize > 0, "MaxFileSize must be positive.")
            .Validate(static value => value.MaxChunkSize > 0, "MaxChunkSize must be positive.")
            .Validate(static value => value.MaxChunkSize <= value.MaxFileSize, "MaxChunkSize cannot exceed MaxFileSize.")
            .Validate(static value => value.MaxChunkCount > 0, "MaxChunkCount must be positive.")
            .Validate(static value => value.IncompleteUploadRetention > TimeSpan.Zero, "IncompleteUploadRetention must be positive.")
            .Validate(static value => value.CompletionLeaseDuration > TimeSpan.Zero, "CompletionLeaseDuration must be positive.")
            .Validate(static value => value.CleanupLeaseDuration > TimeSpan.Zero, "CleanupLeaseDuration must be positive.")
            .Validate(static value => value.LeaseRenewalInterval > TimeSpan.Zero, "LeaseRenewalInterval must be positive.")
            .Validate(
                static value => value.LeaseRenewalInterval < value.CompletionLeaseDuration,
                "LeaseRenewalInterval must be shorter than CompletionLeaseDuration.")
            .Validate(
                static value => value.LeaseRenewalInterval < value.CleanupLeaseDuration,
                "LeaseRenewalInterval must be shorter than CleanupLeaseDuration.")
            .Validate(
                static value => value.ExpiredSessionMetadataRetention > TimeSpan.Zero,
                "ExpiredSessionMetadataRetention must be positive.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IChunkUploadService, ChunkUploadService>();
        services.TryAddScoped<IUploadMaintenanceService, UploadMaintenanceService>();

        return new EasyChunkUploadBuilder(services);
    }
}

internal sealed class EasyChunkUploadBuilder(IServiceCollection services) : IEasyChunkUploadBuilder
{
    public IServiceCollection Services { get; } = services;
}
