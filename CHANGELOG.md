# Changelog

All notable changes to this project are documented in this file. The project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/alihmaidi1/EasyChunckUpload/compare/v2.0.1...HEAD
[2.0.1]: https://github.com/alihmaidi1/EasyChunckUpload/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/alihmaidi1/EasyChunckUpload/compare/v1.0...v2.0.0
