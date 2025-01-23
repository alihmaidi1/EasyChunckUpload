using EasyChunkUpload.ChunkExtension;
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
            var path =ChunkHelper.GetChunkNamePattern("testfile",$"{i+1}");
            File.WriteAllText(path, c);
            return path;
        }).ToArray();

        // Act
        await service.MergeChunksAsync("uploads/final.txt", chunkPaths);

        // // Assert
        Assert.True(File.Exists("uploads/final.txt"));
        Assert.Equal("Chunk1ContentChunk2Content", File.OpenText("uploads/final.txt").ReadToEnd());
    }

    [Fact]
    public async Task MergeChunks_Performance_CreatesMergedFile()
    {

        var service = new ChunkUpload(
            this.MockFileService.Object,
            this.DbMock.Object,
            new FileHelper(),
            Options.Create(this.Settings)
        );


        string expectedContent="";
        var chunks=Enumerable.Range(1,1000).Select(x=>{

            var path =ChunkHelper.GetChunkNamePattern("testfile",$"{x+1}");
            File.WriteAllText(path, $"test{x}");
            expectedContent+=$"test{x}";
            return path;

        }).ToArray();

        // Act
        await service.MergeChunksAsync("uploads/final.txt", chunks);


        // Assert
        Assert.True(File.Exists("uploads/final.txt"));                
        Assert.Equal(expectedContent, File.OpenText("uploads/final.txt").ReadToEnd());


    }
    
}
