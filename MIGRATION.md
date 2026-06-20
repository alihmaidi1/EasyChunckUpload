# Migrating from EasyChunkUpload 1.x to 2.0

EasyChunkUpload 2.0 replaces the 1.x API, metadata model, storage layout, and dependency-injection registration. There is no compatibility layer.

## Before upgrading

1. Stop accepting new 1.x upload sessions.
2. Allow active sessions to complete or cancel them explicitly.
3. Back up completed files and the 1.x metadata table if required.
4. Deploy the shared filesystem path that all application instances will mount.
5. Generate and apply migrations for `UploadDbContext`.

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
