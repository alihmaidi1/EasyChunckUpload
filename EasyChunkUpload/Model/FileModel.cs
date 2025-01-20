namespace EasyChunkUpload.Model;
public class FileModel
{



    public Guid Id {get;set;}


    public string? FileName {get;set;}

    
    public int LastChunkNumber{get;set;}=0;

    public DateTimeOffset LastChunkUploadTime{get;set;}=DateTimeOffset.UtcNow;

    public int? ExpiredAfter{get;set;}

    
}
