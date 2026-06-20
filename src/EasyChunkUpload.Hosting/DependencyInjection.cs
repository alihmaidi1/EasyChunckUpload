using EasyChunkUpload.Abstractions;
using EasyChunkUpload.Hosting.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace EasyChunkUpload.Hosting;

public static class DependencyInjection
{
    public static IEasyChunkUploadBuilder AddUploadMaintenanceWorker(
        this IEasyChunkUploadBuilder builder,
        Action<UploadMaintenanceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Services.AddOptions<UploadMaintenanceOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        options
            .Validate(static value => value.Interval > TimeSpan.Zero, "Interval must be positive.")
            .Validate(static value => value.BatchSize > 0, "BatchSize must be positive.")
            .ValidateOnStart();

        builder.Services.AddHostedService<UploadMaintenanceWorker>();
        return builder;
    }
}
