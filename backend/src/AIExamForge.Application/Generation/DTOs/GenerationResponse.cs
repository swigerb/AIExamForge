namespace AIExamForge.Application.Generation.DTOs;

/// <summary>
/// Response containing generation status and results.
/// Per openapi.yaml: Full generation details including progress and question set.
/// </summary>
public sealed record GenerationResponse
{
    /// <summary>
    /// Generation request ID.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Current status of the generation request.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// Error message if status is Failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The generated question set (populated when Completed).
    /// </summary>
    public QuestionSetDto? QuestionSet { get; init; }

    /// <summary>
    /// When the generation request was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the generation completed (or failed).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Topics where MCP returned insufficient content.
    /// These questions were not generated.
    /// </summary>
    public IReadOnlyList<string> InsufficientContentNotes { get; init; } = [];

    /// <summary>
    /// Development-only diagnostic data: the MCP queries sent while retrieving Learn content.
    /// Omitted in non-Development environments.
    /// </summary>
    public IReadOnlyList<McpQueryTraceDto>? DevMcpQueries { get; init; }
}

public sealed record McpQueryTraceDto
{
    public DateTimeOffset Timestamp { get; init; }
    public Guid? OutlineNodeId { get; init; }
    public required string Query { get; init; }
}

/// <summary>
/// Question set containing all generated questions.
/// </summary>
public sealed record QuestionSetDto
{
    /// <summary>
    /// Question set ID.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Total number of questions in the set.
    /// </summary>
    public int TotalQuestions { get; init; }

    /// <summary>
    /// The generated questions.
    /// </summary>
    public IReadOnlyList<QuestionDto> Questions { get; init; } = [];

    /// <summary>
    /// Available export formats for this question set.
    /// </summary>
    public IReadOnlyList<string> AvailableExportFormats { get; init; } = ["json", "markdown", "docx", "pdf"];
}

/// <summary>
/// Generated question DTO.
/// </summary>
public sealed record QuestionDto
{
    /// <summary>
    /// Question ID.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Display ID for the question (e.g., "Q001").
    /// </summary>
    public required string DisplayId { get; init; }

    /// <summary>
    /// Item type code (M1, BA, DA, etc.).
    /// </summary>
    public required string ItemTypeCode { get; init; }

    /// <summary>
    /// Human-readable type name.
    /// </summary>
    public required string TypeDisplayName { get; init; }

    /// <summary>
    /// Title of the source outline node.
    /// </summary>
    public required string SourceNodeTitle { get; init; }

    /// <summary>
    /// Question difficulty (Easy, Medium, Hard).
    /// </summary>
    public required string Difficulty { get; init; }

    /// <summary>
    /// Question stem/prompt.
    /// </summary>
    public required string Stem { get; init; }

    /// <summary>
    /// Optional instruction text.
    /// </summary>
    public string? Instruction { get; init; }

    /// <summary>
    /// Type-specific question content (validated against item-type schema).
    /// </summary>
    public required object Content { get; init; }

    /// <summary>
    /// Rationale/explanation (included only in Study mode exports).
    /// </summary>
    public string? Explanation { get; init; }
}

/// <summary>
/// SSE progress event data.
/// </summary>
public sealed record GenerationProgressEvent
{
    /// <summary>
    /// Event type: progress, question, complete, error.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Generation request ID.
    /// </summary>
    public required Guid GenerationId { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// Current status message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Question data (for "question" events).
    /// </summary>
    public QuestionDto? Question { get; init; }

    /// <summary>
    /// Error message (for "error" events).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a progress event.
    /// </summary>
    public static GenerationProgressEvent ProgressUpdate(Guid generationId, int progress, string message) =>
        new()
        {
            EventType = "progress",
            GenerationId = generationId,
            Progress = progress,
            Message = message
        };

    /// <summary>
    /// Creates a question event.
    /// </summary>
    public static GenerationProgressEvent QuestionGenerated(Guid generationId, QuestionDto question, int progress) =>
        new()
        {
            EventType = "question",
            GenerationId = generationId,
            Progress = progress,
            Question = question
        };

    /// <summary>
    /// Creates a complete event.
    /// </summary>
    public static GenerationProgressEvent Complete(Guid generationId) =>
        new()
        {
            EventType = "complete",
            GenerationId = generationId,
            Progress = 100
        };

    /// <summary>
    /// Creates an error event.
    /// </summary>
    public static GenerationProgressEvent Failed(Guid generationId, string error) =>
        new()
        {
            EventType = "error",
            GenerationId = generationId,
            Error = error
        };
}
