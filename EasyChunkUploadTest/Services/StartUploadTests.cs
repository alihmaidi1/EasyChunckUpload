using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUploadTest.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;


namespace EasyChunkUploadTest.Services;

public class StartUploadTests: BaseTest
{
    [Theory]
    [InlineData("document.pdf")]
    [InlineData("file_123.txt")]
    [InlineData("image-2023.jpg")]
    public async Task StartUpload_ValidFileName_CreatesSession(string fileName)
    {
        
        // Given        
        var mockFileHelper = new Mock<IFileHelper>();  
        var service = new ChunkUpload(this.DbMock.Object, mockFileHelper.Object, Options.Create(this.Settings));           
        
        // When
        var sessionId =  await service.StartUploadAsync(fileName);             
        
        //Then
        Assert.NotEqual(Guid.Empty,sessionId);

        DbSetMock.Verify(
                d => d.AddAsync(
                    It.Is<FileModel>(f => 
                        f.Id == sessionId && 
                        f.FileName == fileName
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        DbMock.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(Directory.Exists(Path.Combine(this.Settings.TempFolder,sessionId.ToString())));

    }



    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task StartUpload_InvalidFileName_ThrowsException(string fileName)
    {
        var service = new ChunkUpload(this.DbMock.Object, new Mock<IFileHelper>().Object, Options.Create(this.Settings));
        
        await Assert.ThrowsAsync<ArgumentException>(() => service.StartUploadAsync(fileName));
    }





}
