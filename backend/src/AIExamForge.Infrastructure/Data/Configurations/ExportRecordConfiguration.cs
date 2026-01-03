using AIExamForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIExamForge.Infrastructure.Data.Configurations;

public class ExportRecordConfiguration : IEntityTypeConfiguration<ExportRecord>
{
    public void Configure(EntityTypeBuilder<ExportRecord> builder)
    {
        builder.ToTable("ExportRecords");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Format)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.RenderMode)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.BlobStorageUri)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.QuestionSetId);
        builder.HasIndex(e => e.ExpiresAt);
        
        // T176: Composite index for existing export lookup per data-model.md
        builder.HasIndex(e => new { e.QuestionSetId, e.Format })
            .HasDatabaseName("IX_ExportRecords_QuestionSetId_Format");
    }
}
