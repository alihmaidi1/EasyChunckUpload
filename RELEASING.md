# Releasing EasyChunkUpload

EasyChunkUpload uses Semantic Versioning. Breaking public API or behavior changes increment the major version, backward-compatible features increment the minor version, and backward-compatible fixes increment the patch version.

## Release process

1. Update `VersionPrefix` in `src/Directory.Build.props`.
2. Move entries from `Unreleased` into a dated section in `CHANGELOG.md`.
3. Run `dotnet restore`, `dotnet build -c Release`, and `dotnet test -c Release`.
4. Run `dotnet format EasyChunckUpload.sln --verify-no-changes --no-restore`.
5. Run `dotnet list EasyChunckUpload.sln package --vulnerable --include-transitive`.
6. Pack locally and inspect the `.nupkg` files.
7. Commit and push the release changes to `master`.
8. Create and push an annotated tag such as `v2.0.0`.

The `Release` workflow validates the tag, repeats all quality gates, publishes packages to NuGet.org in dependency order, and creates the GitHub release.

## Required repository configuration

- GitHub environment: `nuget.org`
- GitHub Actions secret: `NUGET_API_KEY`
- The API key must be scoped to the five `EasyChunkUpload` package IDs when NuGet.org package ownership permits it.
- Require the `CI` workflow before merging changes into `master`.

Never commit API keys or place them in local configuration files.
