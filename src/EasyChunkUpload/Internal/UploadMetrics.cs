using System.Diagnostics.Metrics;

namespace EasyChunkUpload.Internal;

internal static class UploadMetrics
{
    private static readonly Meter Meter = new("EasyChunkUpload", "2.0.0");

    public static readonly Counter<long> UploadsStarted = Meter.CreateCounter<long>("easy_chunk_upload.uploads.started");
    public static readonly Counter<long> ChunksStored = Meter.CreateCounter<long>("easy_chunk_upload.chunks.stored");
    public static readonly Counter<long> BytesStored = Meter.CreateCounter<long>("easy_chunk_upload.bytes.stored", "bytes");
    public static readonly Counter<long> UploadsCompleted = Meter.CreateCounter<long>("easy_chunk_upload.uploads.completed");
    public static readonly Counter<long> UploadsFailed = Meter.CreateCounter<long>("easy_chunk_upload.uploads.failed");
    public static readonly Histogram<double> CompletionDuration = Meter.CreateHistogram<double>("easy_chunk_upload.completion.duration", "ms");
}
