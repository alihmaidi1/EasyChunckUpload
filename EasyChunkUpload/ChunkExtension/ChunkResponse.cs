using System;


namespace EasyChunkUpload.ChunkExtension;

public class ChunkResponse<T>
{

    public string Message{get;set;}

    public bool Status{get;set;}
    public T Data {get;set;}
    
}
