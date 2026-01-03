using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Models;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Application.Export;

/// <summary>
/// T146: Export service that routes to appropriate exporter based on format.
/// Coordinates export operations and manages exporter registration.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports a question set in the specified format and render mode.
    /// </summary>
    Task<ExportResult> ExportAsync(
        QuestionSet questionSet,
        ExportFormat format,
        RenderMode renderMode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available export formats.
    /// </summary>
    IReadOnlyList<ExportFormat> GetAvailableFormats();
}

/// <summary>
/// Export service implementation that delegates to format-specific exporters.
/// </summary>
public sealed class ExportService : IExportService
{
    private readonly IReadOnlyDictionary<ExportFormat, IQuestionExporter> _exporters;

    public ExportService(IEnumerable<IQuestionExporter> exporters)
    {
        ArgumentNullException.ThrowIfNull(exporters, nameof(exporters));

        _exporters = exporters.ToDictionary(e => e.Format, e => e);
    }

    public async Task<ExportResult> ExportAsync(
        QuestionSet questionSet,
        ExportFormat format,
        RenderMode renderMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(questionSet, nameof(questionSet));

        if (!_exporters.TryGetValue(format, out var exporter))
        {
            return ExportResult.Failed($"Unsupported export format: {format}");
        }

        // Build export context using the factory method
        var context = ExportContext.Create(
            questionSet,
            renderMode,
            BuildTitle(questionSet),
            BuildDescription(questionSet, renderMode));

        return await exporter.ExportAsync(context, cancellationToken);
    }

    public IReadOnlyList<ExportFormat> GetAvailableFormats()
    {
        return _exporters.Keys.ToList();
    }

    private static string BuildTitle(QuestionSet questionSet)
    {
        return $"{questionSet.ExamName} Practice Questions";
    }

    private static string BuildDescription(QuestionSet questionSet, RenderMode renderMode)
    {
        var mode = renderMode == RenderMode.Exam ? "Exam" : "Study";
        return $"Practice questions for {questionSet.ExamName} in {mode} Mode. " +
               $"Contains {questionSet.TotalQuestions} questions generated from official documentation.";
    }
}
