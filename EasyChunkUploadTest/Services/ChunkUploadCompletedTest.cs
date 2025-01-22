using EasyChunkUpload.Model;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUploadTest.Base;
using EasyChunkUploadTest.MoqService;
using Microsoft.Extensions.Options;
using Moq;
namespace EasyChunkUploadTest.Services;

public class ChunkUploadCompletedTest:BaseTest
{

    private readonly IFileHelper fileHelper;

    public ChunkUploadCompletedTest(){

        fileHelper=new FileHelper();
        

    }
    [Fact]
    public async Task CompleteUpload_ValidId_ReturnsFinalPath()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var tempPath = Path.Combine(this.Settings.TempFolder, fileId.ToString());     
        Directory.CreateDirectory(tempPath);     
        MockFileService.Setup(x=>x.GetFile(fileId)).ReturnsAsync(new FileModel{

            Id=fileId,
            FileName="test.txt"

        });
        var service = new ChunkUploadWhenMergeService(
            MockFileService.Object,
            DbMock.Object,
            this.fileHelper,
            Options.Create(this.Settings)
        );

        // Act
        var result = await service.ChunkUploadCompleted(fileId);
        
        // Assert
        Assert.True(result.Status);
        Assert.StartsWith(Path.Combine(Settings.TempFolder,"test.txt"),result.Data);



        Assert.Empty(Directory.GetFiles(tempPath));
    }

    [Fact]
    public async Task CompleteUpload_UnValidId_ReturnsFinalPath()
    {
        // Arrange
        var fileId = Guid.NewGuid();


        MockFileService.Setup(x=>x.GetFile(fileId)).ReturnsAsync(null as FileModel);
        var service = new ChunkUpload(
            MockFileService.Object,
            DbMock.Object,
            this.MockFileHelper.Object,
            Options.Create(this.Settings)
        );

        // Act
        var result = await service.ChunkUploadCompleted(fileId);
        
        // Assert
        Assert.False(result.Status);

    }

}
