using System.Collections.Concurrent;
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;
using EasyChunkUpload.Services.FileHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace EasyChunkUpload.Services.ChunkUpload;

public class ChunkUpload : IChunkUpload
{
    private readonly DbContext _dbContext;
    
    private string TempFolder;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

    private readonly IFileHelper _fileHelper;
    
    
    public ChunkUpload(DbContext dbContext,IFileHelper fileHelper){

        _dbContext=dbContext;
        _fileHelper=fileHelper;

        this.TempFolder=Path.Combine("wwwroot",TempFolder);
    }

    public async Task<Guid> StartUploadAsync(string fileName)
    {
        var id=Guid.NewGuid();
        await _dbContext.Set<FileModel>().AddAsync(new FileModel{
            
            Id=id,
            FileName=fileName

        });
        var fileDirectory=Path.Combine(this.TempFolder,id.ToString());
        Directory.CreateDirectory(fileDirectory);
        return id;
    
    }

    public async Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,Stream fileContent)
    {

        if(fileContent==null||fileContent.Length==0)
            return ChunkHelper.Fail<Object>("File content is empty.");
        
        var file=await _dbContext.Set<FileModel>().FirstOrDefaultAsync(x=>x.Id==fileId);
        if(file is null) return ChunkHelper.Fail<Object>("file id is not exists");        
        
        if(chunkNumber<1 || file.LastChunkNumber>=chunkNumber) return ChunkHelper.Fail<Object>("chunk number is not valid");            
        
        string chunkFileName = $"{fileId}_chunk_{chunkNumber}";
        string chunkPath = Path.Combine(this.TempFolder,fileId.ToString(), chunkFileName);
        using(FileStream fs = System.IO.File.Create(chunkPath)){
            fileContent.Position=0;
            await fileContent.CopyToAsync(fs);
        }       
        file.LastChunkNumber=chunkNumber;
        file.LastChunkUploadTime=DateTimeOffset.UtcNow;
        _dbContext.SaveChanges();
        return ChunkHelper.Success<Object>();
    }


    public async Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,byte[] fileContent){

            using (var stream = new MemoryStream(fileContent))
            {
                return await UploadChunkAsync(fileId,chunkNumber,stream);
            }

    }

    public async Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,IEnumerable<byte> fileContent)
    {

            byte[] bytes = fileContent.ToArray();
            using (var stream = new MemoryStream(bytes))
            {

                return await UploadChunkAsync(fileId,chunkNumber,stream);
            }
    }





    private async Task MergeChunksAsync(string destinationFilePath, string[] chunkFilePaths)
    {
        
        var fileLock = _fileLocks.GetOrAdd(destinationFilePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync();
        try{



            var tempFilePath = Path.Combine(this.TempFolder,$"{Guid.NewGuid()}.tmp");
            await using (var destinationStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan| FileOptions.Asynchronous))
            {

                foreach (var chunkFilePath in chunkFilePaths){

                    await _fileHelper.RetryIOAsync(async () =>
                    {
                        await using (var chunkStream = new FileStream(
                            chunkFilePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            4096,
                            FileOptions.SequentialScan | FileOptions.Asynchronous))
                        {
                            await chunkStream.CopyToAsync(destinationStream);
                        }
                        
                    }, maxRetries: 3, delayMs: 100);


                }
                File.Move(tempFilePath, destinationFilePath, overwrite: true);
                await _fileHelper.DeleteFilesAsync(chunkFilePaths);
                

            }



        }
        finally{

            fileLock.Release();
            _fileLocks.TryRemove(destinationFilePath, out _);

        } 

        
    }


    public async Task<ChunkResponse<string>> ChunkUploadCompleted(Guid fileId,string fileName){

        var file=await _dbContext.Set<FileModel>().FirstOrDefaultAsync(x=>x.Id==fileId);
        if(file is null) return ChunkHelper.Fail<string>("file is not exist");
        string LastFileName=Path.Combine(this.TempFolder,fileName);
        string[] chunks=Directory.GetFiles(Path.Combine(this.TempFolder,fileName)).OrderBy(x=>x.Split($"{fileId}_chunk_")[1]).ToArray();
        await MergeChunksAsync(LastFileName,chunks);
        await _dbContext.Set<FileModel>().Where(x=>x.Id==fileId).ExecuteDeleteAsync();                                         
        return ChunkHelper.Success<string>("this is your file name",LastFileName);


    }







    public async Task<ChunkResponse<int>> GetLastChunk(Guid fileId){

        var file=await _dbContext.Set<FileModel>().FirstOrDefaultAsync(x=>x.Id==fileId);
        if(file is null) return ChunkHelper.Fail<int>("file is not exist");
        return ChunkHelper.Success("this is data",file.LastChunkNumber);
    }

    
}
