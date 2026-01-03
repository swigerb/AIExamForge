namespace AIExamForge.Application.Documents.Interfaces;

/// <summary>
/// Extracts text content from document files.
/// Supports .docx and .pdf file formats.
/// </summary>
public interface IDocumentExtractor
{
    /// <summary>
    /// Gets the file extensions this extractor supports.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Determines if this extractor can handle the given file.
    /// </summary>
    /// <param name="fileName">The file name including extension.</param>
    bool CanExtract(string fileName);

    /// <summary>
    /// Extracts text content from the document.
    /// </summary>
    /// <param name="stream">The document content stream.</param>
    /// <param name="fileName">The file name for format detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted text content.</returns>
    Task<DocumentExtractionResult> ExtractTextAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of document text extraction.
/// </summary>
public sealed record DocumentExtractionResult
{
    /// <summary>
    /// Whether extraction was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Extracted text content.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Error message if extraction failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of pages/sections processed.
    /// </summary>
    public int PageCount { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static DocumentExtractionResult Successful(string text, int pageCount = 0)
        => new() { Success = true, Text = text, PageCount = pageCount };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static DocumentExtractionResult Failed(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };
}
