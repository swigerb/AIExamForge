using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AIExamForge.Infrastructure.Data.Configurations;

public class GenerationRequestConfiguration : IEntityTypeConfiguration<GenerationRequest>
{
    public void Configure(EntityTypeBuilder<GenerationRequest> builder)
    {
        builder.ToTable("GenerationRequests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.ExamName)
            .HasMaxLength(200);

        builder.Property(r => r.ToneProfile)
            .HasMaxLength(100);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(r => r.QuestionCount);
        builder.Property(r => r.QuestionsGenerated);
        builder.Property(r => r.QuestionsValidated);
        builder.Property(r => r.RetryCount);

        // Store selected node IDs as JSON array
        builder.Property<string>("SelectedNodeIdsJson")
            .HasColumnName("SelectedNodeIds")
            .HasConversion(
                v => v,
                v => v);

        // Store item types as JSON array of strings
        builder.Property<string>("ItemTypesJson")
            .HasColumnName("ItemTypes")
            .HasConversion(
                v => v,
                v => v);

        // Ignore the backing field collections - they'll be handled manually
        builder.Ignore(r => r.SelectedNodeIds);
        builder.Ignore(r => r.ItemTypes);

        // Store difficulty distribution as owned type
        builder.OwnsOne(r => r.DifficultyDistribution, dd =>
        {
            dd.Property(d => d.EasyPercent).HasColumnName("EasyPercent");
            dd.Property(d => d.MediumPercent).HasColumnName("MediumPercent");
            dd.Property(d => d.HardPercent).HasColumnName("HardPercent");
        });

        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.OutlineId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.CreatedAt);
        
        // T176: Composite index for user history queries per data-model.md
        builder.HasIndex(r => new { r.UserId, r.CreatedAt })
            .HasDatabaseName("IX_GenerationRequests_UserId_CreatedAt");
    }
}
