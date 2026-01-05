using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Application.Generation.Interfaces;
using AIExamForge.Application.Mcp.Interfaces;
using AIExamForge.Domain.Interfaces;
using AIExamForge.Domain.Repositories;
using AIExamForge.Infrastructure.Data;
using AIExamForge.Infrastructure.Export;
using AIExamForge.Infrastructure.MCP;
using AIExamForge.Infrastructure.OpenAI;
using AIExamForge.Infrastructure.Repositories;
using AIExamForge.Infrastructure.TypeRegistry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
    /// Adds infrastructure services and binds infrastructure configuration (e.g., Azure OpenAI).
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString,
        DatabaseSettings? settings = null)
    {
        services.AddInfrastructure(connectionString, settings);

        // Item type registry (schemas + prompt fragments)
        services.AddSingleton<ItemTypeRegistry>();
        services.AddSingleton<IItemTypeRegistry>(sp => sp.GetRequiredService<ItemTypeRegistry>());
        services.AddHostedService<ItemTypeRegistryInitializerHostedService>();

        // MCP client
        services.Configure<McpClientOptions>(configuration.GetSection(McpClientOptions.SectionName));
        services.AddHttpClient<IMcpClient, McpClient>();

        // LLM provider (Azure OpenAI)
        services.AddSingleton<IOptions<AzureOpenAiOptions>>(_ =>
        {
            var options = new AzureOpenAiOptions
            {
                Endpoint = configuration["AzureOpenAI:Endpoint"] ?? string.Empty,
                ApiKey = configuration["AzureOpenAI:ApiKey"] ?? string.Empty,
                DeploymentName = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4-turbo",
                Temperature = float.TryParse(configuration["AzureOpenAI:Temperature"], out var temperature)
                    ? temperature
                    : 0.7f,
                MaxTokens = int.TryParse(configuration["AzureOpenAI:MaxTokens"], out var maxTokens)
                    ? maxTokens
                    : 4096
            };

            return Options.Create(options);
        });

        services.AddScoped<ILlmClient, AzureOpenAiClient>();

        return services;
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
