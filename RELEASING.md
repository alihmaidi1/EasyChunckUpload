# Releasing EasyChunkUpload

EasyChunkUpload uses Semantic Versioning. Breaking public API or behavior changes increment the major version, backward-compatible features increment the minor version, and backward-compatible fixes increment the patch version.

## Release process

1. Update `VersionPrefix` in `src/Directory.Build.props`.
2. Move entries from `Unreleased` into a dated section in `CHANGELOG.md`.
3. Run `dotnet restore`, `dotnet build -c Release`, and `dotnet test -c Release`.
4. Run `dotnet format EasyChunckUpload.sln --verify-no-changes --no-restore`.
5. Run `dotnet list EasyChunckUpload.sln package --vulnerable --include-transitive`.
6. Pack locally and inspect the `.nupkg` files.
7. Verify README links, XML documentation, migration guidance, and package release notes.
8. Commit and push the release changes to `master`.
9. Wait for the `CI` workflow to succeed on the release commit.
10. Create and push an annotated tag such as `v2.0.1`.

The `Release` workflow validates the tag, repeats all quality gates, publishes packages to NuGet.org in dependency order, and creates the GitHub release.

## Required repository configuration

- GitHub environment: `nuget.org`
- NuGet.org Trusted Publishing policy owned by `AliHmaidi` for repository `alihmaidi1/EasyChunckUpload`, workflow `release.yml`, and environment `nuget.org`.
- Require the `CI` workflow before merging changes into `master`.

The release workflow uses GitHub OIDC through `NuGet/login@v1` to obtain a short-lived NuGet API key. No long-lived NuGet secret is stored in GitHub or local configuration.

## Post-release verification

1. Confirm the `Release` workflow completed successfully.
2. Confirm the GitHub release is public and contains `.nupkg` and `.snupkg` assets for all five packages.
3. Confirm the new version appears in the NuGet V3 feed for every package ID.
4. Install the published packages in a clean sample application.
5. Move the next planned work under `Unreleased` in `CHANGELOG.md`.
