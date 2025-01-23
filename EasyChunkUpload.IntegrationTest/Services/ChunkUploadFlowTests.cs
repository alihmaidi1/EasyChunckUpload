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

    


}
