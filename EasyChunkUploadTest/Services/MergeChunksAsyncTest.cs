using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUpload.Services.FileHelper;
using EasyChunkUploadTest.Base;
using Microsoft.Extensions.Options;

namespace EasyChunkUploadTest.Services;

public class MergeChunksAsyncTest:BaseTest
{

    [Fact]
    public async Task MergeChunks_ValidInput_CreatesMergedFile()
    {

        var service = new ChunkUpload(
            this.MockFileService.Object,
            this.DbMock.Object,
            new FileHelper(),
            Options.Create(this.Settings)
        );

        var chunks = new[]
        {
            "Chunk1Content",
            "Chunk2Content"
        };

        var chunkPaths = chunks.Select((c, i) =>
        {
            var path = $"testfile_chunk_{i + 1}";
            File.WriteAllText(path, c);
            return path;
        }).ToArray();

        // Act
        await service.MergeChunksAsync("uploads/final.txt", chunkPaths);

        // // Assert
        Assert.True(File.Exists("uploads/final.txt"));
        Assert.Equal("Chunk1ContentChunk2Content", File.OpenText("uploads/final.txt").ReadToEnd());
    }
    
}
