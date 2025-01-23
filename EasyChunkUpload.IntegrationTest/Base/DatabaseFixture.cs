using Microsoft.EntityFrameworkCore;

namespace EasyChunkUpload.IntegrationTest.Base;

public class DatabaseFixture : IDisposable
{

    private readonly string _dbName = Guid.NewGuid().ToString();
    public DbContextOptions<ChunkUploadContext> Options { get; }
    
    public DatabaseFixture()
    {
        Options = new DbContextOptionsBuilder<ChunkUploadContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
    }

    public void Dispose()
    {
        using var context = new ChunkUploadContext(Options);
        context.Database.EnsureDeleted();
    }
}
