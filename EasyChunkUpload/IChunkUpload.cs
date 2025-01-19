namespace EasyChunkUpload;

public interface IChunkUpload
{

    /// <summary>
    /// Merges a collection of file chunks into a single file.
    /// </summary>
    /// <param name="destinationFilePath">The full path of the final file to be created.</param>
    /// <param name="chunkFilePaths">An array containing the paths of the file chunks to be merged.</param>
    /// <returns>A task representing the asynchronous merge operation.</returns>
    /// <exception cref="FileNotFoundException">Thrown if any of the chunk files do not exist.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs during the operation.</exception>
    public Task MergeChunksAsync(string destinationFilePath, string[] chunkFilePaths);
}
