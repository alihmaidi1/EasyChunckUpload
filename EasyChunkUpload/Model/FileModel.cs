namespace EasyChunkUpload.Model;
public class FileModel
{



    public Guid Id{get;set;}


    public string FileName {get;set;}

    
    public int LastChunkNumber{get;set;}=0;

    public DateTimeOffset CreatedTime{get;set;}

    public int ExpiredAfter{get;set;}

    
}
