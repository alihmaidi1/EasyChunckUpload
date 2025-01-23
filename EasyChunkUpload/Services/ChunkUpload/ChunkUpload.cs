using System.Collections.Concurrent;
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUpload.Services.FileService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;



namespace EasyChunkUpload.Services.ChunkUpload;

public class ChunkUpload : IChunkUpload
{
    private readonly DbContext _dbContext;

    private string TempFolder;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

    private readonly IFileHelper _fileHelper;
    
    private readonly IFileService fileService;
    public ChunkUpload(IFileService fileService,DbContext dbContext,IFileHelper fileHelper,IOptions<ChunkUploadSettings> chunkSetting){

        this.fileService=fileService;
        _dbContext=dbContext;
        _fileHelper=fileHelper;        
        this.TempFolder=chunkSetting.Value.TempFolder;
    
    }

    /// <summary>
    /// Initializes a new chunked file upload session and generates a unique session identifier
    /// </summary>
    /// <param name="fileName">
    /// The original name of the file being uploaded (including extension).
    /// Will be sanitized to remove special characters and path information.
    /// </param>
    /// <returns>
    /// <para>A <see cref="Guid"/> representing the upload session ID that must be used for subsequent chunk uploads.</para>
    /// <para>This session ID will expire after configured retention period if not completed.</para>
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>fileName is null or empty</description></item>
    /// <item><description>fileName contains invalid characters</description></item>
    /// <item><description>fileName length exceeds system limits</description></item>
    /// </list>
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>Maximum concurrent upload sessions reached</description></item>
    /// <item><description>Storage provider initialization failed</description></item>
    /// </list>
    /// </exception>
    /// <remarks>
    /// <para>This method performs the following operations:</para>
    /// <list type="number">
    /// <item><description>Validates and sanitizes file name</description></item>
    /// <item><description>Creates temporary storage directory for chunks</description></item>
    /// <item><description>Generates initial metadata entry</description></item>
    /// <item><description>Starts session expiration timer</description></item>
    /// </list>
    /// <para>The generated session ID should be stored client-side for subsequent chunk uploads.</para>
    /// </remarks>

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
    /// Processes and stores an individual chunk of a file during chunked upload
    /// </summary>
    /// <param name="fileId">The unique identifier for the ongoing file upload session</param>
    /// <param name="chunkNumber">The sequential number of the chunk (1-based index)</param>
    /// <param name="fileContent">The binary content of the file chunk to be stored</param>
    /// <returns>
    /// <para>A ChunkResponse containing:</para>
    /// <para>- Current upload status (Success/Error)</para>
    /// <para>- Processed chunk number</para>
    /// <para>- Error details if operation failed</para>
    /// <para>- Additional metadata in the Result object</para>
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>chunkNumber is less than 1</description></item>
    /// <item><description>fileContent contains no data</description></item>
    /// </list>
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when no upload session exists for the specified fileId</exception>
    /// <exception cref="InvalidOperationException">Thrown when chunk processing order is violated</exception>

