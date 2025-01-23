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
    
        private readonly ChunkUploadSettings _settings = new() { TempFolder = "/home/ali/Desktop/ChunckUpload" };
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

    [Fact]
    public async Task Upload_ShouldFail_WithMissingChunks()
    {
        var sut = CreateSUT();
        var fileId = await sut.StartUploadAsync("incomplete.txt");
        
        // Upload chunks 1 and 3 (skip 2)
        await sut.UploadChunkAsync(fileId, 1, new byte[1024]);
        await sut.UploadChunkAsync(fileId, 3, new byte[1024]);

        var result = await sut.ChunkUploadCompleted(fileId);
        
        Assert.False(result.Status);
        Assert.Contains("you should first upload lost chunk", result.Message);
    }

    [Fact]
    public async Task CancelUpload_ShouldCleanup_Resources()
    {
        var sut = CreateSUT();
        var fileId = await sut.StartUploadAsync("cancel_test.txt");
        await sut.UploadChunkAsync(fileId, 1, new byte[512]);
        
        var cancelResult = await sut.CancelUploadAsync(fileId);
        
        Assert.True(cancelResult.Status);
        Assert.False(Directory.Exists(Path.Combine(_settings.TempFolder, fileId.ToString())));
        Assert.False(sut.GetLastChunk(fileId).GetAwaiter().GetResult().Status);
    }


 [Fact]
    public async Task GetLostChunks_ShouldIdentify_MissingSequences()
    {
        var sut = CreateSUT();
        var fileId = await sut.StartUploadAsync("sequence_test.bin");
        
        // Upload non-sequential chunks
        await sut.UploadChunkAsync(fileId, 1, new byte[1024]);
        await sut.UploadChunkAsync(fileId, 3, new byte[1024]);
        await sut.UploadChunkAsync(fileId, 5, new byte[1024]);

        var lostChunks = await sut.GetLostChunkNumber(fileId);
        
        Assert.True(lostChunks.Status);
        Assert.Equal(new List<int> {2, 4}, lostChunks.Data);
    }

}
