using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

        // Persist/hydrate the collections via backing fields.
        // We intentionally expose them as read-only lists on the entity, but store them as JSON.
        builder.Ignore(r => r.SelectedNodeIds);
        builder.Ignore(r => r.ItemTypes);

        builder.Property<List<Guid>>("_selectedNodeIds")
            .HasColumnName("SelectedNodeIds")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<Guid>()
                    : (JsonSerializer.Deserialize<List<Guid>>(v) ?? new List<Guid>()))
            .Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                v => v.ToList()));

        builder.Property<List<ItemTypeCode>>("_itemTypes")
            .HasColumnName("ItemTypes")
            .HasConversion(
                v => JsonSerializer.Serialize(v.Select(it => it.Code).ToList(), (JsonSerializerOptions?)null),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<ItemTypeCode>()
                    : (JsonSerializer.Deserialize<List<string>>(v) ?? new List<string>())
                        .Select(ItemTypeCode.Create)
                        .ToList())
            .Metadata.SetValueComparer(new ValueComparer<List<ItemTypeCode>>(
                (a, b) => a != null && b != null && a.Select(x => x.Code).SequenceEqual(b.Select(x => x.Code)),
                v => v.Select(x => x.Code).Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                v => v.Select(it => ItemTypeCode.Create(it.Code)).ToList()));

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
