namespace AIExamForge.Domain.ValueObjects;

/// <summary>
/// Export format per spec.md FR-017 through FR-020.
/// </summary>
public enum ExportFormat
{
    /// <summary>JSON format with all metadata</summary>
    Json = 0,

    /// <summary>Markdown format, human-readable</summary>
    Markdown = 1,

    /// <summary>Word document (.docx) with title page and formatting</summary>
    Docx = 2,

    /// <summary>PDF document matching DOCX layout</summary>
    Pdf = 3
}

public static class ExportFormatExtensions
{
    public static string GetFileExtension(this ExportFormat format) => format switch
    {
        ExportFormat.Json => ".json",
        ExportFormat.Markdown => ".md",
        ExportFormat.Docx => ".docx",
        ExportFormat.Pdf => ".pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static string GetContentType(this ExportFormat format) => format switch
    {
        ExportFormat.Json => "application/json",
        ExportFormat.Markdown => "text/markdown",
        ExportFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ExportFormat.Pdf => "application/pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
