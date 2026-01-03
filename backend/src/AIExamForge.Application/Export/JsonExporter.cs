using System.Text.Json;
using System.Text.Json.Serialization;
using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Domain.Models;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Application.Export;

/// <summary>
/// T138: JSON exporter for question sets.
/// Per openapi.yaml: format=json returns application/json
/// Per FR-050: Exam Mode omits explanations.
/// Per FR-051: Study Mode includes explanations.
/// </summary>
public sealed class JsonExporter : IQuestionExporter
{
    public ExportFormat Format => ExportFormat.Json;
    public string ContentType => "application/json";
    public string FileExtension => ".json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<ExportResult> ExportAsync(
        ExportContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        try
        {
            var filteredQuestions = RenderModeFilter.FilterQuestions(context);
            var rationalesSection = RenderModeFilter.GetRationalesSection(context);

            var exportData = new JsonExportData
            {
                Metadata = new JsonExportMetadata
                {
                    Title = context.Title,
                    ExamName = context.ExamName,
                    Description = context.Description,
                    QuestionCount = context.QuestionCount,
                    GeneratedAt = context.GeneratedAt,
                    RenderMode = context.RenderMode.ToString(),
                    Disclaimer = ExportContext.ShortDisclaimer
                },
                Questions = filteredQuestions.Select(q => new JsonQuestion
                {
                    Id = q.Id,
                    ItemType = q.ItemTypeCode,
                    Title = q.Title,
                    Stem = q.Stem,
                    Instruction = q.Instruction,
                    Difficulty = q.Difficulty.ToString(),
                    Content = JsonSerializer.Deserialize<JsonElement>(q.Content),
                    Tags = new JsonQuestionTags
                    {
                        Exam = q.Tags.Exam,
                        Objective = q.Tags.Objective,
                        Section = q.Tags.Section,
                        Topics = q.Tags.Topics.ToList()
                    },
                    Scoring = new JsonQuestionScoring
                    {
                        Model = q.Scoring.Model,
                        MaxPoints = q.Scoring.MaxPoints
                    },
                    Rationale = q.Rationale // Will be null in Exam Mode
                }).ToList(),
                // Separate rationales section for Study Mode (null in Exam Mode)
                Rationales = rationalesSection.Count > 0 ? rationalesSection.Select(r => new JsonRationale
                {
                    QuestionNumber = r.QuestionNumber,
                    QuestionId = r.QuestionId,
                    Rationale = r.Rationale
                }).ToList() : null
            };

            var json = JsonSerializer.SerializeToUtf8Bytes(exportData, JsonOptions);
            var fileName = GenerateFileName(context);

            return Task.FromResult(ExportResult.Succeeded(json, ContentType, fileName));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExportResult.Failed($"JSON export failed: {ex.Message}"));
        }
    }

    private static string GenerateFileName(ExportContext context)
    {
        var sanitizedName = SanitizeFileName(context.ExamName);
        var modeIndicator = context.RenderMode == RenderMode.Exam ? "exam" : "study";
        var timestamp = context.GeneratedAt.ToString("yyyyMMdd");
        return $"{sanitizedName}_{modeIndicator}_{timestamp}.json";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Where(c => !invalid.Contains(c)));
        return sanitized.Replace(" ", "_").ToLowerInvariant();
    }
}

#region JSON Export DTOs

internal sealed record JsonExportData
{
    public required JsonExportMetadata Metadata { get; init; }
    public required List<JsonQuestion> Questions { get; init; }
    public List<JsonRationale>? Rationales { get; init; }
}

internal sealed record JsonExportMetadata
{
    public required string Title { get; init; }
    public required string ExamName { get; init; }
    public string? Description { get; init; }
    public int QuestionCount { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public required string RenderMode { get; init; }
    public required string Disclaimer { get; init; }
}

internal sealed record JsonQuestion
{
    public Guid Id { get; init; }
    public required string ItemType { get; init; }
    public required string Title { get; init; }
    public required string Stem { get; init; }
    public string? Instruction { get; init; }
    public required string Difficulty { get; init; }
    public JsonElement Content { get; init; }
    public required JsonQuestionTags Tags { get; init; }
    public required JsonQuestionScoring Scoring { get; init; }
    public string? Rationale { get; init; }
}

internal sealed record JsonQuestionTags
{
    public required string Exam { get; init; }
    public required string Objective { get; init; }
    public required string Section { get; init; }
    public required List<string> Topics { get; init; }
}

internal sealed record JsonQuestionScoring
{
    public required string Model { get; init; }
    public int MaxPoints { get; init; }
}

internal sealed record JsonRationale
{
    public int QuestionNumber { get; init; }
    public Guid QuestionId { get; init; }
    public required string Rationale { get; init; }
}

#endregion
