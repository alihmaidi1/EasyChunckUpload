# ChunkUpload Service

[![NuGet Version](https://img.shields.io/nuget/v/ChunkUploadService.svg?style=flat-square)](https://www.nuget.org/packages/EasyChunkUpload/)
[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET Core](https://img.shields.io/badge/.NET-9.0%2B-blue.svg?style=flat-square)](https://dotnet.microsoft.com/)

A robust implementation for handling large file uploads using chunking strategy with resumable capabilities and concurrent processing support.

## Table of Contents
- [Features](#features)
- [Installation](#installation)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [API Documentation](#api-documentation)
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
Install-Package  EasyChunkUpload 

# .NET CLI
dotnet add package  EasyChunkUpload 
```
# Requirements <a name="requirements"></a>
- .NET 9.0 SDK or later
-  Microsoft.EntityFrameworkCore (>= 9.0.1)
-   Microsoft.Extensions.DependencyInjection (>= 9.0.1)
-    Microsoft.Extensions.Hosting (>= 9.0.1) 

## Quick Start <a name="quick-start"></a>
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
### Option Configuration(ChunkUploadSettings) <a name="configuration"></a>
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `TempFolder` | `string` | **Required** | Temporary storage directory path for chunks |
| `CleanupInterval` | `int` | `3600` | Temp files cleanup interval (seconds) |
| `CompletedFilesExpiration` | `int` | `60*60*24*7` | how Many (Seconds) time File While be expired after upload lastest chunk  |


## API Documentation <a name="api-documentation"></a>
Core Methods
```
public interface IChunkUpload
{

    /// <summary>Initializes new upload session</summary>
    Task<Guid> StartUploadAsync(string fileName);

    /// <summary>Retrieves last uploaded chunk number</summary>
    Task<ChunkResponse<int>> GetLastChunk(Guid fileId);

    /// <summary>Processes chunk from Stream source</summary>
    Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber, Stream fileContent);

    /// <summary>Processes chunk from byte array</summary>
    Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,byte[] fileContent);

    /// <summary>Finalizes upload and merges chunks</summary>
    Task<ChunkResponse<string>> ChunkUploadCompleted(Guid fileId);

    /// <summary>Identifies missing chunks in sequence</summary>
    Task<ChunkResponse<List<int>>> GetLostChunkNumber(Guid fileId);

    /// <summary>Cancels active upload session</summary>
    Task<ChunkResponse<bool>> CancelUploadAsync(Guid fileId);

}
```
Response Model
```
public class ChunkResponse<T>
{
    /// <summary>Detailed message for failures or success</summary>
    public string Message{get;set;}

    /// <summary>Operation  status</summary>
    public bool Status{get;set;}

    /// <summary>Result payload</summary>
    public T Data {get;set;}
    
}
```
## Performance <a name="performance"></a>
Optimization Tips :

 **Chunk Sizes**: Use 5-10MB chunks for optimal throughput
 
 **File Locking**: Monitor SemaphoreSlim usage



 ## Contributing <a name="contributing"></a>
    Fork the repository

    Create feature branch:
    git checkout -b feature/your-feature

    Commit changes:
    git commit -m 'Add awesome feature'

    Push to branch:
    git push origin feature/your-feature

    Open a Pull Request
    
 ## License <a name="license"></a>

MIT License - See LICENSE for full text.

📫 Contact: [Ali Hmaidi] - alihmaidi095@gmail.com

🔗 Repository: https://github.com/alihmaidi1/EasyChunckUpload
