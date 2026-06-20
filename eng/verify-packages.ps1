param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$packageRoot = (Resolve-Path $PackageDirectory).Path
$temporaryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path ([System.IO.Path]::GetTempPath()) "easy-chunk-upload-package-test-$([Guid]::NewGuid().ToString('N'))"))
$systemTemporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())

if (-not $temporaryRoot.StartsWith($systemTemporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Temporary project path is outside the system temporary directory: $temporaryRoot"
}

function Invoke-DotNet {
    param([string[]] $Arguments)

    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

try {
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    $escapedPackageRoot = [System.Security.SecurityElement]::Escape($packageRoot)
    $nugetConfig = Join-Path $temporaryRoot 'NuGet.Config'
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$escapedPackageRoot" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -Path $nugetConfig

    foreach ($framework in @('net8.0', 'net9.0', 'net10.0')) {
        $projectDirectory = Join-Path $temporaryRoot $framework
        New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
        Invoke-DotNet @('new', 'console', '--framework', $framework, '--output', $projectDirectory, '--no-restore')
        $projectFiles = @(Get-ChildItem -Path $projectDirectory -Filter '*.csproj' -File)
        if ($projectFiles.Count -ne 1) {
            throw "Expected one project in $projectDirectory, found $($projectFiles.Count)."
        }

        $projectPath = $projectFiles[0].FullName

        foreach ($package in @(
            'EasyChunkUpload',
            'EasyChunkUpload.Storage.FileSystem',
            'EasyChunkUpload.Persistence.EntityFrameworkCore',
            'EasyChunkUpload.Hosting')) {
            Invoke-DotNet @('add', $projectPath, 'package', $package, '--version', $Version, '--no-restore')
        }

        Invoke-DotNet @(
            'add', $projectPath, 'package', 'Microsoft.EntityFrameworkCore.Sqlite',
            '--version', '8.0.28', '--no-restore')
        Invoke-DotNet @(
            'add', $projectPath, 'package', 'SQLitePCLRaw.bundle_e_sqlite3',
            '--version', '3.0.3', '--no-restore')

        @'
using EasyChunkUpload;
using EasyChunkUpload.Hosting;
using EasyChunkUpload.Persistence.EntityFrameworkCore;
using EasyChunkUpload.Storage.FileSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var root = Path.Combine(Path.GetTempPath(), "easy-chunk-upload-consumer", Guid.NewGuid().ToString("N"));
var services = new ServiceCollection();
services.AddLogging();
services
    .AddEasyChunkUpload()
    .UseSharedFileSystem(options => options.RootPath = root)
    .UseEntityFrameworkStore(options => options.UseSqlite("Data Source=:memory:"))
    .AddUploadMaintenanceWorker();

await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
});
'@ | Set-Content -Path (Join-Path $projectDirectory 'Program.cs')

        Invoke-DotNet @('restore', $projectPath, '--configfile', $nugetConfig)
        Invoke-DotNet @(
            'build', $projectPath, '--configuration', 'Release', '--no-restore',
            '-p:TreatWarningsAsErrors=true')

        $vulnerabilityReport = & dotnet list $projectPath package --vulnerable --include-transitive
        if ($LASTEXITCODE -ne 0) {
            throw "Vulnerability scan failed for $framework."
        }

        $vulnerabilityReport | Out-Host
        if ($vulnerabilityReport -match 'has the following vulnerable packages') {
            throw "Consumer project for $framework contains vulnerable packages."
        }
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
