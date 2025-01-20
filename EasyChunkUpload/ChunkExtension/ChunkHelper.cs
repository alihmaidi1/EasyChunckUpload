using EasyChunkUpload.Enum;

namespace EasyChunkUpload.ChunkExtension;

public class ChunkHelper
{

      public static ChunkResponse<T> Success<T>(string Message="",T data= default)
            => new ChunkResponse<T>() { Data = data,Status=true,Message=Message};


      public static ChunkResponse<T> Fail<T>(string Message="",T data= default)
      => new ChunkResponse<T>() { Data = data,Status=false,Message=Message};


    
}
