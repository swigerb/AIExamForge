using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AIExamForge.Infrastructure.Data.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Title)
            .IsRequired()
            .HasMaxLength(Question.MaxTitleLength);

        builder.Property(q => q.Stem)
            .IsRequired();

        builder.Property(q => q.Instruction)
            .HasMaxLength(1000);

        builder.Property(q => q.Difficulty)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(q => q.ContentJson)
            .IsRequired();

        builder.Property(q => q.Rationale);

        // Store ItemTypeCode as its string code
        builder.Property(q => q.ItemType)
            .HasConversion(
                v => v.Code,
                v => ItemTypeCode.Create(v))
            .HasColumnName("ItemTypeCode")
            .HasMaxLength(20);

        // Store Tags as owned type
        builder.OwnsOne(q => q.Tags, tags =>
        {
            tags.Property(t => t.Exam)
                .HasColumnName("TagExam")
                .HasMaxLength(100);

            tags.Property(t => t.Objective)
                .HasColumnName("TagObjective")
                .HasMaxLength(200);

            tags.Property(t => t.Section)
                .HasColumnName("TagSection")
                .HasMaxLength(200);

            tags.Property(t => t.Topics)
                .HasColumnName("TagTopics")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => (IReadOnlyList<string>)(JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()));
        });

        // Store Scoring as owned type
        builder.OwnsOne(q => q.Scoring, scoring =>
        {
            scoring.Property(s => s.Model)
                .HasColumnName("ScoringModel")
                .HasMaxLength(20);

            scoring.Property(s => s.MaxPoints)
                .HasColumnName("ScoringMaxPoints");
        });

        builder.HasIndex(q => q.QuestionSetId);
        builder.HasIndex(q => q.Difficulty);
        builder.HasIndex("ItemTypeCode");
    }
}
