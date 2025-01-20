using EasyChunkUpload.ChunkExtension;

namespace EasyChunkUpload.Services;

public class ChunkUpload: IChunkUpload
{
    private readonly IFileHelper fileHelper;

    public string UploadPath{get;set;}
    public string TempUpload{get;set;}

    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // Semaphore for asynchronous locking
    public int ChunkSize { get; set; }

    public ChunkUpload(IFileHelper fileHelper,string TempUpload,string UploadPath,int ChunkSize=1024*1024*2){

        this.fileHelper=fileHelper;
        this.UploadPath=UploadPath;
        this.TempUpload=TempUpload;
        this.ChunkSize=ChunkSize;

    }
        
    
    

    public async Task<ChunkResponse<string>> UploadChunk(byte[] fileContent,string uniqueFileName,int chunkNumber,int totalChunks){

            using (var stream = new MemoryStream(fileContent))
            {
                return await UploadChunk(stream,uniqueFileName, chunkNumber, totalChunks);
            }

    }

    public async Task<ChunkResponse<string>> UploadChunk(IEnumerable<byte> fileContent,string uniqueFileName,int chunkNumber,int totalChunks)
    {


            byte[] bytes = fileContent.ToArray();
            using (var stream = new MemoryStream(bytes))
            {
                
                return await UploadChunk(stream,uniqueFileName, chunkNumber, totalChunks);
            }
    }



    public  async Task<ChunkResponse<string>> UploadChunk(Stream fileContent,string uniqueFileName, int chunkNumber, int totalChunks){

        await _semaphore.WaitAsync();
        try{

            string fileName = Path.GetFileNameWithoutExtension(uniqueFileName);
            string newPath = Path.Combine(TempUpload, fileName);
            if(!Directory.Exists(newPath)){

                Directory.CreateDirectory(newPath);
            }
            string chunkFileName = $"{fileName}_chunk_{chunkNumber}";
            string chunkPath = Path.Combine(TempUpload,fileName, chunkFileName);
            using(FileStream fs = System.IO.File.Create(chunkPath)){
                        
                fileContent.Position=0;
                await fileContent.CopyToAsync(fs);
            
            }   

            if(chunkNumber==totalChunks){
                var filename=await ChunksCompletedAsync(Path.GetFileName(uniqueFileName));                
                return ChunkHelper.Success(Enum.UploadStatus.FileUploadCompleted,filename);
                
            }

            return ChunkHelper.Success<string>(Enum.UploadStatus.ChunkUploadCompleted);            
            }
            finally
            {
                _semaphore.Release();
            }
            


    }

    


    public async Task<string> ChunksCompletedAsync(string fullFileName)
    {
        var fileName=Path.GetFileNameWithoutExtension(fullFileName);
        string tempPath = Path.Combine(TempUpload, fileName);
        string[] ChunkFiles = Directory.GetFiles(tempPath)
        .Where(p => p.Contains(fileName))
        .OrderBy(p => Int32.Parse(p.Split($"{fileName}_chunk_")[1]))
        .ToArray();
        string targetPath=Path.Combine(UploadPath, fileName);
        await MergeChunksAsync(fullFileName,ChunkFiles);
        return fullFileName;
    }




    public async Task MergeChunksAsync(string destinationFilePath, string[] chunkFilePaths)
    {
        if(!Directory.Exists(UploadPath)){

            Directory.CreateDirectory(UploadPath);
        }
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
