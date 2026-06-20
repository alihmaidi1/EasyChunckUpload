namespace EasyChunkUpload.Hosting;

public sealed class UploadMaintenanceOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);

    public int BatchSize { get; set; } = 100;
}
