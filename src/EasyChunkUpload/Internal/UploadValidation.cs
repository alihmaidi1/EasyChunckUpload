using System.Globalization;
using EasyChunkUpload.Abstractions;

namespace EasyChunkUpload.Internal;

internal static class UploadValidation
{
    public static UploadError? ValidateStart(StartUploadRequest request, UploadOptions options)
    {
        if (request is null)
        {
            return new(UploadErrorCode.InvalidRequest, "The upload request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName) ||
            request.FileName.Length > 255 ||
            request.FileName != Path.GetFileName(request.FileName) ||
            request.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return new(UploadErrorCode.InvalidRequest, "The file name is invalid.");
        }

        if (request.ContentLength <= 0 || request.ContentLength > options.MaxFileSize)
        {
            return new(UploadErrorCode.InvalidRequest, "The file size is outside the configured limits.");
        }

        if (request.TotalChunks <= 0 || request.TotalChunks > options.MaxChunkCount)
        {
            return new(UploadErrorCode.InvalidRequest, "The chunk count is outside the configured limits.");
        }

        if (!IsSha256(request.Sha256))
        {
            return new(UploadErrorCode.InvalidRequest, "SHA-256 must contain exactly 64 hexadecimal characters.");
        }

        return null;
    }

    public static UploadError? ValidateChunk(
        UploadSessionRecord session,
        int chunkIndex,
        Stream content,
        long contentLength,
        string sha256,
        UploadOptions options)
    {
        if (session.State is not (UploadState.Created or UploadState.Uploading))
        {
            return new(UploadErrorCode.InvalidState, $"Chunks cannot be uploaded while the session is {session.State}.");
        }

        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
        {
            return new(UploadErrorCode.InvalidRequest, "The chunk index is outside the upload range.");
        }

        if (content is null || !content.CanRead)
        {
            return new(UploadErrorCode.InvalidRequest, "The chunk stream must be readable.");
        }

        if (contentLength <= 0 || contentLength > options.MaxChunkSize || contentLength > session.ContentLength)
        {
            return new(UploadErrorCode.InvalidRequest, "The chunk size is outside the configured limits.");
        }

        if (!IsSha256(sha256))
        {
            return new(UploadErrorCode.InvalidRequest, "SHA-256 must contain exactly 64 hexadecimal characters.");
        }

        return null;
    }

    public static bool IsSha256(string value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        return value.All(static character => char.IsAsciiHexDigit(character));
    }

    public static string NormalizeSha256(string value) => value.ToLower(CultureInfo.InvariantCulture);
}
