using EasyChunkUpload.ChunkExtension;

namespace EasyChunkUpload.Services.ChunkUpload;
public interface IChunkUpload
{

    Task<Guid> StartUploadAsync(string fileName);

    Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber, Stream fileContent);


    Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,byte[] fileContent);



    Task<ChunkResponse<int>> GetLastChunk(Guid fileId); 




    Task<ChunkResponse<string>> ChunkUploadCompleted(Guid fileId,string fileName);
    
    
    Task<ChunkResponse<bool>> CancelUpload(Guid fileId);

}
