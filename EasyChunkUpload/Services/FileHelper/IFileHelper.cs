namespace EasyChunkUpload.Services.FileHelper;
public interface IFileHelper
{
    /// <summary>
    /// Deletes a collection of files from the specified paths.
    /// </summary>
    /// <param name="filePaths">An array containing the paths of the files to be deleted.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    /// <exception cref="FileNotFoundException">Thrown if any of the specified files do not exist.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs during the delete operation.</exception>
    public Task DeleteFilesAsync(string[] filePaths);

    
}
