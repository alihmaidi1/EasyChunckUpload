using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EasyChunkUploadTest.Base;

public class BaseTest
{
    protected Mock<DbContext> DbMock { get; }

    protected Mock<DbSet<FileModel>> DbSetMock { get; }

    protected ChunkUploadSettings Settings { get; } = new ChunkUploadSettings { TempFolder = "test-uploads" };

    protected BaseTest()
    {
        // Initialize Mocks
        DbSetMock = new Mock<DbSet<FileModel>>();
        DbMock = new Mock<DbContext>();

        // Common Setup
        DbMock.Setup(db => db.Set<FileModel>()).Returns(DbSetMock.Object);
    }


}
