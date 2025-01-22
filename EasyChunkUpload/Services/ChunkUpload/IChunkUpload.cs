using EasyChunkUpload.ChunkExtension;

namespace EasyChunkUpload.Services.ChunkUpload;
public interface IChunkUpload
{

    Task<Guid> StartUploadAsync(string fileName);

    Task<ChunkResponse<int>> GetLastChunk(Guid fileId); 


    Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber, Stream fileContent);


    Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,byte[] fileContent);






    Task<ChunkResponse<string>> ChunkUploadCompleted(Guid fileId);
    
    
    Task<ChunkResponse<bool>> CancelUploadAsync(Guid fileId);

}
