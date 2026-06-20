# Migrating from EasyChunkUpload 1.x to 2.0

EasyChunkUpload 2.0 replaces the 1.x API, metadata model, storage layout, and dependency-injection registration. There is no compatibility layer.

## Before upgrading

1. Stop accepting new 1.x upload sessions.
2. Allow active sessions to complete or cancel them explicitly.
3. Back up completed files and the 1.x metadata table if required.
4. Deploy the shared filesystem path that all application instances will mount.
5. Generate and apply migrations for `UploadDbContext`.
6. Replace 1.x dependency-injection registration and HTTP handlers with the 2.0 service workflow.

## API replacements

| 1.x | 2.0 |
|---|---|
| `IChunkUpload` | `IChunkUploadService` |
| `StartUploadAsync` | `StartAsync` |
| `UploadChunkAsync` | `UploadChunkAsync` with length and SHA-256 |
| `GetLastChunk` | `GetStatusAsync` |
| `GetLostChunkNumber` | `GetMissingChunksAsync` |
| `ChunkUploadCompleted` | `CompleteAsync` |
| `CancelUploadAsync` | `CancelAsync` |
| `ChunkResponse<T>` | `UploadResult<T>` |

Chunk indexes change from one-based to zero-based. Every upload must declare its final length, total chunk count, and final SHA-256 before any chunks are accepted.

## Data migration

The 1.x `FileModel` table is not reused. Version 2.0 creates `EasyChunkUploadSessions` and `EasyChunkUploadChunks` through the consuming application's EF Core migrations.

Do not attempt to resume 1.x sessions through the 2.0 API. Existing completed files can remain in their current location or be moved into application-owned storage separately.

## Registration replacement

Version 2.0 requires explicit core, storage, and persistence registration:

```csharp
services
    .AddEasyChunkUpload()
    .UseSharedFileSystem(options => options.RootPath = sharedRoot)
    .UseEntityFrameworkStore(options => options.UseSqlServer(connectionString))
    .AddUploadMaintenanceWorker();
```

The consuming application must reference its chosen EF Core provider. The package does not select one.

## Behavioral changes

- Chunk indexes are `0..TotalChunks - 1`.
- Every chunk requires a declared length and SHA-256.
- Starting an upload requires the final file length and SHA-256.
- Completion fails until all indexes exist and their combined length matches the declaration.
- Repeating a matching chunk succeeds; repeating an index with different metadata returns `ChunkConflict`.
- Completed results contain an opaque storage key, not a filesystem path.
- Completed files are not deleted by package maintenance.
- Expected failures use `UploadResult`; infrastructure failures continue to throw exceptions.

## Deployment sequence

1. Deploy the shared filesystem and verify every instance can read and write it.
2. Apply the new `UploadDbContext` migration.
3. Deploy the 2.0 application with identical options on every instance.
4. Verify start, chunk upload, missing-chunk lookup, completion, and cancellation in the target environment.
5. Remove 1.x tables and artifacts only after the rollback window closes.

For the runtime boundaries and recovery model, read [Architecture](docs/architecture.md).
