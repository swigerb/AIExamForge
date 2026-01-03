using System.Text.Json;

namespace AIExamForge.Application.Questions.DTOs;

/// <summary>
/// T127: Request DTO for updating a question.
/// Per openapi.yaml: PUT /api/questions/{questionId}
/// </summary>
public sealed record UpdateQuestionRequest
{
    /// <summary>
    /// The question stem text. If null, the existing stem is preserved.
    /// </summary>
    public string? Stem { get; init; }

    /// <summary>
    /// Additional instructions for the question. If null, existing instruction is preserved.
    /// </summary>
    public string? Instruction { get; init; }

    /// <summary>
    /// The explanation/rationale for the correct answer. If null, existing rationale is preserved.
    /// </summary>
    public string? Rationale { get; init; }

    /// <summary>
    /// The item-type-specific content (choices, answers, etc.). If null, existing content is preserved.
    /// This should conform to the question's item type schema.
    /// </summary>
    public JsonElement? Content { get; init; }

    /// <summary>
    /// The difficulty level override. If null, existing difficulty is preserved.
    /// Valid values: Easy, Medium, Hard
    /// </summary>
    public string? Difficulty { get; init; }

    /// <summary>
    /// Updated tags for the question. If null, existing tags are preserved.
    /// </summary>
    public UpdateQuestionTagsDto? Tags { get; init; }
}

/// <summary>
/// DTO for updating question tags.
/// </summary>
public sealed record UpdateQuestionTagsDto
{
    /// <summary>
    /// The exam identifier.
    /// </summary>
    public string? Exam { get; init; }

    /// <summary>
    /// The learning objective.
    /// </summary>
    public string? Objective { get; init; }

    /// <summary>
    /// The section within the exam.
    /// </summary>
    public string? Section { get; init; }

    /// <summary>
    /// Topic tags for the question.
    /// </summary>
    public List<string>? Topics { get; init; }
}

/// <summary>
/// Response DTO after successfully updating a question.
/// </summary>
public sealed record UpdateQuestionResponse
{
    /// <summary>
    /// The updated question ID.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Whether the update was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Validation warnings (if any). Non-blocking lint violations may appear here.
    /// </summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// Timestamp when the question was updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
