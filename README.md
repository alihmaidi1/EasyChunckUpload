# EasyChunkUpload 2.0

[![CI](https://github.com/alihmaidi1/EasyChunckUpload/actions/workflows/ci.yml/badge.svg)](https://github.com/alihmaidi1/EasyChunckUpload/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/EasyChunkUpload.svg)](https://www.nuget.org/packages/EasyChunkUpload)
[![NuGet Downloads](https://img.shields.io/nuget/dt/EasyChunkUpload.svg)](https://www.nuget.org/packages/EasyChunkUpload)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

EasyChunkUpload is a provider-neutral .NET library for resumable, integrity-checked chunk uploads. It separates upload orchestration, metadata persistence, physical storage, and background maintenance so applications can replace infrastructure without changing the core workflow.

## Requirements

- .NET 8 or later
- A shared relational database for multi-instance deployments
- A shared filesystem mounted by every application instance

## Packages

```bash
dotnet add package EasyChunkUpload --version 2.0.0
dotnet add package EasyChunkUpload.Storage.FileSystem --version 2.0.0
dotnet add package EasyChunkUpload.Persistence.EntityFrameworkCore --version 2.0.0
dotnet add package EasyChunkUpload.Hosting --version 2.0.0
```

`EasyChunkUpload.Abstractions` is referenced transitively and is available separately for custom adapters.

## Registration

```csharp
using EasyChunkUpload;
using EasyChunkUpload.Hosting;
using EasyChunkUpload.Persistence.EntityFrameworkCore;
using EasyChunkUpload.Storage.FileSystem;
using Microsoft.EntityFrameworkCore;

builder.Services
    .AddEasyChunkUpload(options =>
    {
        options.MaxFileSize = 10L * 1024 * 1024 * 1024;
        options.MaxChunkSize = 64L * 1024 * 1024;
        options.MaxChunkCount = 10_000;
    })
    .UseSharedFileSystem(options =>
    {
        options.RootPath = builder.Configuration["ChunkUpload:RootPath"]!;
    })
    .UseEntityFrameworkStore(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("ChunkUploads"));
    })
    .AddUploadMaintenanceWorker();
```

Use a shared path such as a mounted network volume when the application runs on multiple instances. Local instance storage does not provide multi-instance guarantees.

## Database schema

The package exposes `UploadDbContext` and leaves provider selection and migrations to the consuming application.

```bash
dotnet ef migrations add EasyChunkUploadV2 \
  --context UploadDbContext \
  --output-dir Data/Migrations/EasyChunkUpload

dotnet ef database update --context UploadDbContext
```

## Upload flow

Chunk indexes are zero-based. SHA-256 values must contain exactly 64 hexadecimal characters.

```csharp
var started = await uploadService.StartAsync(
    new StartUploadRequest(
        FileName: "archive.zip",
        ContentLength: fileLength,
        TotalChunks: totalChunks,
        Sha256: fileSha256),
    cancellationToken);

var uploadId = started.Value!.UploadId;

await uploadService.UploadChunkAsync(
    uploadId,
    chunkIndex,
    chunkStream,
    chunkLength,
    chunkSha256,
    cancellationToken);

var completed = await uploadService.CompleteAsync(uploadId, cancellationToken);
var storageKey = completed.Value!.StorageKey;
```

The completed result returns a storage key, never a machine-local path. The original file name is metadata and is not used to construct physical storage paths.

## Guarantees

- Required SHA-256 validation for every chunk and the assembled file
- Idempotent retries for matching chunks
- Conflict detection when the same chunk index contains different data
- Atomic chunk and completed-file publication
- Numeric chunk ordering for any supported chunk count
- Optimistic concurrency and completion leases across application instances
- Scoped EF Core services and scoped background maintenance work
- Cancellation support for all public operations

## Operational defaults

| Setting | Default |
|---|---:|
| Maximum file size | 10 GiB |
| Maximum chunk size | 64 MiB |
| Maximum chunk count | 10,000 |
| Incomplete upload retention | 24 hours |
| Completion lease | 5 minutes |
| Maintenance interval | 15 minutes |
| Filesystem buffer | 128 KiB |

Completed files are not deleted automatically. The application owns their retention policy.

## HTTP and security

EasyChunkUpload does not expose HTTP endpoints or perform authorization. The consuming application must authenticate callers, authorize access to upload IDs, apply request limits, and avoid exposing storage paths.

## Upgrading from 1.x

Version 2.0 is intentionally breaking and does not migrate active 1.x sessions. Read [MIGRATION.md](MIGRATION.md) before upgrading.

## Versioning and releases

The project follows Semantic Versioning and records changes in [CHANGELOG.md](CHANGELOG.md). Releases are created from `vMAJOR.MINOR.PATCH` tags after CI, tests, package validation, and dependency vulnerability checks succeed. Maintainer instructions are available in [RELEASING.md](RELEASING.md).

## License

MIT
