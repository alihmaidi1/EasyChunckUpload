using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EasyChunkUpload.Services.FileService;

public interface IFileService
{
    

    public Task<bool> DeleteFile(Guid fileId);

    public Task<bool> IsExists(Guid fileId);
    
}
