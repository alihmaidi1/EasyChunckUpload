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
    
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
    private readonly ChunkUploadSettings uploadSettings;

    private readonly IFileHelper _fileHelper;

    private static readonly object _directoryLock = new();

    public ChunkUpload(DbContext dbContext,IFileHelper fileHelper,IOptions<ChunkUploadSettings> options){

        _dbContext=dbContext;
        uploadSettings=options.Value??new ChunkUploadSettings();
        _fileHelper=fileHelper;
    }

    public async Task<Guid> StartUploadAsync(string fileName)
    {
        var id=Guid.NewGuid();
        await _dbContext.Set<FileModel>().AddAsync(new FileModel{
            
            Id=id,
            FileName=fileName

        });
        var fileDirectory=Path.Combine(this.uploadSettings.TempFolder,id.ToString());
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
        string chunkPath = Path.Combine(this.uploadSettings.TempFolder,fileId.ToString(), chunkFileName);
        using(FileStream fs = System.IO.File.Create(chunkPath)){
            fileContent.Position=0;
            await fileContent.CopyToAsync(fs);
        }       
        file.LastChunkNumber=chunkNumber;
        file.LastChunkUploadTime=DateTimeOffset.UtcNow;
        _dbContext.SaveChanges();
        return ChunkHelper.Success<Object>();
    }




    private async Task MergeChunksAsync(string destinationFilePath, string[] chunkFilePaths)
    {
        
        var fileLock = _fileLocks.GetOrAdd(destinationFilePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync();
        try{

            var directory = Path.GetDirectoryName(destinationFilePath)!;
            lock(_directoryLock){
                if(!Directory.Exists(destinationFilePath)){

                    Directory.CreateDirectory(destinationFilePath);
                }
            }

            var tempFilePath = Path.Combine(directory, $"{Guid.NewGuid()}.tmp");
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





    // public async Task<ChunkResponse<string>> UploadChunk(byte[] fileContent,string uniqueFileName,int chunkNumber,int totalChunks){

    //         using (var stream = new MemoryStream(fileContent))
    //         {
    //             return await UploadChunk(stream,uniqueFileName, chunkNumber, totalChunks);
    //         }

    // }

    // public async Task<ChunkResponse<string>> UploadChunk(IEnumerable<byte> fileContent,string uniqueFileName,int chunkNumber,int totalChunks)
    // {


    //         byte[] bytes = fileContent.ToArray();
    //         using (var stream = new MemoryStream(bytes))
    //         {

    //             return await UploadChunk(stream,uniqueFileName, chunkNumber, totalChunks);
    //         }
    // }










}
