using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EasyChunkUpload.IntegrationTest.Base;

public class DatabaseFixture : IDisposable
{

    private readonly SqliteConnection _connection;
    private readonly string _dbName = Guid.NewGuid().ToString();
    public DbContextOptions<ChunkUploadContext> Options { get; }
    
    public DatabaseFixture()
    {

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<ChunkUploadContext>()
            .UseSqlite(_connection) 
            .Options;

        using var context = new ChunkUploadContext(Options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        using var context = new ChunkUploadContext(Options);
        context.Database.EnsureDeleted(); 
        _connection.Close(); 
    }
}
