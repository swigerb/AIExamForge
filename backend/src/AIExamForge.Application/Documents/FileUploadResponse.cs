using AIExamForge.Application.Parsing.DTOs;

namespace AIExamForge.Application.Documents;

/// <summary>
/// Response DTO for file upload operations.
/// </summary>
public sealed record FileUploadResponse
{
    /// <summary>
    /// The ID of the file upload record.
    /// </summary>
    public required Guid FileUploadId { get; init; }

    /// <summary>
    /// The ID of the created outline.
    /// </summary>
    public required Guid OutlineId { get; init; }

    /// <summary>
    /// The parsed outline.
    /// </summary>
    public required OutlineResponse Outline { get; init; }
}
