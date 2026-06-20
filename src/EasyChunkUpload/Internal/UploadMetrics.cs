using System.Diagnostics.Metrics;

namespace EasyChunkUpload.Internal;

internal static class UploadMetrics
{
    private static readonly Meter Meter = new(
        "EasyChunkUpload",
        typeof(UploadMetrics).Assembly.GetName().Version?.ToString());

    public static readonly Counter<long> UploadsStarted = Meter.CreateCounter<long>("easy_chunk_upload.uploads.started");
    public static readonly Counter<long> ChunksStored = Meter.CreateCounter<long>("easy_chunk_upload.chunks.stored");
    public static readonly Counter<long> BytesStored = Meter.CreateCounter<long>("easy_chunk_upload.bytes.stored", "bytes");
    public static readonly Counter<long> UploadsCompleted = Meter.CreateCounter<long>("easy_chunk_upload.uploads.completed");
    public static readonly Counter<long> UploadsFailed = Meter.CreateCounter<long>("easy_chunk_upload.uploads.failed");
    public static readonly Histogram<double> CompletionDuration = Meter.CreateHistogram<double>("easy_chunk_upload.completion.duration", "ms");
    public static readonly Counter<long> LeasesRenewed = Meter.CreateCounter<long>("easy_chunk_upload.leases.renewed");
    public static readonly Counter<long> LeasesLost = Meter.CreateCounter<long>("easy_chunk_upload.leases.lost");
    public static readonly Counter<long> CompletionLeasesRecovered = Meter.CreateCounter<long>("easy_chunk_upload.leases.completion_recovered");
    public static readonly Counter<long> UploadArtifactsCleaned = Meter.CreateCounter<long>("easy_chunk_upload.maintenance.artifacts_cleaned");
    public static readonly Counter<long> UploadMetadataPurged = Meter.CreateCounter<long>("easy_chunk_upload.maintenance.metadata_purged");
    public static readonly Histogram<double> MaintenanceDuration = Meter.CreateHistogram<double>("easy_chunk_upload.maintenance.duration", "ms");
}
