namespace EasyChunkUpload.Services.FileHelper;

public class FileHelper: IFileHelper
{


    public async Task DeleteFilesAsync(string[] filePaths)
    {
        foreach (var filePath in filePaths)
        {

                if (File.Exists(filePath))                
                    await Task.Run(() => File.Delete(filePath));
                
        }
    }


    
}
