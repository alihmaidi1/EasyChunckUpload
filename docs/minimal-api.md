# Minimal API integration

EasyChunkUpload intentionally does not register HTTP endpoints. This keeps routing, authentication, authorization, quotas, and ownership rules in the consuming application.

The following example shows a small transport layer around `IChunkUploadService`. Adapt request contracts and authorization to your application instead of treating these routes as a drop-in security model.

## Endpoint example

```csharp
using EasyChunkUpload.Abstractions;

var uploads = app.MapGroup("/uploads")
    .RequireAuthorization();

uploads.MapPost("/", async (
    StartUploadRequest request,
    IChunkUploadService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.StartAsync(request, cancellationToken);
    return ToHttpResult(result);
});

uploads.MapPut("/{uploadId:guid}/chunks/{chunkIndex:int}", async (
    Guid uploadId,
    int chunkIndex,
    long contentLength,
    string sha256,
    HttpRequest request,
    IChunkUploadService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.UploadChunkAsync(
        uploadId,
        chunkIndex,
        request.Body,
        contentLength,
        sha256,
        cancellationToken);

    return ToHttpResult(result);
});

uploads.MapGet("/{uploadId:guid}", async (
    Guid uploadId,
    IChunkUploadService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetStatusAsync(uploadId, cancellationToken);
    return ToHttpResult(result);
});

uploads.MapGet("/{uploadId:guid}/missing", async (
    Guid uploadId,
    IChunkUploadService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetMissingChunksAsync(uploadId, cancellationToken);
    return ToHttpResult(result);
});

uploads.MapPost("/{uploadId:guid}/complete", async (
    Guid uploadId,
    IChunkUploadService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CompleteAsync(uploadId, cancellationToken);
    return ToHttpResult(result);
});

uploads.MapDelete("/{uploadId:guid}", async (
    Guid uploadId,
    IChunkUploadService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CancelAsync(uploadId, cancellationToken);
    return result.IsSuccess
        ? Results.NoContent()
        : ToHttpError(result.Error!);
});

static IResult ToHttpResult<T>(UploadResult<T> result) =>
    result.IsSuccess
        ? Results.Ok(result.Value)
        : ToHttpError(result.Error!);

static IResult ToHttpError(UploadError error) => error.Code switch
{
    UploadErrorCode.InvalidRequest or
    UploadErrorCode.HashMismatch or
    UploadErrorCode.SizeMismatch => Results.BadRequest(error),

    UploadErrorCode.NotFound => Results.NotFound(error),

    UploadErrorCode.InvalidState or
    UploadErrorCode.ChunkConflict or
    UploadErrorCode.IncompleteUpload or
    UploadErrorCode.LeaseUnavailable => Results.Conflict(error),

    _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
};
```

The chunk endpoint accepts `contentLength` and `sha256` as query parameters in this example:

```http
PUT /uploads/{uploadId}/chunks/0?contentLength=67108864&sha256=<64-hex-characters>
Content-Type: application/octet-stream
```

Headers are also a reasonable transport choice. Whichever contract you choose, pass the declared values unchanged to `UploadChunkAsync`; the storage adapter verifies them while streaming.

## Authorization checklist

Before calling the service, verify that the authenticated principal may operate on the supplied upload ID. The package does not associate sessions with users or tenants.

Recommended application metadata:

- Upload ID
- Owner user or tenant ID
- Allowed purpose or destination
- Original content type
- Creation and business-expiration timestamps

Store this application-owned authorization metadata separately or extend your persistence boundary deliberately. Do not infer ownership from a filename or storage key.

## HTTP limits

Align server and proxy limits with `UploadOptions.MaxChunkSize`:

- Kestrel or endpoint request-body limits
- Reverse-proxy maximum body size
- Request timeout and minimum data-rate policies
- Rate limiting per user, tenant, and upload
- Maximum concurrent chunk requests

Reject oversized requests before invoking the package when possible. Package validation remains the final integrity boundary, not the only network-abuse control.

## Client retry behavior

- Retry transient network and server failures using the same index, length, and SHA-256.
- A matching retry succeeds with `WasAlreadyUploaded = true`.
- Do not retry `ChunkConflict` without correcting the client-side chunk plan.
- On `LeaseUnavailable`, poll status and retry completion with backoff.
- Use `GetMissingChunksAsync` after reconnecting instead of assuming the last acknowledged index.
- Treat `HashMismatch` and `SizeMismatch` as client-data errors.

Completion is idempotent after the session reaches `Completed`; repeated calls return the stored descriptor.

## Completed files

The completion response contains an opaque `StorageKey`. Keep filesystem path resolution inside trusted application infrastructure. Do not concatenate user input into storage paths or return server-local paths to clients.

Common post-completion steps include malware scanning, business metadata persistence, object promotion, and retention scheduling. EasyChunkUpload does not delete completed files automatically.
