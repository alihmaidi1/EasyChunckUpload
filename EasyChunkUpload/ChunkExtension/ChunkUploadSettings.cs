namespace EasyChunkUpload.ChunkExtension;
public class ChunkUploadSettings
{


    public string TempFolder{get;set;}


    public string CleanupInterval { get; set; }
    
    public bool KeepCompletedFiles { get; set; }

    public string CompletedFilesExpiration { get; set; }

}