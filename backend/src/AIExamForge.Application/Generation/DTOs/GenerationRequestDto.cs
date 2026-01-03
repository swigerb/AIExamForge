namespace AIExamForge.Application.Generation.DTOs;

/// <summary>
/// DTO for requesting question generation per openapi.yaml GenerationRequest schema.
/// </summary>
public sealed record GenerationRequestDto
{
    /// <summary>
    /// The ID of the outline to generate questions for.
    /// </summary>
    public required Guid OutlineId { get; init; }

    /// <summary>
    /// The IDs of the outline nodes (sections) to generate questions for.
    /// Must contain at least one node ID.
    /// </summary>
    public required IReadOnlyList<Guid> SelectedNodeIds { get; init; }

    /// <summary>
    /// Number of questions to generate (1-50).
    /// </summary>
    public required int QuestionCount { get; init; }

    /// <summary>
    /// Item type codes for the questions to generate.
    /// Must contain at least one valid item type.
    /// </summary>
    public required IReadOnlyList<string> QuestionTypes { get; init; }

    /// <summary>
    /// Render mode for the generated questions.
    /// - Exam: Export without rationales/explanations
    /// - Study: Export with rationales in separate section (default)
    /// </summary>
    public RenderMode RenderMode { get; init; } = RenderMode.Study;

    /// <summary>
    /// Optional difficulty distribution for generated questions.
    /// Percentages must sum to 100 if specified.
    /// </summary>
    public DifficultyDistributionDto? Difficulty { get; init; }

    /// <summary>
    /// Optional tone profile for the generated questions.
    /// </summary>
    public ToneProfile? ToneProfile { get; init; }
}

/// <summary>
/// Distribution of question difficulty levels.
/// </summary>
public sealed record DifficultyDistributionDto
{
    /// <summary>
    /// Percentage of easy questions (0-100).
    /// </summary>
    public int EasyPercent { get; init; }

    /// <summary>
    /// Percentage of medium questions (0-100).
    /// </summary>
    public int MediumPercent { get; init; }

    /// <summary>
    /// Percentage of hard questions (0-100).
    /// </summary>
    public int HardPercent { get; init; }
}

/// <summary>
/// Render mode for generated questions.
/// </summary>
public enum RenderMode
{
    /// <summary>
    /// Export without rationales/explanations (exam mode).
    /// </summary>
    Exam,

    /// <summary>
    /// Export with rationales in separate section (default study mode).
    /// </summary>
    Study
}

/// <summary>
/// Tone profile for generated question text.
/// </summary>
public enum ToneProfile
{
    /// <summary>
    /// Concise, minimal explanations.
    /// </summary>
    Concise,

    /// <summary>
    /// Professional, formal tone.
    /// </summary>
    Professional,

    /// <summary>
    /// Exam-like, neutral and precise.
    /// </summary>
    ExamLike
}
