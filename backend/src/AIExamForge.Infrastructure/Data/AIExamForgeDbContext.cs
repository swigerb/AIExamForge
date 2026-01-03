using AIExamForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIExamForge.Infrastructure.Data;

/// <summary>
/// Entity Framework DbContext for AIExamForge.
/// </summary>
public class AIExamForgeDbContext : DbContext
{
    public AIExamForgeDbContext(DbContextOptions<AIExamForgeDbContext> options) : base(options)
    {
    }

    public DbSet<Outline> Outlines => Set<Outline>();
    public DbSet<OutlineNode> OutlineNodes => Set<OutlineNode>();
    public DbSet<FileUpload> FileUploads => Set<FileUpload>();
    public DbSet<GenerationRequest> GenerationRequests => Set<GenerationRequest>();
    public DbSet<QuestionSet> QuestionSets => Set<QuestionSet>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExportRecord> ExportRecords => Set<ExportRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AIExamForgeDbContext).Assembly);
    }
}
