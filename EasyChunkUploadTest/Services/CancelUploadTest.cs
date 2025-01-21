using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUploadTest.Base;
using Microsoft.Extensions.Options;
using Moq;


namespace EasyChunkUploadTest.Services;

public class CancelUploadTest:BaseTest
{
    [Fact]
    public async Task CancelUpload_ValidId_RemovesAllArtifacts()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var tempDir = Path.Combine(this.Settings.TempFolder, fileId.ToString());        
        var service = new ChunkUpload(MockFileService.Object,DbMock.Object, this.MockFileHelper.Object, Options.Create(this.Settings));           

        MockFileService.Setup(x=>x.IsExists(fileId)).ReturnsAsync(true);

        // When
        ChunkResponse<bool> IsCanceled =  await service.CancelUploadAsync(fileId);             

        //Then
        Assert.True(IsCanceled.Status);
        MockFileService.Verify(x=>x.DeleteFile(fileId),Times.Once);
        MockFileHelper.Verify(f=>f.DeleteDirectory(tempDir),Times.Once);
       
        
    }

    [Fact]
    public async Task CancelUpload_InValidId_RemovesAllArtifacts()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var tempDir = Path.Combine(this.Settings.TempFolder, fileId.ToString());        
        var service = new ChunkUpload(MockFileService.Object,DbMock.Object, this.MockFileHelper.Object, Options.Create(this.Settings));           

        MockFileService.Setup(x=>x.IsExists(fileId)).ReturnsAsync(false);

        // When
        ChunkResponse<bool> IsCanceled =  await service.CancelUploadAsync(fileId);             

        //Then
        Assert.False(IsCanceled.Status);
       
        
    }

}
