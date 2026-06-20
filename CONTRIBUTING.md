# Contributing to EasyChunkUpload

Thank you for helping improve EasyChunkUpload.

## Report an issue

Open a [GitHub issue](https://github.com/alihmaidi1/EasyChunckUpload/issues) with:

- The affected package and version
- Target framework, operating system, database provider, and storage type
- Minimal reproduction steps
- Expected and actual behavior
- Relevant logs with secrets and personal data removed

Report security vulnerabilities privately according to [SECURITY.md](SECURITY.md).

## Development setup

Prerequisites:

- The .NET SDK versions selected by `global.json`
- Git

```bash
git clone https://github.com/alihmaidi1/EasyChunckUpload.git
cd EasyChunckUpload
dotnet restore EasyChunckUpload.sln
dotnet build EasyChunckUpload.sln --configuration Release --no-restore
dotnet test EasyChunckUpload.sln --configuration Release --no-build
```

## Make a change

1. Fork the repository.
2. Create a focused branch from `master`.
3. Keep changes scoped to one problem.
4. Add or update tests for public behavior.
5. Update documentation when behavior, configuration, or public API changes.
6. Run the quality checks below.
7. Open a pull request explaining the problem, design, and verification.

## Quality checks

```bash
dotnet restore EasyChunckUpload.sln
dotnet build EasyChunckUpload.sln --configuration Release --no-restore -p:ContinuousIntegrationBuild=true
dotnet test EasyChunckUpload.sln --configuration Release --no-build
dotnet format EasyChunckUpload.sln --verify-no-changes --no-restore
dotnet list EasyChunckUpload.sln package --vulnerable --include-transitive
```

CI runs consumer tests on .NET 8, .NET 9, and .NET 10 and validates the package on Windows and Linux.

## Design guidelines

- Keep the core independent of EF Core, ASP.NET Core, and physical storage.
- Keep implementation types internal unless consumers must reference or implement them.
- Preserve cancellation, idempotency, integrity checks, and distributed lease semantics.
- Avoid adding a public abstraction without a real consumer substitution point.
- Treat public API changes as compatibility decisions and document them in `CHANGELOG.md`.
- Add integration tests for storage, persistence, concurrency, or recovery changes.

Read [Architecture](docs/architecture.md) before changing package boundaries or implementing an adapter.

## Documentation style

- Prefer executable examples using the current public API.
- State defaults, constraints, ownership, and failure behavior explicitly.
- Use `EasyChunkUpload` for package names and preserve the repository URL spelling `EasyChunckUpload`.
- Never include real credentials, private paths, or sensitive logs.

## Code of conduct

Participation is governed by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
