using EasyChunkUpload.Enum;

namespace EasyChunkUpload.ChunkExtension;

public class ChunkHelper
{

      public static ChunkResponse<T> Success<T>(string Message="",T data= default)
            => new ChunkResponse<T>() { Data = data,Status=true,Message=Message};


      public static ChunkResponse<T> Fail<T>(string Message="",T data= default)
      => new ChunkResponse<T>() { Data = data,Status=false,Message=Message};


      public static void IsValidFileName(string fileName){

            if (string.IsNullOrWhiteSpace(fileName))
            {
                  throw new ArgumentException("File name cannot be null or whitespace.", nameof(fileName));
            }
            var invalidChars = Path.GetInvalidFileNameChars();

            if (fileName.Any(c => invalidChars.Contains(c)))
            {
                  throw new ArgumentException("File name contains invalid characters.", nameof(fileName));
            }

            if (fileName.Length > 255)
            {
                  throw new ArgumentException("File name is too long.", nameof(fileName));
            }

            if(!Path.HasExtension(fileName)){

                  throw new ArgumentException("File name is not have extension.", nameof(fileName));

            }


            
      }

    
}
