using AIExamForge.Application.Export;
using AIExamForge.Domain.Repositories;
using AIExamForge.Domain.ValueObjects;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AIExamForge.Api.Endpoints;

/// <summary>
/// T144: Export endpoints for downloading question sets.
/// Per openapi.yaml: GET /api/questions/{generationId}/export?format=json|md|docx|pdf&mode=exam|study
/// </summary>
public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/questions")
            .WithTags("Export")
            .WithOpenApi();

        group.MapGet("/{generationId}/export", ExportQuestionSet)
            .WithName("ExportQuestionSet")
            .WithSummary("Export question set in specified format")
            .WithDescription("Exports the question set for a generation in the requested format and render mode.")
            .Produces<FileContentHttpResult>(StatusCodes.Status200OK, "application/json", "text/markdown",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/pdf")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/export/formats", GetAvailableFormats)
            .WithName("GetExportFormats")
            .WithSummary("Get available export formats")
            .WithDescription("Returns list of supported export formats.");

        return routes;
    }

    /// <summary>
    /// Export question set in specified format.
    /// Per FR-017/018/019/020: Support JSON, Markdown, DOCX, PDF formats.
    /// Per FR-050/051: Exam Mode excludes rationales, Study Mode includes them.
    /// </summary>
    private static async Task<IResult> ExportQuestionSet(
        Guid generationId,
        [FromQuery(Name = "format")] string formatStr,
        [FromQuery(Name = "mode")] string modeStr,
        IExportService exportService,
        IGenerationRequestRepository generationRepository,
        IQuestionSetRepository questionSetRepository,
        CancellationToken cancellationToken)
    {
        // Parse and validate format
        if (!TryParseFormat(formatStr, out var format))
        {
            return Results.Problem(
                title: "Invalid export format",
                detail: $"Invalid format '{formatStr}'. Valid formats: json, md, docx, pdf",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Parse and validate render mode
        if (!TryParseRenderMode(modeStr, out var renderMode))
        {
            return Results.Problem(
                title: "Invalid render mode",
                detail: $"Invalid mode '{modeStr}'. Valid modes: exam, study",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Get the generation
        var generation = await generationRepository.GetByIdAsync(generationId, cancellationToken);
        if (generation is null)
        {
            return Results.Problem(
                title: "Generation not found",
                detail: $"Generation with ID {generationId} was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Check if generation has a question set
        if (generation.QuestionSetId is null)
        {
            return Results.Problem(
                title: "No questions to export",
                detail: "This generation has no questions available for export.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Get the question set
        var questionSet = await questionSetRepository.GetByIdWithQuestionsAsync(generation.QuestionSetId.Value, cancellationToken);
        if (questionSet is null || questionSet.TotalQuestions == 0)
        {
            return Results.Problem(
                title: "No questions to export",
                detail: "The question set for this generation could not be found or is empty.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Export
        var result = await exportService.ExportAsync(
            questionSet,
            format,
            renderMode,
            cancellationToken);

        if (!result.Success)
        {
            return Results.Problem(
                title: "Export failed",
                detail: result.ErrorMessage ?? "An error occurred during export.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        // Return file
        return Results.File(
            result.Content!,
            result.ContentType!,
            result.FileName);
    }

    /// <summary>
    /// Get available export formats.
    /// </summary>
    private static IResult GetAvailableFormats(IExportService exportService)
    {
        var formats = exportService.GetAvailableFormats()
            .Select(f => new ExportFormatInfo(
                Format: f.ToString().ToLowerInvariant(),
                DisplayName: GetDisplayName(f),
                ContentType: GetContentType(f),
                FileExtension: GetFileExtension(f)))
            .ToList();

        return Results.Ok(new ExportFormatsResponse(formats));
    }

    private static bool TryParseFormat(string? formatStr, out ExportFormat format)
    {
        format = ExportFormat.Json;

        if (string.IsNullOrWhiteSpace(formatStr))
        {
            return false;
        }

        return formatStr.ToLowerInvariant() switch
        {
            "json" => SetFormat(out format, ExportFormat.Json),
            "md" or "markdown" => SetFormat(out format, ExportFormat.Markdown),
            "docx" or "word" => SetFormat(out format, ExportFormat.Docx),
            "pdf" => SetFormat(out format, ExportFormat.Pdf),
            _ => false
        };
    }

    private static bool SetFormat(out ExportFormat format, ExportFormat value)
    {
        format = value;
        return true;
    }

    private static bool TryParseRenderMode(string? modeStr, out RenderMode mode)
    {
        mode = RenderMode.Exam;

        if (string.IsNullOrWhiteSpace(modeStr))
        {
            return false;
        }

        return modeStr.ToLowerInvariant() switch
        {
            "exam" => SetMode(out mode, RenderMode.Exam),
            "study" => SetMode(out mode, RenderMode.Study),
            _ => false
        };
    }

    private static bool SetMode(out RenderMode mode, RenderMode value)
    {
        mode = value;
        return true;
    }

    private static string GetDisplayName(ExportFormat format) => format switch
    {
        ExportFormat.Json => "JSON",
        ExportFormat.Markdown => "Markdown",
        ExportFormat.Docx => "Word Document",
        ExportFormat.Pdf => "PDF",
        _ => format.ToString()
    };

    private static string GetContentType(ExportFormat format) => format switch
    {
        ExportFormat.Json => "application/json",
        ExportFormat.Markdown => "text/markdown",
        ExportFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ExportFormat.Pdf => "application/pdf",
        _ => "application/octet-stream"
    };

    private static string GetFileExtension(ExportFormat format) => format switch
    {
        ExportFormat.Json => ".json",
        ExportFormat.Markdown => ".md",
        ExportFormat.Docx => ".docx",
        ExportFormat.Pdf => ".pdf",
        _ => ""
    };
}

/// <summary>
/// Response DTO for available export formats.
/// </summary>
public sealed record ExportFormatsResponse(IReadOnlyList<ExportFormatInfo> Formats);

/// <summary>
/// Information about a single export format.
/// </summary>
public sealed record ExportFormatInfo(
    string Format,
    string DisplayName,
    string ContentType,
    string FileExtension);
