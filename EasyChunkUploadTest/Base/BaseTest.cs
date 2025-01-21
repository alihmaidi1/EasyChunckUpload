using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUpload.Services.FileService;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EasyChunkUploadTest.Base;

public class BaseTest
{
    protected Mock<DbContext> DbMock { get; }

    protected Mock<IFileHelper> MockFileHelper{get;} 
    protected Mock<IFileService> MockFileService{get;} 

    protected Mock<DbSet<FileModel>> DbSetMock { get; }

    protected ChunkUploadSettings Settings { get; } = new ChunkUploadSettings { TempFolder = "test-uploads" };

    protected BaseTest()
    {
        // Initialize Mocks
        DbSetMock = new Mock<DbSet<FileModel>>();
        DbMock = new Mock<DbContext>();
        MockFileHelper=new Mock<IFileHelper>();
        MockFileService=new Mock<IFileService>();
        DbMock.Setup(db => db.Set<FileModel>()).Returns(DbSetMock.Object);
    }


}
