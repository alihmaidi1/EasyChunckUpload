# ChunkUpload Service

[![NuGet Version](https://img.shields.io/nuget/v/ChunkUploadService.svg?style=flat-square)](https://www.nuget.org/packages/ChunkUploadService/)
[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET Core](https://img.shields.io/badge/.NET-9.0%2B-blue.svg?style=flat-square)](https://dotnet.microsoft.com/)

A robust implementation for handling large file uploads using chunking strategy with resumable capabilities and concurrent processing support.

## Table of Contents
- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [API Documentation](#api-documentation)
- [Error Handling](#error-handling)
- [Performance](#performance)
- [Contributing](#contributing)
- [License](#license)

## Features <a name="features"></a>
- 🚀 **Resumable Uploads** - Continue interrupted uploads
- ⚡ **Chunk Validation** - Automatic chunk order verification
- 🔒 **Concurrency Control** - Thread-safe operations with SemaphoreSlim
- 📁 **Atomic Merging** - Temp file strategy for data integrity
- 📊 **Progress Tracking** - Real-time upload status monitoring
- 🧹 **Auto-Cleanup** - Configurable temp file retention

## Installation <a name="installation"></a>
```bash
# Package Manager
Install-Package ChunkUploadService

# .NET CLI
dotnet add package ChunkUploadService
```
### Quick Start <a name="quick-start"></a>
 **1. Configure Services**
```public void ConfigureServices(IServiceCollection services)
{
    services.AddEasyChunkUploadConfiguration(new ChunkUploadSettings{

          TempFolder = "your_temp_folder",
          CleanupInterval= 60 * 60 * 24, //in seconds
          CompletedFilesExpiration= 60 * 60 // in seconds
  
    });
    
    // Add other dependencies
}
```
 **2. Basic Usage Example**
```public class UploadController : ControllerBase
{
    private readonly IChunkUpload _chunkUpload;

    public UploadController(IChunkUpload chunkUpload)
    {
        _chunkUpload = chunkUpload;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartUpload([FromBody] FileInfoModel model)
    {
        var fileId = await _chunkUpload.StartUploadAsync(model.FileName);
        return Ok(new { FileId = fileId });
    }
}
```
## Option Configuration(ChunkUploadSettings) <a name="configuration"></a>
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `TempFolder` | `string` | **Required** | Temporary storage directory path for chunks |
| `CleanupInterval` | `int` | `3600` | Temp files cleanup interval (seconds) |
| `CompletedFilesExpiration` | `int` | `60*60*24*7` | how Many (Seconds) time File While be expired after upload lastest chunk  |




