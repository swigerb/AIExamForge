using AIExamForge.Api;
using AIExamForge.Application.Export;
using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Application.Generation.Interfaces;
using AIExamForge.Application.Validation.Interfaces;
using AIExamForge.Domain.ValueObjects;
using AIExamForge.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AIExamForge.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"AIExamForge_IntegrationTests_{Guid.NewGuid():N}";

    public ApiWebApplicationFactory()
    {
        // Integration tests should never try to contact Azure Key Vault.
        // In Visual Studio's test runner, DefaultAzureCredential can attempt to use Visual Studio/VS Code
        // credentials which may block/hang the IDE.
        Environment.SetEnvironmentVariable("AZURE_KEY_VAULT_ENDPOINT", null);
        Environment.SetEnvironmentVariable("AZURE_KEY_VAULT_URL", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureLogging(logging =>
        {
            // Keep the VS test runner responsive (avoid flooding the Output window with Debug/Info logs).
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server DbContext with InMemory for integration tests.
            services.RemoveAll<DbContextOptions<AIExamForgeDbContext>>();
            services.RemoveAll<AIExamForgeDbContext>();

            // Keep EF provider registrations isolated for the test DbContext to avoid
            // the "only a single database provider can be registered" error.
            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<AIExamForgeDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.UseInternalServiceProvider(inMemoryProvider);
            });

            // Make integration tests deterministic by removing dependencies on external services/assets.
            services.RemoveAll<IQuestionGenerationService>();
            services.AddSingleton<IQuestionGenerationService, FakeQuestionGenerationService>();

            services.RemoveAll<ISchemaValidator>();
            services.AddSingleton<ISchemaValidator, FakeSchemaValidator>();

            services.RemoveAll<ILintValidator>();
            services.AddSingleton<ILintValidator, FakeLintValidator>();

            services.RemoveAll<IExportService>();
            services.AddSingleton<IExportService, FakeExportService>();
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Ensure any Azure Key Vault configuration source is removed for test runs.
            // (Key vault access can hang when DefaultAzureCredential tries IDE-linked credentials.)
            var keyVaultSources = config.Sources
                .Where(s => (s.GetType().FullName ?? string.Empty).Contains("AzureKeyVault", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var source in keyVaultSources)
            {
                config.Sources.Remove(source);
            }

            // Provide minimal configuration required for app startup.
            // The API currently throws if DefaultConnection is missing.
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=AIExamForge_Test;Trusted_Connection=True;TrustServerCertificate=True",

                // Minimal AzureAd config so Microsoft.Identity.Web can bind options.
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
                ["AzureAd:ClientId"] = "00000000-0000-0000-0000-000000000000",

                // Keep CORS predictable in tests.
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000"
            };

            config.AddInMemoryCollection(overrides);
        });
    }
}

internal sealed class FakeQuestionGenerationService : IQuestionGenerationService
{
    public Task<AIExamForge.Application.Generation.GenerationResult> GenerateAsync(
        AIExamForge.Application.Generation.DTOs.GenerationRequestDto requestDto,
        string userId,
        Guid? generationId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AIExamForge.Application.Generation.GenerationResult.Succeeded(
            requestId: generationId ?? Guid.NewGuid(),
            questionSetId: Guid.NewGuid(),
            generated: 0,
            failed: 0,
            sourceUrls: []));

    public async Task<AIExamForge.Application.Generation.GenerationResult> GenerateStreamingAsync(
        AIExamForge.Application.Generation.DTOs.GenerationRequestDto requestDto,
        string userId,
        Guid? generationId,
        Func<AIExamForge.Application.Generation.GenerationProgress, Task> onProgress,
        CancellationToken cancellationToken = default)
    {
        await onProgress(AIExamForge.Application.Generation.GenerationProgress.Started("Starting generation"));
        await onProgress(AIExamForge.Application.Generation.GenerationProgress.Completed(generated: 0, failed: 0));

        return await GenerateAsync(requestDto, userId, generationId, cancellationToken);
    }

    public Task<AIExamForge.Application.Generation.GenerationResult> RegenerateAllAsync(
        Guid generationRequestId,
        string userId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AIExamForge.Application.Generation.GenerationResult.Succeeded(
            requestId: generationRequestId,
            questionSetId: Guid.NewGuid(),
            generated: 0,
            failed: 0,
            sourceUrls: []));

    public Task<AIExamForge.Application.Generation.QuestionRegenerationResult> RegenerateQuestionAsync(
        Guid questionId,
        string userId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(questionId == Guid.Empty
            ? AIExamForge.Application.Generation.QuestionRegenerationResult.Failed(questionId, "not found")
            : new AIExamForge.Application.Generation.QuestionRegenerationResult
            {
                OriginalQuestionId = questionId,
                Success = true,
                NewQuestionId = null
            });
}

internal sealed class FakeSchemaValidator : ISchemaValidator
{
    public Task<AIExamForge.Domain.ValueObjects.ValidationResult> ValidateQuestionAsync(
        string questionJson,
        string itemTypeCode,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AIExamForge.Domain.ValueObjects.ValidationResult.Success());

    public Task<AIExamForge.Domain.ValueObjects.ValidationResult> ValidateBaseSchemaAsync(
        string questionJson,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AIExamForge.Domain.ValueObjects.ValidationResult.Success());

    public Task<AIExamForge.Domain.ValueObjects.ValidationResult> ValidateItemTypeSchemaAsync(
        string questionJson,
        string itemTypeCode,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AIExamForge.Domain.ValueObjects.ValidationResult.Success());
}

internal sealed class FakeLintValidator : ILintValidator
{
    public IReadOnlyList<AIExamForge.Domain.ValueObjects.LintViolation> Validate(string text, string context = "text")
        => [];

    public IReadOnlyList<AIExamForge.Domain.ValueObjects.LintViolation> ValidateQuestion(
        string stem,
        string? instruction,
        IEnumerable<string> choices,
        string? rationale)
        => [];

    public AIExamForge.Domain.ValueObjects.LintViolation? ValidateRule(string text, AIExamForge.Domain.ValueObjects.LintRule rule)
        => null;
}

internal sealed class FakeExportService : IExportService
{
    public IReadOnlyList<ExportFormat> GetAvailableFormats() =>
        [ExportFormat.Json, ExportFormat.Markdown, ExportFormat.Docx, ExportFormat.Pdf];

    public Task<ExportResult> ExportAsync(
        AIExamForge.Domain.Entities.QuestionSet questionSet,
        ExportFormat format,
        RenderMode renderMode,
        CancellationToken cancellationToken = default)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}");
        var fileName = $"export{GetExtension(format)}";

        return Task.FromResult(ExportResult.Succeeded(
            content: bytes,
            contentType: GetContentType(format),
            fileName: fileName));
    }

    private static string GetExtension(ExportFormat format) => format switch
    {
        ExportFormat.Json => ".json",
        ExportFormat.Markdown => ".md",
        ExportFormat.Docx => ".docx",
        ExportFormat.Pdf => ".pdf",
        _ => ".bin"
    };

    private static string GetContentType(ExportFormat format) => format switch
    {
        ExportFormat.Json => "application/json",
        ExportFormat.Markdown => "text/markdown",
        ExportFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ExportFormat.Pdf => "application/pdf",
        _ => "application/octet-stream"
    };
}
