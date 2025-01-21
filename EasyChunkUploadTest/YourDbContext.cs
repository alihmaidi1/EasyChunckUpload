using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyChunkUpload.Model;
using Microsoft.EntityFrameworkCore;

namespace EasyChunkUploadTest;

public class YourDbContext : DbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options) : base(options) { }
    public DbSet<FileModel> Files { get; set; }
}
