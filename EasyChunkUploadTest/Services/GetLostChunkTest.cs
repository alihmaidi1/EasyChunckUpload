
using EasyChunkUpload.ChunkExtension;
using EasyChunkUpload.Model;
using EasyChunkUpload.Services.ChunkUpload;
using EasyChunkUploadTest.Base;
using Microsoft.Extensions.Options;
using Moq;

namespace EasyChunkUploadTest.Services;
public class GetLostChunkTest: BaseTest
{

    [Fact]
    public async Task LostChunk_ValidInput_GetLostChunk()
    {

        // Given        
        var fileId = Guid.NewGuid();
        var folderName=Path.Combine(Settings.TempFolder,fileId.ToString());
        Directory.CreateDirectory(folderName);        
        File.WriteAllText(Path.Combine(folderName,ChunkHelper.GetChunkNamePattern(fileId.ToString(),"1")), "test1");
        File.WriteAllText(Path.Combine(folderName,ChunkHelper.GetChunkNamePattern(fileId.ToString(),"3")), "test3");
        File.WriteAllText(Path.Combine(folderName,ChunkHelper.GetChunkNamePattern(fileId.ToString(),"6")), "test6");

        MockFileService.Setup(x=>x.GetFile(fileId)).ReturnsAsync(new FileModel{

            Id=fileId,
            FileName="test.txt",
            LastChunkNumber=6
        });
        var service = new ChunkUpload(MockFileService.Object,DbMock.Object, MockFileHelper.Object, Options.Create(this.Settings));                   
        List<int> expectedLostChunk=new List<int>{

            2,4,5

        };


        // Act

        var result = await service.GetLostChunkNumber(fileId);

        // Assert
        Assert.True(result.Status);
        Assert.Equal(expectedLostChunk,result.Data);
        

    }


    
}
