using DocumentManagement.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.Property(d => d.OriginalFileName)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(d => d.StoredFileName)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(d => d.ContentType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(d => d.FileSize)
                .IsRequired();

            entity.Property(d => d.UploadedAt)
                .IsRequired();
        });
    }
}
