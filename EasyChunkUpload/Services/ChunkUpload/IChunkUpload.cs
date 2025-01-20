using EasyChunkUpload.ChunkExtension;

namespace EasyChunkUpload.Services.ChunkUpload;
public interface IChunkUpload
{



    // string TempUpload{get;set;}
    // string UploadPath{get;set;}

    // int ChunkSize { get; set; }



    Task<Guid> StartUploadAsync(string fileName);

    Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber, Stream fileContent);


    Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,byte[] fileContent);



    Task<ChunkResponse<int>> GetLastChunk(Guid fileId); 




    Task<ChunkResponse<string>> ChunkUploadCompleted(Guid fileId,string fileName);
    
    
    Task<ChunkResponse<bool>> CancelUpload(Guid fileId);

    // /// <summary>
    // /// Uploads a chunk of file content to the server.
    // /// </summary>
    // /// <param name="fileContent">The stream containing the file content.</param>
    // /// <param name="fileName">The name of the file being uploaded.</param>
    // /// <param name="chunkNumber">The sequence number of the chunk.</param>
    // /// <param name="totalChunks">The total number of chunks in the file.</param>
    // /// <returns>A task that represents the asynchronous operation.</returns>
    // Task<ChunkResponse<string>> UploadChunk(Stream fileContent,string fileName, int chunkNumber, int totalChunks);
    
    
    // /// <summary>
    // /// Uploads a chunk of file content to the server.
    // /// </summary>
    // /// <param name="fileContent">The byte Array containing the file content.</param>
    // /// <param name="fileName">The name of the file being uploaded.</param>
    // /// <param name="chunkNumber">The sequence number of the chunk.</param>
    // /// <param name="totalChunks">The total number of chunks in the file.</param>
    // /// <returns>A task that represents the asynchronous operation.</returns>
    // Task<ChunkResponse<string>> UploadChunk(byte[] fileContent,string fileName,int chunkNumber,int totalChunks);
    

    // /// <summary>
    // /// Uploads a chunk of file content to the server.
    // /// </summary>
    // /// <param name="fileContent">The IEnumerable containing the file content.</param>
    // /// <param name="fileName">The name of the file being uploaded.</param>
    // /// <param name="chunkNumber">The sequence number of the chunk.</param>
    // /// <param name="totalChunks">The total number of chunks in the file.</param>
    // /// <returns>A task that represents the asynchronous operation.</returns>
    // Task<ChunkResponse<string>> UploadChunk(IEnumerable<byte> fileContent,string fileName,int chunkNumber,int totalChunks);

    // /// <summary>
    // /// Merges a collection of file chunks into a single file.
    // /// </summary>
    // /// <param name="destinationFilePath">The full path of the final file to be created.</param>
    // /// <param name="chunkFilePaths">An array containing the paths of the file chunks to be merged.</param>
    // /// <returns>A task representing the asynchronous merge operation.</returns>
    // /// <exception cref="FileNotFoundException">Thrown if any of the chunk files do not exist.</exception>
    // /// <exception cref="IOException">Thrown when an I/O error occurs during the operation.</exception>
    // public Task MergeChunksAsync(string destinationFilePath, string[] chunkFilePaths);
}
