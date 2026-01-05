using AIExamForge.Application.Export;
using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Application.Documents;
using AIExamForge.Application.Generation;
using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Application.Generation.Interfaces;
using AIExamForge.Application.Generation.Validators;
using AIExamForge.Application.Mcp;
using AIExamForge.Application.Parsing;
using AIExamForge.Application.Parsing.Interfaces;
using AIExamForge.Application.Questions;
using AIExamForge.Application.Validation;
using AIExamForge.Application.Validation.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AIExamForge.Application;

/// <summary>
/// Dependency injection configuration for the Application layer.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Parsing services
        services.AddScoped<IOutlineParser, OutlineParser>();
        services.AddScoped<OutlineService>();

        // Document upload services
        services.AddScoped<FileUploadService>();

        // Validation services
        services.AddScoped<ISchemaValidator, SchemaValidator>();
        services.AddScoped<ILintValidator, LintValidator>();
        services.AddScoped<QuestionValidationPipeline>();

        // MCP services
        services.AddScoped<McpRetrievalService>();
        
        // MCP response caching (T175: Per NFR-004 performance optimization)
        services.AddScoped<IMcpResponseCache, RequestScopedMcpCache>();
        services.AddScoped<CachedMcpRetrievalService>();

        // Generation services
        services.AddScoped<IPromptAssembler, PromptAssembler>();
        services.AddScoped<QuestionGenerationService>();

        // Question editing services (T127-T130)
        services.AddScoped<QuestionEditingService>();

        // Export services (T136-T146)
        // Note: Exporters (JSON in Application, DOCX/PDF in Infrastructure) must be registered
        // by Infrastructure.DependencyInjection.AddInfrastructure() before this
        services.AddScoped<IQuestionExporter, JsonExporter>();
        services.AddScoped<IQuestionExporter, MarkdownExporter>();
        services.AddScoped<IExportService, ExportService>();

        // FluentValidation validators
        services.AddScoped<IValidator<GenerationRequestDto>, GenerationRequestValidator>();

        return services;
    }
}
