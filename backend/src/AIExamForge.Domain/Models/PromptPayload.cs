namespace AIExamForge.Domain.Models;

/// <summary>
/// Represents a complete prompt payload ready for LLM generation.
/// Contains all components needed for question generation.
/// </summary>
public sealed record PromptPayload
{
    /// <summary>
    /// The system prompt that sets the AI's behavior and constraints.
    /// Per Constitution Principle VI: Enforces MCP-exclusive sourcing.
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// The user prompt containing context, instructions, and content.
    /// </summary>
    public required string UserPrompt { get; init; }

    /// <summary>
    /// Number of questions to generate.
    /// </summary>
    public required int QuestionCount { get; init; }

    /// <summary>
    /// The item types to generate (e.g., ["M1", "BA", "CHOOSE_N"]).
    /// </summary>
    public required IReadOnlyList<string> ItemTypes { get; init; }

    /// <summary>
    /// Source URLs from MCP content for citation purposes.
    /// </summary>
    public required IReadOnlyList<string> SourceUrls { get; init; }

    /// <summary>
    /// Render mode (Exam or Study).
    /// </summary>
    public required string RenderMode { get; init; }

    /// <summary>
    /// Optional difficulty distribution.
    /// </summary>
    public DifficultyDistribution? Difficulty { get; init; }

    /// <summary>
    /// Optional tone profile.
    /// </summary>
    public string? ToneProfile { get; init; }

    /// <summary>
    /// Unique identifier for this prompt (for tracing).
    /// </summary>
    public string PromptId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// When this prompt was assembled.
    /// </summary>
    public DateTimeOffset AssembledAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the estimated token count for this prompt.
    /// Uses rough approximation: 4 characters per token.
    /// </summary>
    public int EstimatedTokenCount => (SystemPrompt.Length + UserPrompt.Length) / 4;

    /// <summary>
    /// Gets the JSON schema instruction for the response format.
    /// </summary>
    public string GetJsonSchemaInstruction()
    {
        return @"
Respond ONLY with a valid JSON object in the following format:
{
  ""questions"": [
    {
      ""id"": ""Q001"",
      ""itemTypeCode"": ""M1"",
      ""difficulty"": ""medium"",
      ""stem"": ""Question text here"",
      ""options"": [...],
      ""correctAnswer"": ""A"",
      ""explanation"": ""Explanation text here"",
      ""sourceUrls"": [""https://learn.microsoft.com/...""]
    }
  ]
}

Do NOT include any text before or after the JSON.
";
    }
}

/// <summary>
/// Difficulty distribution for question generation.
/// </summary>
public sealed record DifficultyDistribution
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

    /// <summary>
    /// Returns the distribution as a string for prompts.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (EasyPercent > 0)
        {
            parts.Add($"{EasyPercent}% easy");
        }

        if (MediumPercent > 0)
        {
            parts.Add($"{MediumPercent}% medium");
        }

        if (HardPercent > 0)
        {
            parts.Add($"{HardPercent}% hard");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Calculates how many questions of each difficulty for a given count.
    /// </summary>
    public (int Easy, int Medium, int Hard) CalculateCounts(int totalQuestions)
    {
        var easy = (int)Math.Round(totalQuestions * EasyPercent / 100.0);
        var hard = (int)Math.Round(totalQuestions * HardPercent / 100.0);
        var medium = totalQuestions - easy - hard; // Remainder goes to medium
        return (easy, Math.Max(0, medium), hard);
    }
}
