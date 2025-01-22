using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;
using Microsoft.EntityFrameworkCore;

namespace EasyChunkUpload.Services.FileService;

public class FileService : IFileService
{

    private readonly DbContext dbContext;
    public FileService(DbContext dbContext){

        this.dbContext=dbContext;
        

    }
    public async Task<bool> DeleteFile(Guid fileId)
    {

        await dbContext.Set<FileModel>().Where(x=>x.Id==fileId).ExecuteDeleteAsync();
        return true;
    }

    public async Task<FileModel?> GetFile(Guid fileId){


        return await dbContext.Set<FileModel>().FirstOrDefaultAsync(x=>x.Id==fileId);
        
    }


    public async Task<bool> IsExists(Guid fileId){


        return await dbContext.Set<FileModel>().AnyAsync(x=>x.Id==fileId);

    }

    public async Task<ChunkResponse<int>> GetLastChunk(Guid fileId){

        var file=await dbContext.Set<FileModel>().FirstOrDefaultAsync(x=>x.Id==fileId);
        if(file is null){

        return ChunkHelper.Fail<int>("file is not exists");


        }else{


            return ChunkHelper.Success<int>("file is not exists",file.LastChunkNumber);

        }


    }   


}
