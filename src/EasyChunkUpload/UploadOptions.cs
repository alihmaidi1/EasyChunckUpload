namespace EasyChunkUpload;

public sealed class UploadOptions
{
    public long MaxFileSize { get; set; } = 10L * 1024 * 1024 * 1024;

    public long MaxChunkSize { get; set; } = 64L * 1024 * 1024;

    public int MaxChunkCount { get; set; } = 10_000;

    public TimeSpan IncompleteUploadRetention { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan CompletionLeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan CleanupLeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan ExpiredSessionMetadataRetention { get; set; } = TimeSpan.FromDays(30);
}
