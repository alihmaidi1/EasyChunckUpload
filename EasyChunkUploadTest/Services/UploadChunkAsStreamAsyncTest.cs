using EasyChunkUpload.Model;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUploadTest.Base;
using Microsoft.Extensions.Options;
using Moq;

namespace EasyChunkUploadTest.Services;

public class UploadChunkAsStreamAsyncTest: BaseTest
{

    [Theory]
    [InlineData(5)]
    [InlineData(2)]

    public async Task UploadChunk_ValidInput_SavesChunk(int chunkNumber)
    {

        // Given        
        var fileId = Guid.NewGuid();
        var folderName=Path.Combine(Settings.TempFolder,fileId.ToString());
        Directory.CreateDirectory(folderName);        
        File.WriteAllText(Path.Combine(folderName,$"{fileId}_chunk_1"), "test1");
        File.WriteAllText(Path.Combine(folderName,$"{fileId}_chunk_3"), "test3");
        File.WriteAllText(Path.Combine(folderName,$"{fileId}_chunk_4"), "test4");

        string chunkFileName = $"{fileId}_chunk_{chunkNumber}";
        MockFileService.Setup(x=>x.GetFile(fileId)).ReturnsAsync(new FileModel{

            Id=fileId,
            FileName="test.txt",
            LastChunkNumber=4
        });
        var service = new ChunkUpload(MockFileService.Object,DbMock.Object, MockFileHelper.Object, Options.Create(this.Settings));                   
        using var chunkData = new MemoryStream();
        chunkData.SetLength(2048);

        // Act

        var result = await service.UploadChunkAsync(fileId, chunkNumber, chunkData);

        // Assert
        Assert.True(result.Status);
        DbMock.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(File.Exists(Path.Combine(Settings.TempFolder,fileId.ToString(),chunkFileName)));


    }

    [Fact]
    public async Task UploadChunk_InvalidFileId_ReturnsError()
    {
        var fileId = Guid.NewGuid();
        MockFileService.Setup(x=>x.GetFile(fileId)).ReturnsAsync(null as FileModel);
        var service = new ChunkUpload(MockFileService.Object,DbMock.Object, MockFileHelper.Object, Options.Create(this.Settings));                   
        using var chunkData = new MemoryStream();
        chunkData.SetLength(2048);


        // Act
        var result = await service.UploadChunkAsync(fileId, 5, chunkData);


        // Assert
        Assert.False(result.Status);

    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]

    public async Task UploadChunk_InvalidChunkNumber_ReturnsError(int chunkNumber)
    {


        //Give
        var fileId = Guid.NewGuid();        
        MockFileService.Setup(x=>x.GetFile(fileId)).ReturnsAsync(new FileModel{

            Id=fileId,
            FileName="test.txt",
            LastChunkNumber=4
        });
       

        var folderName=Path.Combine(Settings.TempFolder,fileId.ToString());
        Directory.CreateDirectory(folderName);        
        File.WriteAllText(Path.Combine(folderName,$"{fileId}_chunk_1"), "test1");
        File.WriteAllText(Path.Combine(folderName,$"{fileId}_chunk_3"), "test2");
        File.WriteAllText(Path.Combine(folderName,$"{fileId}_chunk_4"), "test4");




        var service = new ChunkUpload(MockFileService.Object,DbMock.Object, MockFileHelper.Object, Options.Create(this.Settings));                   
        using var chunkData = new MemoryStream();
        chunkData.SetLength(2048);


        // Act
        var result = await service.UploadChunkAsync(fileId, chunkNumber, chunkData);


        // Assert
        Assert.False(result.Status);

     
    }




}