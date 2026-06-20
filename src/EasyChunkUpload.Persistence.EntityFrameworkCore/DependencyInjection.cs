using EasyChunkUpload.Abstractions;
using EasyChunkUpload.Persistence.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyChunkUpload.Persistence.EntityFrameworkCore;

public static class DependencyInjection
{
    public static IEasyChunkUploadBuilder UseEntityFrameworkStore(
        this IEasyChunkUploadBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddDbContext<UploadDbContext>(configure);
        builder.Services.TryAddScoped<IUploadSessionStore, EntityFrameworkUploadSessionStore>();
        builder.Services.TryAddScoped<IUploadCompletionCoordinator, EntityFrameworkUploadCompletionCoordinator>();
        return builder;
    }
}
