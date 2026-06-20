# Changelog

All notable changes to this project are documented in this file. The project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.1.0] - 2026-06-20

### Added

- Renewable completion and cleanup leases for long-running storage operations.
- Owner-conditional cleanup finalization and incomplete-session metadata retention.
- Public API baseline analysis and packed-package consumer verification.
- Metrics for lease renewal, lease loss, recovery, cleanup, metadata purge, and maintenance duration.

### Changed

- Normalized EF Core timestamp columns to UTC `DateTime` for provider-portable comparisons.
- Rejected upload plans that cannot represent the declared file size.
- Enabled durable filesystem flushes by default before atomic publication.
- Removed user filenames from package logs.
- Expanded concurrency, cleanup, persistence, and filesystem tests.

### Fixed

- Prevented completion and cleanup work from silently outliving their leases.
- Prevented a stale cleanup worker from clearing another worker's lease.
- Prevented cleaned incomplete-session metadata from growing without retention.
- Preserved unexpected EF Core update failures instead of flattening them into state conflicts.

## [2.0.1] - 2026-06-20

### Documentation

- Expanded the consumer quick start, configuration, error model, and operational guidance.
- Added architecture, adapter-authoring, and Minimal API integration guides.

## [2.0.0] - 2026-06-20

### Added

- Provider-neutral upload orchestration targeting .NET 8 and later.
- Required SHA-256 verification for chunks and completed files.
- Shared-filesystem storage with atomic writes and numeric assembly.
- EF Core metadata persistence with optimistic concurrency and distributed leases.
- Hosted cleanup and expired completion-lease recovery.
- Architecture, unit, storage, persistence, integration, and .NET 8/9/10 consumer tests.
- Source symbols, XML documentation, migration guidance, and automated releases.

### Changed

- Replaced the 1.x API with immutable requests, descriptors, and structured results.
- Changed chunk indexes from one-based to zero-based.
- Split the library into focused Core, Abstractions, FileSystem, EF Core, and Hosting packages.

### Removed

- Removed `IChunkUpload`, `ChunkResponse<T>`, `FileModel`, and the 1.x storage layout.
- Removed support for resuming active 1.x upload sessions.

### Security

- Added file-size, chunk-size, and chunk-count limits.
- Removed user filenames from physical storage paths.
- Added dependency vulnerability checks to CI and release workflows.

[Unreleased]: https://github.com/alihmaidi1/EasyChunckUpload/compare/v2.1.0...HEAD
[2.1.0]: https://github.com/alihmaidi1/EasyChunckUpload/compare/v2.0.1...v2.1.0
[2.0.1]: https://github.com/alihmaidi1/EasyChunckUpload/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/alihmaidi1/EasyChunckUpload/compare/v1.0...v2.0.0
