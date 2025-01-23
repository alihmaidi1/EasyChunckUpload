using System.Collections.Concurrent;
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUpload.Services.FileService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;



namespace EasyChunkUpload.Services.ChunkUpload;

/// <summary>
/// Service responsible for managing chunked file uploads including initialization, chunk processing,
/// merging, and cleanup operations.
/// </summary>

public class ChunkUpload : IChunkUpload
{
    private readonly DbContext _dbContext;

    private string TempFolder;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

    private readonly IFileHelper _fileHelper;
    
    private readonly IFileService fileService;
    /// <summary>
    /// Initializes a new instance of the ChunkUpload service
    /// </summary>
    /// <param name="fileService">File metadata service</param>
    /// <param name="dbContext">Database context for upload tracking</param>
    /// <param name="fileHelper">File system operations helper</param>
    /// <param name="chunkSetting">Upload configuration settings</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is null</exception>

    public ChunkUpload(IFileService fileService,DbContext dbContext,IFileHelper fileHelper,IOptions<ChunkUploadSettings> chunkSetting){

        this.fileService=fileService??throw new ArgumentException();
        _dbContext=dbContext??throw new ArgumentException();
        _fileHelper=fileHelper??throw new ArgumentException();        
        this.TempFolder=chunkSetting.Value.TempFolder;
    
    }


    /// <summary>
    /// Initializes a new chunked upload session
    /// </summary>
    /// <param name="fileName">Original filename With extension (will be sanitized)</param>
    /// <returns>Upload session GUID</returns>
    /// <exception cref="ArgumentException">Thrown for invalid filenames</exception>
    /// <exception cref="DbUpdateException">Thrown on database write failure</exception>

    public async Task<Guid> StartUploadAsync(string fileName)
    {

        
        ChunkHelper.IsValidFileName(fileName);
        var id=Guid.NewGuid();
        await _dbContext.Set<FileModel>().AddAsync(new FileModel{
            
            Id=id,
            FileName=fileName

        });
        await _dbContext.SaveChangesAsync();
        var fileDirectory=Path.Combine(this.TempFolder,id.ToString());
        Directory.CreateDirectory(fileDirectory);
        return id;
    
    }


    /// <summary>
    /// Processes an uploaded file chunk from Stream source
    /// </summary>
    /// <param name="fileId">Upload session ID</param>
    /// <param name="chunkNumber">Sequential chunk number (1-based)</param>
    /// <param name="fileContent">Chunk data stream</param>
    /// <returns>Chunk operation status</returns>


