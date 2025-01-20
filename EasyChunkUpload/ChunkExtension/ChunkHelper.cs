using EasyChunkUpload.Enum;

namespace EasyChunkUpload.ChunkExtension;

public class ChunkHelper
{

      public static ChunkResponse<T> Success<T>(UploadStatus uploadStatus,T data= default)
            => new ChunkResponse<T>() { Data = data,UploadStatus=uploadStatus};



    
}
