using EasyChunkUpload.Persistence.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace EasyChunkUpload.Persistence.EntityFrameworkCore;

public sealed class UploadDbContext(DbContextOptions<UploadDbContext> options) : DbContext(options)
{
    internal DbSet<UploadSessionEntity> UploadSessions => Set<UploadSessionEntity>();

    internal DbSet<UploadChunkEntity> UploadChunks => Set<UploadChunkEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<UploadSessionEntity>(entity =>
        {
            entity.ToTable("EasyChunkUploadSessions");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.FileName).HasMaxLength(255).IsRequired();
            entity.Property(value => value.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(value => value.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(value => value.StorageKey).HasMaxLength(512);
            entity.Property(value => value.LeaseOwner).HasMaxLength(64);
            entity.Property(value => value.LeasePurpose).HasConversion<string>().HasMaxLength(32);
            entity.Property(value => value.Version).IsConcurrencyToken();
            entity.HasMany(value => value.Chunks)
                .WithOne(value => value.Session)
                .HasForeignKey(value => value.UploadId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(value => new { value.State, value.ExpiresAt });
            entity.HasIndex(value => value.LeaseExpiresAt);
        });

        modelBuilder.Entity<UploadChunkEntity>(entity =>
        {
            entity.ToTable("EasyChunkUploadChunks");
            entity.HasKey(value => new { value.UploadId, value.ChunkIndex });
            entity.Property(value => value.Sha256).HasMaxLength(64).IsRequired();
        });
    }
}