    public async Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,Stream fileContent)
    {

        if(fileContent==null||fileContent.Length==0)
            return ChunkHelper.Fail<Object>("File content is empty.");
        
        var file=await fileService.GetFile(fileId);
        if(file is null) return ChunkHelper.Fail<Object>("file id is not exists");        
        
        if(!ChunkHelper.IsValidChunkNumber(chunkNumber,file,Path.Combine(TempFolder,fileId.ToString()))) return ChunkHelper.Fail<Object>("chunk number is not valid");                    
        string chunkPath = Path.Combine(this.TempFolder,fileId.ToString(), ChunkHelper.GetChunkNamePattern(fileId.ToString(),chunkNumber.ToString()));
        using(FileStream fs = System.IO.File.Create(chunkPath)){
            fileContent.Position=0;
            await fileContent.CopyToAsync(fs);
        }       
        file.LastChunkNumber=chunkNumber;
        file.LastChunkUploadTime=DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();
        return ChunkHelper.Success<Object>();
    }

    /// <summary>
    /// Processes an uploaded file chunk from byte array
    /// </summary>
    /// <inheritdoc cref="UploadChunkAsync(Guid,int,Stream)"/>
    public async Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,byte[] fileContent){

            using (var stream = new MemoryStream(fileContent))
            {
                return await UploadChunkAsync(fileId,chunkNumber,stream);
            }

    }


    /// <summary>
    /// Processes an uploaded file chunk from byte collection
    /// </summary>
    /// <inheritdoc cref="UploadChunkAsync(Guid,int,Stream)"/>

    public async Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,IEnumerable<byte> fileContent)
    {

            byte[] bytes = fileContent.ToArray();
            using (var stream = new MemoryStream(bytes))
            {

                return await UploadChunkAsync(fileId,chunkNumber,stream);
            }
    }



    /// <summary>
    /// Merges uploaded chunks into final file
    /// </summary>
    /// <param name="destinationFilePath">Target file path</param>
    /// <param name="chunkFilePaths">Ordered list of chunk paths</param>
    /// <remarks>
    /// Implements concurrency control using semaphore per destination file.
    /// Uses atomic file operations with temp file to ensure data integrity.
    /// </remarks>
    /// <exception cref="IOException">Thrown for file system errors</exception>

    public virtual async  Task MergeChunksAsync(string destinationFilePath, string[] chunkFilePaths)
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

    /// <summary>
    /// Completes the chunked upload process
    /// </summary>
    /// <param name="fileId">Upload session ID</param>
    /// <returns>Final file path or error details</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when chunks are missing
    /// </exception>
    public async Task<ChunkResponse<string>> ChunkUploadCompleted(Guid fileId){

        var file=await fileService.GetFile(fileId);
        if(file is null) return ChunkHelper.Fail<string>("file is not exist");
        string LastFileName=Path.Combine(TempFolder,file.FileName);
        string[] chunks=Directory.GetFiles(Path.Combine(this.TempFolder,fileId.ToString())).OrderBy(x=>x.Split(ChunkHelper.GetChunkNamePattern(fileId.ToString()))[1]).ToArray();
        if(GetLostChunkNumber(chunks,fileId).GetAwaiter().GetResult().Count==0){

            await MergeChunksAsync(LastFileName,chunks);
            await fileService.DeleteFile(fileId);
            Directory.Delete(Path.Combine(this.TempFolder,fileId.ToString()));
            return ChunkHelper.Success<string>("this is your file name",LastFileName);

        }else{

            return ChunkHelper.Fail<string>("you should first upload lost chunk"); 
        }


    }






    /// <summary>
    /// Retrieves last uploaded chunk number for a session
    /// </summary>
    /// <param name="fileId">Upload session ID</param>
    /// <returns>Chunk number or error status</returns>

    public async Task<ChunkResponse<int>> GetLastChunk(Guid fileId){

        var file=await fileService.GetLastChunk(fileId);
        if(!file.Status) return ChunkHelper.Fail<int>("file is not exist");
        return ChunkHelper.Success("this is data",file.Data);
    }



    /// <summary>
    /// Cancels an active upload session
    /// </summary>
    /// <param name="fileId">Upload session ID</param>
    /// <returns>Operation status</returns>
    public async Task<ChunkResponse<bool>> CancelUploadAsync(Guid fileId)
    {

        if(await fileService.IsExists(fileId)){

        await fileService.DeleteFile(fileId);
        await _fileHelper.DeleteDirectory(Path.Combine(this.TempFolder,fileId.ToString()));
        return ChunkHelper.Success("Upload Canceled Successfully",true);
            
        }else{

        return ChunkHelper.Fail("File is not exists",false);

        }
        
    }


    /// <summary>
    /// Identifies missing chunks in upload sequence
    /// </summary>
    /// <param name="fileId">Upload session ID</param>
    /// <returns>List of missing chunk numbers</returns>

    public async Task<ChunkResponse<List<int>>> GetLostChunkNumber(Guid fileId){

        var file=await fileService.GetFile(fileId);
        if(file is null) return ChunkHelper.Fail<List<int>>("file is not exists");
        var path=Path.Combine(TempFolder,fileId.ToString());
        var existsChunk=Directory
        .GetFiles(path)
        .Select(x=>x.Split(ChunkHelper.GetChunkNamePattern(fileId.ToString()))[1])
        .Select(x=>Int32.Parse(x))
        .Order()
        .ToList();
        
        List<int> lostChunk=new List<int>();
        int counter=1;
        for (int i = 0; i < existsChunk.Count;)
        {
            if(existsChunk[i]==counter){

                i++;
                counter++;
            }else{

                lostChunk.Add(counter);
                counter++;

            }            
        }
        
        return ChunkHelper.Success("this is lost chunk",lostChunk);

    }


    /// <summary>
    /// Internal method to identify missing chunks from existing chunk list
    /// </summary>
    /// <param name="existsChunk">Array of existing chunk paths</param>
    /// <param name="fileId">Upload session ID</param>
    /// <returns>List of missing chunk numbers</returns>
    private async Task<List<int>> GetLostChunkNumber(string[] existsChunk,Guid fileId){
        
        var existsChunkAsInt=existsChunk
        .Select(x=>x.Split(ChunkHelper.GetChunkNamePattern(fileId.ToString()))[1])
        .Select(x=>Int32.Parse(x))
        .ToList();        
        List<int> lostChunk=new List<int>();
        int counter=1;
        for (int i = 0; i < existsChunkAsInt.Count;)
        {
            if(existsChunkAsInt[i]==counter){

                i++;
                counter++;
            }else{

                lostChunk.Add(counter);
                counter++;

            }            
        }
        
        return lostChunk;

    }



}
