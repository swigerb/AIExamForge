using AIExamForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIExamForge.Infrastructure.Data.Configurations;

public class FileUploadConfiguration : IEntityTypeConfiguration<FileUpload>
{
    public void Configure(EntityTypeBuilder<FileUpload> builder)
    {
        builder.ToTable("FileUploads");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.UserId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(f => f.OriginalFileName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(f => f.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.BlobStorageUri)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(f => f.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(f => f.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(f => f.ExtractedText);

        builder.HasIndex(f => f.UserId);
        builder.HasIndex(f => f.Status);
        builder.HasIndex(f => f.UploadedAt);
        
        // T176: Composite index for cleanup job queries per data-model.md
        builder.HasIndex(f => new { f.Status, f.UploadedAt })
            .HasDatabaseName("IX_FileUploads_Status_UploadedAt");
    }
}
