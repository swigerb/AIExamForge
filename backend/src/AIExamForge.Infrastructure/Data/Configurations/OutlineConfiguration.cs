using AIExamForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIExamForge.Infrastructure.Data.Configurations;

public class OutlineConfiguration : IEntityTypeConfiguration<Outline>
{
    public void Configure(EntityTypeBuilder<Outline> builder)
    {
        builder.ToTable("Outlines");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.UserId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(o => o.Title)
            .HasMaxLength(200);

        builder.Property(o => o.RawContent)
            .IsRequired()
            .HasMaxLength(512000); // 500KB per FR-001a

        builder.Property(o => o.ContentSizeBytes);

        builder.Property(o => o.FileUploadId);

        // RootNodes is a backing field list, configured via OutlineNode
        builder.Ignore(o => o.RootNodes);

        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => o.CreatedAt);
    }
}

public class OutlineNodeConfiguration : IEntityTypeConfiguration<OutlineNode>
{
    public void Configure(EntityTypeBuilder<OutlineNode> builder)
    {
        builder.ToTable("OutlineNodes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.OutlineId)
            .IsRequired();

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.Content)
            .HasMaxLength(10000);

        builder.Property(n => n.Level);

        builder.Property(n => n.SortOrder);

        builder.Property(n => n.ParentId);

        // Children is managed as a backing field
        builder.Ignore(n => n.Children);

        builder.HasIndex(n => n.OutlineId);
        builder.HasIndex(n => n.ParentId);
        
        // T176: Composite index for efficient tree ordering queries per data-model.md
        builder.HasIndex(n => new { n.OutlineId, n.ParentId, n.SortOrder })
            .HasDatabaseName("IX_OutlineNodes_OutlineId_ParentId_SortOrder");
    }
}
