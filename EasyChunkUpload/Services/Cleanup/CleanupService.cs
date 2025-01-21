
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EasyChunkUpload.Services.Cleanup;
public class CleanupService : ICleanupService
{

    public readonly DbContext _dbContext;

    public readonly string TempFolder;
    private readonly int ExpiredAt;
    public CleanupService(DbContext _dbContext,IOptions<ChunkUploadSettings> chunkOptions){

        this._dbContext=_dbContext;
        ExpiredAt=chunkOptions.Value.CompletedFilesExpiration;
        TempFolder=chunkOptions.Value.TempFolder;
        
    }
    public async Task CleanUpExpiredUploadsAsync()
    {

        List<Guid> AllExpiredFile=await _dbContext
        .Set<FileModel>()
        .Where(x=>x.LastChunkUploadTime.AddSeconds(ExpiredAt)>=DateTimeOffset.UtcNow)
        .Select(x=>x.Id)
        .ToListAsync();
        
        Parallel.ForEach(AllExpiredFile,item=>{


            Directory.Delete(Path.Combine(TempFolder,item.ToString()),true);


        });


        await _dbContext.Set<FileModel>().Where(x=>AllExpiredFile.Contains(x.Id)).ExecuteDeleteAsync();


    }

            
    
}
