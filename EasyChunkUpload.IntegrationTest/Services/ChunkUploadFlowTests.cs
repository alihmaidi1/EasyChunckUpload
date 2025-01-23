using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.IntegrationTest.Base;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUpload.Services.FileService;
using Microsoft.Extensions.Options;

namespace EasyChunkUpload.IntegrationTest.Services;
public class ChunkUploadFlowTests:IClassFixture<DatabaseFixture>
{

    private readonly DatabaseFixture _fixture;
    
        private readonly ChunkUploadSettings _settings = new() { TempFolder = "test_uploads" };
    public ChunkUploadFlowTests(DatabaseFixture _fixture){

        this._fixture=_fixture;

    }

    private ChunkUpload CreateSUT()
    {
        var context = new ChunkUploadContext(_fixture.Options);
        var fileService = new FileService(context);
        var fileHelper = new FileHelper();
        
        return new ChunkUpload(
            fileService,
            context,
            fileHelper,
            Options.Create(_settings)
        );
    }


     [Fact]
    public async Task FullUploadFlow_ShouldWork_WithValidChunks()
    {
        // Arrange
        var sut = CreateSUT();
        var fileName = "testfile.zip";
        var chunkSize = 1024 * 1024; // 1MB
        var totalChunks = 5;

        // Act - Start Upload
        var fileId = await sut.StartUploadAsync(fileName);        
        
        // Upload Chunks
        for (int i = 1; i <= totalChunks; i++)
        {
            var chunkData = ChunkTestHelpers.GenerateTestChunk(chunkSize);
            var result = await sut.UploadChunkAsync(fileId, i, chunkData);
            Assert.True(result.Status);
        }

        // Complete Upload
        var completeResult = await sut.ChunkUploadCompleted(fileId);
        
        // Assert
        Assert.True(completeResult.Status);
        Assert.True(File.Exists(completeResult.Data));
        Assert.False(Directory.Exists(Path.Combine(_settings.TempFolder, fileId.ToString())));
    }


}