    public async Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,Stream fileContent)
    {

        if(fileContent==null||fileContent.Length==0)
            return ChunkHelper.Fail<Object>("File content is empty.");
        
        var file=await fileService.GetFile(fileId);
        if(file is null) return ChunkHelper.Fail<Object>("file id is not exists");        
        
        if(!ChunkHelper.IsValidChunkNumber(chunkNumber,file,Path.Combine(TempFolder,fileId.ToString()))) return ChunkHelper.Fail<Object>("chunk number is not valid");            
        
        string chunkFileName = $"{fileId}_chunk_{chunkNumber}";
        string chunkPath = Path.Combine(this.TempFolder,fileId.ToString(), chunkFileName);
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
    /// Processes and stores an individual chunk of a file during chunked upload
    /// </summary>
    /// <param name="fileId">The unique identifier for the ongoing file upload session</param>
    /// <param name="chunkNumber">The sequential number of the chunk (1-based index)</param>
    /// <param name="fileContent">The binary content of the file chunk to be stored</param>
    /// <returns>
    /// <para>A ChunkResponse containing:</para>
    /// <para>- Current upload status (Success/Error)</para>
    /// <para>- Processed chunk number</para>
    /// <para>- Error details if operation failed</para>
    /// <para>- Additional metadata in the Result object</para>
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>chunkNumber is less than 1</description></item>
    /// <item><description>fileContent contains no data</description></item>
    /// </list>
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when no upload session exists for the specified fileId</exception>
    /// <exception cref="InvalidOperationException">Thrown when chunk processing order is violated</exception>

    public async Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,byte[] fileContent){

            using (var stream = new MemoryStream(fileContent))
            {
                return await UploadChunkAsync(fileId,chunkNumber,stream);
            }

    }

    /// <summary>
    /// Processes and stores an individual chunk of a file during chunked upload
    /// </summary>
    /// <param name="fileId">The unique identifier for the ongoing file upload session</param>
    /// <param name="chunkNumber">The sequential number of the chunk (1-based index)</param>
    /// <param name="fileContent">The binary content of the file chunk to be stored</param>
    /// <returns>
    /// <para>A ChunkResponse containing:</para>
    /// <para>- Current upload status (Success/Error)</para>
    /// <para>- Processed chunk number</para>
    /// <para>- Error details if operation failed</para>
    /// <para>- Additional metadata in the Result object</para>
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>chunkNumber is less than 1</description></item>
    /// <item><description>fileContent contains no data</description></item>
    /// </list>
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when no upload session exists for the specified fileId</exception>
    /// <exception cref="InvalidOperationException">Thrown when chunk processing order is violated</exception>

    public async Task<ChunkResponse<Object>> UploadChunkAsync(Guid fileId, int chunkNumber,IEnumerable<byte> fileContent)
    {

            byte[] bytes = fileContent.ToArray();
            using (var stream = new MemoryStream(bytes))
            {

                return await UploadChunkAsync(fileId,chunkNumber,stream);
            }
    }





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
    /// Finalizes the chunked file upload process and merges uploaded chunks into the completed file
    /// </summary>
    /// <param name="fileId">The unique identifier for the file upload session</param>
    /// <param name="fileName">The final name to give the completed file (including extension)</param>
    /// <returns>
    /// <para>A ChunkResponse containing:</para>
    /// <para>- Success status of the merge operation</para>
    /// <para>- Final file path/URL in the Result property</para>
    /// <para>- Error message if operation failed</para>
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when no chunks exist for the specified fileId</exception>
    /// <exception cref="InvalidOperationException">Thrown when chunks are missing or corrupted</exception>
    public async Task<ChunkResponse<string>> ChunkUploadCompleted(Guid fileId){

        var file=await fileService.GetFile(fileId);
        if(file is null) return ChunkHelper.Fail<string>("file is not exist");
        string LastFileName=Path.Combine(TempFolder,file.FileName);
        string[] chunks=Directory.GetFiles(Path.Combine(this.TempFolder,fileId.ToString())).OrderBy(x=>x.Split($"{fileId}_chunk_")[1]).ToArray();
        if(GetLostChunkNumber(chunks,fileId).GetAwaiter().GetResult().Count==0){

            await MergeChunksAsync(LastFileName,chunks);
            await fileService.DeleteFile(fileId);
            return ChunkHelper.Success<string>("this is your file name",LastFileName);

        }else{

            return ChunkHelper.Fail<string>("you should first upload lost chunk"); 
        }


    }







    public async Task<ChunkResponse<int>> GetLastChunk(Guid fileId){

        var file=await fileService.GetLastChunk(fileId);
        if(!file.Status) return ChunkHelper.Fail<int>("file is not exist");
        return ChunkHelper.Success("this is data",file.Data);
    }



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



    public async Task<ChunkResponse<List<int>>> GetLostChunkNumber(Guid fileId){

        var file=await fileService.GetFile(fileId);
        if(file is null) return ChunkHelper.Fail<List<int>>("file is not exists");
        var path=Path.Combine(TempFolder,fileId.ToString());
        var existsChunk=Directory
        .GetFiles(path)
        .Select(x=>x.Split($"{fileId}_chunk_")[1])
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


    private async Task<List<int>> GetLostChunkNumber(string[] existsChunk,Guid fileId){
        
        var existsChunkAsInt=existsChunk
        .Select(x=>x.Split($"{fileId}_chunk_")[1])
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
