using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;

namespace EasyChunkUpload.Services.FileService;

public interface IFileService
{
    

    public Task<bool> DeleteFile(Guid fileId);

    public Task<bool> IsExists(Guid fileId);


    public Task<ChunkResponse<int>> GetLastChunk(Guid fileId);   

    public Task<FileModel?> GetFile(Guid fileId);
}
