using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Domain.Repositories;
using AIExamForge.Infrastructure.Data;
using AIExamForge.Infrastructure.Export;
using AIExamForge.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AIExamForge.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services with default database settings.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        return services.AddInfrastructure(connectionString, new DatabaseSettings());
    }

    /// <summary>
    /// T177: Adds infrastructure services with configurable connection pooling.
    /// Per NFR-004: Performance optimization through proper connection management.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        string connectionString,
        DatabaseSettings? settings = null)
    {
        settings ??= new DatabaseSettings();

        // Register DbContext with connection pooling and resilience configuration
        services.AddDbContext<AIExamForgeDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                // T177: Connection pooling is handled via connection string parameters
                // Min Pool Size and Max Pool Size are specified in connection string
                // Here we configure EF Core specific options

                // Command timeout
                sqlOptions.CommandTimeout(settings.CommandTimeout);

                // Retry policy for transient failures (Azure SQL best practice)
                if (settings.EnableRetryOnFailure)
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: settings.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(settings.MaxRetryDelaySeconds),
                        errorNumbersToAdd: null);
                }

                // Use batching for better performance with multiple inserts/updates
                sqlOptions.MaxBatchSize(100);
            });

            // Connection resiliency
            options.EnableServiceProviderCaching();
        });

        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register repositories (optional if using UnitOfWork only)
        services.AddScoped<IOutlineRepository, OutlineRepository>();
        services.AddScoped<IFileUploadRepository, FileUploadRepository>();
        services.AddScoped<IGenerationRequestRepository, GenerationRequestRepository>();
        services.AddScoped<IQuestionSetRepository, QuestionSetRepository>();
        services.AddScoped<IExportRecordRepository, ExportRecordRepository>();

        // Register exporters (T140-T141: DOCX and PDF are in Infrastructure due to dependencies)
        services.AddScoped<IQuestionExporter, DocxExporter>();
        services.AddScoped<IQuestionExporter, PdfExporter>();

        return services;
    }
}
