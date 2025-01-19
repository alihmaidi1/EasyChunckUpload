namespace EasyChunkUpload;
public class ChunkUpload: IChunkUpload
{
    private IFileHelper fileHelper;

    public ChunkUpload(IFileHelper fileHelper){

        this.fileHelper=fileHelper;

    }
    
    public async Task MergeChunksAsync(string destinationFilePath, string[] chunkFilePaths)
    {
        using (var destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
        {
            foreach (var chunkFilePath in chunkFilePaths)
            {
                using (var chunkStream = new FileStream(chunkFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
                {
                    await chunkStream.CopyToAsync(destinationStream);
                }
            }

            await fileHelper.DeleteFilesAsync(chunkFilePaths);
            
        }
    }


    
    
}
