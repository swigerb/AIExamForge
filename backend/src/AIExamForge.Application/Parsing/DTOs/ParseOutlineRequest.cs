using System.ComponentModel.DataAnnotations;

namespace AIExamForge.Application.Parsing.DTOs;

/// <summary>
/// Request to parse a plain text exam outline.
/// Per openapi.yaml: POST /outlines/parse
/// </summary>
public sealed record ParseOutlineRequest
{
    /// <summary>
    /// Plain text exam outline content.
    /// </summary>
    [Required]
    [MaxLength(512000)] // 512KB max per openapi.yaml
    public required string Text { get; init; }

    /// <summary>
    /// Optional exam name (e.g., "AZ-204").
    /// </summary>
    [MaxLength(256)]
    public string? ExamName { get; init; }
}
