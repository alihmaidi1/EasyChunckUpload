using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUploadTest.Base;
using Microsoft.Extensions.Options;
using Moq;

namespace EasyChunkUploadTest.Services;

public class GetLastChunkTest:BaseTest
{

    [Fact]
    public async Task GetLastChunk_FileExists_ReturnsLastChunkNumber()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var expectedChunkNumber = 5;
        var expectedResponse=ChunkHelper.Success<int>("this is last chunk",expectedChunkNumber);        
        MockFileService.Setup(x=>x.GetLastChunk(fileId)).ReturnsAsync(expectedResponse);
        
        var service = new ChunkUpload(MockFileService.Object,DbMock.Object,MockFileHelper.Object, Options.Create(this.Settings));

        // Act
        var result = await service.GetLastChunk(fileId);

        // Assert
        Assert.True(result.Status);
        Assert.Equal(expectedChunkNumber, result.Data);
    }



    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetLastChunk_FileNotExists_ReturnsFailure(Guid fileId)
    {

        
        var expectedResponse=ChunkHelper.Fail<int>("file not exists");        
        MockFileService.Setup(x=>x.GetLastChunk(fileId)).ReturnsAsync(expectedResponse);
        
        var service = new ChunkUpload(MockFileService.Object,DbMock.Object,MockFileHelper.Object, Options.Create(this.Settings));

        // Act
        var result = await service.GetLastChunk(fileId);

        // Assert
        Assert.False(result.Status);
        Assert.Equal("file is not exist", result.Message);
    }
    
}
