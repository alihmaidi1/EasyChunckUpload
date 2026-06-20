namespace EasyChunkUpload.Storage.FileSystem;

public sealed class FileSystemStorageOptions
{
    public string RootPath { get; set; } = string.Empty;

    public int BufferSize { get; set; } = 128 * 1024;

    public bool FlushToDisk { get; set; } = true;
}
