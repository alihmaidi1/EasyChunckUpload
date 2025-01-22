using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUpload.Services.FileService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EasyChunkUploadTest.MoqService;

public class ChunkUploadWhenMergeService : ChunkUpload
{
    public ChunkUploadWhenMergeService(IFileService fileService, DbContext dbContext, IFileHelper fileHelper, IOptions<ChunkUploadSettings> chunkSetting) : base(fileService, dbContext, fileHelper, chunkSetting)
    {
    }
    protected override async  Task MergeChunksAsync(string destinationFilePath, string[] chunkFilePaths){

        
    }

 
}
