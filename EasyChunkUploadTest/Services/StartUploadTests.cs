using EasyChunkUpload.Model;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUploadTest.Base;
using Microsoft.Extensions.Options;
using Moq;


namespace EasyChunkUploadTest.Services;

public class StartUploadTests: BaseTest
{
    [Fact]
    public async Task StartUpload_ValidFileName_CreatesSession()
    {
        
        // Given        
        var mockFileHelper = new Mock<IFileHelper>();  
        var service = new ChunkUpload(this.DbMock.Object, mockFileHelper.Object, Options.Create(this.Settings));           
        
        // When
        var sessionId =  await service.StartUploadAsync("test.pdf");             
        
        //Then
        Assert.NotEqual(Guid.Empty,sessionId);

        DbSetMock.Verify(
                d => d.AddAsync(
                    It.Is<FileModel>(f => 
                        f.Id == sessionId && 
                        f.FileName == "test.pdf"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        DbMock.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(Directory.Exists(Path.Combine(this.Settings.TempFolder,sessionId.ToString())));

    }


}
