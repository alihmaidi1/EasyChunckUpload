# EasyChunkUpload

[![CI](https://github.com/alihmaidi1/EasyChunckUpload/actions/workflows/ci.yml/badge.svg)](https://github.com/alihmaidi1/EasyChunckUpload/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EasyChunkUpload.svg)](https://www.nuget.org/packages/EasyChunkUpload)
[![NuGet Downloads](https://img.shields.io/nuget/dt/EasyChunkUpload.svg)](https://www.nuget.org/packages/EasyChunkUpload)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/LICENSE)

EasyChunkUpload is a provider-neutral .NET library for resumable, integrity-checked chunk uploads. It coordinates upload state, verifies SHA-256 hashes, and supports safe multi-instance completion without exposing HTTP endpoints or machine-local paths.

## Why EasyChunkUpload?

- Stream large files without buffering them entirely in memory.
- Retry a chunk safely: matching content is idempotent, conflicting content is rejected.
- Verify every chunk and the completed file with SHA-256.
- Coordinate completion and cleanup across multiple application instances.
- Replace persistence or storage through explicit abstractions.
- Keep authentication, authorization, HTTP design, and completed-file retention under application control.

## Requirements

- .NET 8 or later
- A relational database supported by your chosen EF Core provider
- An absolute filesystem path
- For multi-instance deployments: one shared database and one shared filesystem mounted by every instance

## Install

```bash
dotnet add package EasyChunkUpload --version 2.0.1
dotnet add package EasyChunkUpload.Storage.FileSystem --version 2.0.1
dotnet add package EasyChunkUpload.Persistence.EntityFrameworkCore --version 2.0.1
dotnet add package EasyChunkUpload.Hosting --version 2.0.1
```

`EasyChunkUpload.Abstractions` is referenced transitively. Install it directly only when building a custom storage or persistence adapter.

Install the EF Core provider in the consuming application, for example:

```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

## Quick start

### 1. Register the services

```csharp
using EasyChunkUpload;
using EasyChunkUpload.Hosting;
using EasyChunkUpload.Persistence.EntityFrameworkCore;
using EasyChunkUpload.Storage.FileSystem;
using Microsoft.EntityFrameworkCore;

builder.Services
    .AddEasyChunkUpload()
    .UseSharedFileSystem(options =>
    {
        options.RootPath = builder.Configuration["ChunkUpload:RootPath"]!;
    })
    .UseEntityFrameworkStore(options =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("ChunkUploads"));
    })
    .AddUploadMaintenanceWorker();
```

All options are validated when the application starts. `RootPath` must be absolute.

### 2. Create the database migration

The package exposes `UploadDbContext`; the consuming application owns the EF provider, migrations, and database deployment.

```bash
dotnet ef migrations add EasyChunkUploadV2 \
  --context UploadDbContext \
  --output-dir Data/Migrations/EasyChunkUpload

dotnet ef database update --context UploadDbContext
```

If migrations live in a separate project, pass the appropriate `--project` and `--startup-project` values.

### 3. Upload and complete a file

Chunk indexes are zero-based. SHA-256 values are required and must contain exactly 64 hexadecimal characters.

```csharp
using EasyChunkUpload.Abstractions;

var started = await uploadService.StartAsync(
    new StartUploadRequest(
        FileName: "archive.zip",
        ContentLength: fileLength,
        TotalChunks: totalChunks,
        Sha256: fileSha256),
    cancellationToken);

if (!started.IsSuccess)
{
    // Map started.Error.Code to your HTTP or application error model.
    return;
}

var uploadId = started.Value!.UploadId;

for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
{
    await using var chunkStream = OpenChunk(chunkIndex);
    var uploaded = await uploadService.UploadChunkAsync(
        uploadId,
        chunkIndex,
        chunkStream,
        chunkLengths[chunkIndex],
        chunkSha256Values[chunkIndex],
        cancellationToken);

    if (!uploaded.IsSuccess)
    {
        // Retry expected transient failures. Do not retry ChunkConflict unchanged.
        return;
    }
}

var completed = await uploadService.CompleteAsync(uploadId, cancellationToken);
if (completed.IsSuccess)
{
    var file = completed.Value!;
    Console.WriteLine($"Stored as {file.StorageKey}");
}
```

`StorageKey` is an opaque application-facing key, not a local filesystem path. The original filename is metadata and never determines the physical storage location.

For HTTP endpoints and result mapping, see [Minimal API integration](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/docs/minimal-api.md).

## Public workflow

| Operation | Purpose |
|---|---|
| `StartAsync` | Validate metadata and create an upload session. |
| `UploadChunkAsync` | Stream and verify one zero-based chunk. |
| `GetStatusAsync` | Read state, uploaded chunk count, and uploaded bytes. |
| `GetMissingChunksAsync` | Return the zero-based indexes still required. |
| `CompleteAsync` | Acquire a lease, assemble, verify, and publish the file. |
| `CancelAsync` | Cancel an incomplete upload and remove its temporary artifacts. |

## Error model

Expected failures return `UploadResult` or `UploadResult<T>`; unexpected infrastructure or programming failures throw exceptions.

| Error code | Typical meaning |
|---|---|
| `InvalidRequest` | Invalid filename, size, chunk count, index, stream, or SHA-256. |
| `NotFound` | The upload session does not exist. |
| `InvalidState` | The operation is not allowed in the current upload state. |
| `ChunkConflict` | The same index already contains different content. |
| `HashMismatch` | Supplied content does not match its declared SHA-256. |
| `SizeMismatch` | Supplied or assembled content has the wrong length. |
| `IncompleteUpload` | Completion was requested before every chunk arrived. |
| `LeaseUnavailable` | Another instance currently owns completion or cleanup. |

Treat `LeaseUnavailable` as a retryable conflict. A successful repeated chunk upload returns `ChunkReceipt.WasAlreadyUploaded = true`.

## Configuration

### Core options

| Setting | Default | Constraint |
|---|---:|---|
| `MaxFileSize` | 10 GiB | Positive |
| `MaxChunkSize` | 64 MiB | Positive and not greater than `MaxFileSize` |
| `MaxChunkCount` | 10,000 | Positive |
| `IncompleteUploadRetention` | 24 hours | Positive |
| `CompletionLeaseDuration` | 5 minutes | Positive |

### Filesystem options

| Setting | Default | Constraint |
|---|---:|---|
| `RootPath` | None | Required absolute path |
| `BufferSize` | 128 KiB | At least 4 KiB |

### Maintenance options

| Setting | Default | Constraint |
|---|---:|---|
| `Interval` | 15 minutes | Positive |
| `BatchSize` | 100 | Positive |

Override defaults only when application limits, infrastructure throughput, or recovery requirements justify it.

## Operational guarantees

- Chunk files are written to unique `.part` files and atomically renamed in the same directory.
- Completed files are assembled in numeric chunk order using streaming I/O.
- Completion validates chunk presence, combined length, and final SHA-256 before committing metadata.
- Optimistic concurrency and expiring leases prevent concurrent completion or cleanup of the same session.
- Expired completion leases can be recovered after an instance failure.
- Incomplete sessions expire after the configured retention period.
- Completed files are never deleted automatically.

## Multi-instance deployment

Every instance must use:

1. The same database and schema.
2. The same shared filesystem root.
3. Synchronized system clocks.
4. Consistent upload and maintenance options.

Local container or VM disks are not shared storage. Mount a durable network volume at the same logical path for every instance.

## Security responsibilities

EasyChunkUpload does not expose endpoints or authorize callers. The consuming application must:

- Authenticate clients and authorize every operation against the upload ID.
- Enforce HTTP request-body and rate limits in addition to package limits.
- Validate business rules for filenames and allowed content types.
- Keep the storage root outside the public web root.
- Avoid logging sensitive filenames or exposing physical paths.
- Define malware scanning and completed-file retention policies.

See the [security policy](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/SECURITY.md) for vulnerability reporting.

## Architecture and extension points

The core package depends only on `EasyChunkUpload.Abstractions`. EF Core, filesystem storage, and hosting are optional adapters with internal implementations.

- Implement `IChunkStorage` for another physical storage backend.
- Implement `IUploadSessionStore` and `IUploadCompletionCoordinator` together for another persistence technology.
- Resolve `IUploadMaintenanceService` to run maintenance from your own scheduler instead of the hosted worker.

Read [Architecture](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/docs/architecture.md) before implementing an adapter.

## Upgrade, support, and releases

- Migrating from 1.x: [Migration guide](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/MIGRATION.md)
- Changes by version: [Changelog](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/CHANGELOG.md)
- Contribution guide: [Contributing](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/CONTRIBUTING.md)
- Maintainer release process: [Releasing](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/RELEASING.md)
- Security policy: [Security](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/SECURITY.md)

The project follows Semantic Versioning. EasyChunkUpload 2.x targets `net8.0` and can be consumed by .NET 8 and later applications.

## License

[MIT](https://github.com/alihmaidi1/EasyChunckUpload/blob/master/LICENSE)
