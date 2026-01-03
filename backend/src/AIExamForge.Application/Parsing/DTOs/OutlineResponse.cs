namespace AIExamForge.Application.Parsing.DTOs;

/// <summary>
/// Response containing parsed outline structure.
/// Per openapi.yaml: OutlineResponse schema
/// </summary>
public sealed record OutlineResponse
{
    /// <summary>
    /// Unique identifier for the outline.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Exam name (e.g., "AZ-204").
    /// </summary>
    public string? ExamName { get; init; }

    /// <summary>
    /// Source type: TextPaste, DocxUpload, or PdfUpload.
    /// </summary>
    public required string SourceType { get; init; }

    /// <summary>
    /// Original file name if uploaded.
    /// </summary>
    public string? SourceFileName { get; init; }

    /// <summary>
    /// Root-level outline nodes.
    /// </summary>
    public required IReadOnlyList<OutlineNodeDto> Nodes { get; init; }

    /// <summary>
    /// When the outline was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// DTO representing an outline node in the hierarchy.
/// Per openapi.yaml: OutlineNode schema
/// </summary>
public sealed record OutlineNodeDto
{
    /// <summary>
    /// Unique identifier for the node.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Section title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Optional content/description.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Nesting level (1-10).
    /// </summary>
    public required int Level { get; init; }

    /// <summary>
    /// Child nodes.
    /// </summary>
    public required IReadOnlyList<OutlineNodeDto> Children { get; init; }
}
