using AIExamForge.Domain.Models;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Application.Export.Interfaces;

/// <summary>
/// T136: Interface for exporting question sets in various formats.
/// Per openapi.yaml: GET /api/questions/{generationId}/export
/// </summary>
public interface IQuestionExporter
{
    /// <summary>
    /// Gets the export format this exporter handles.
    /// </summary>
    ExportFormat Format { get; }

    /// <summary>
    /// Gets the content type for the exported file.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Gets the file extension for the exported file.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Exports the question set to the target format.
    /// </summary>
    /// <param name="context">Export context containing questions and render mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exported file content as bytes.</returns>
    Task<ExportResult> ExportAsync(
        ExportContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an export operation.
/// </summary>
public sealed record ExportResult
{
    /// <summary>
    /// Whether the export was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The exported file content.
    /// </summary>
    public byte[] Content { get; init; } = [];

    /// <summary>
    /// The content type for the exported file.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Suggested filename for the export.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Error message if export failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static ExportResult Succeeded(byte[] content, string contentType, string fileName) => new()
    {
        Success = true,
        Content = content,
        ContentType = contentType,
        FileName = fileName
    };

    public static ExportResult Failed(string error) => new()
    {
        Success = false,
        ContentType = "application/octet-stream",
        FileName = "error.txt",
        ErrorMessage = error
    };
}
