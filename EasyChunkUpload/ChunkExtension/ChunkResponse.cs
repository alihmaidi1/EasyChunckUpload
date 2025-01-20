using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyChunkUpload.Enum;

namespace EasyChunkUpload.ChunkExtension;

public class ChunkResponse<T>
{

    public UploadStatus UploadStatus{get;set;}

    public T Data {get;set;}
    
}
