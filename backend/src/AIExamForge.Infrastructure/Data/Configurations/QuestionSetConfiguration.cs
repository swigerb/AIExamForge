using AIExamForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AIExamForge.Infrastructure.Data.Configurations;

public class QuestionSetConfiguration : IEntityTypeConfiguration<QuestionSet>
{
    public void Configure(EntityTypeBuilder<QuestionSet> builder)
    {
        builder.ToTable("QuestionSets");

        builder.HasKey(qs => qs.Id);

        builder.Property(qs => qs.UserId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(qs => qs.ExamName)
            .HasMaxLength(200);

        builder.Property(qs => qs.RenderMode)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasMany(qs => qs.Questions)
            .WithOne()
            .HasForeignKey(q => q.QuestionSetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(qs => qs.UserId);
        
        // T176: Unique constraint for one-to-one with GenerationRequest per data-model.md
        builder.HasIndex(qs => qs.GenerationRequestId)
            .IsUnique();
            
        builder.HasIndex(qs => qs.CreatedAt);
    }
}
