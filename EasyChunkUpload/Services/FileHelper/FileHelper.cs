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

    public async Task<bool> DeleteDirectory(string path){


        Directory.Delete(path,true);
        return true;

    }


    public async Task RetryIOAsync(Func<Task> action, int maxRetries, int delayMs)
    {
        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                await action();
                return;
            }
            catch (IOException) when (retry < maxRetries - 1)
            {
                await Task.Delay(delayMs * (retry + 1));
            }
        }
    }


    
}
