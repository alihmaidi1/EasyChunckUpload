using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public async Task<bool> IsExists(Guid fileId){


        return await dbContext.Set<FileModel>().AnyAsync(x=>x.Id==fileId);

    }

}
