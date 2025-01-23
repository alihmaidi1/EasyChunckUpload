using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyChunkUpload.Model;
using Microsoft.EntityFrameworkCore;

namespace EasyChunkUpload.IntegrationTest.Base;


/// <summary>
/// Database context for chunk upload operations
/// </summary>
public class ChunkUploadContext: DbContext
{

    public ChunkUploadContext(DbContextOptions<ChunkUploadContext> options): base(options) { }
    
    public virtual DbSet<FileModel> Files { get; set; }


}
