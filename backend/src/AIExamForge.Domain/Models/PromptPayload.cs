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
            ""title"": ""Short title"",
            ""stem"": ""Question stem text"",
            ""instruction"": ""(optional)"",
            ""difficulty"": ""easy|medium|hard"",
            ""tags"": {
                ""exam"": ""AZ-204"",
                ""objective"": ""Objective name"",
                ""section"": ""Section name"",
                ""topics"": [""Topic 1"", ""Topic 2""]
            },
            ""scoring"": {
                ""model"": ""dichotomous|polytomous"",
                ""maxPoints"": 1
            },
            ""content"": {
                // Item-type-specific fields (MUST match the item type schema)
            },
                        ""rationale"": ""Study-mode explanation. REQUIRED.""
        }
    ]
}

Rules:
- Output JSON only (no markdown, no prose).
- Do NOT include any text before or after the JSON.
- Use property names exactly as shown.
- The ""content"" object MUST match the schema for the given ""itemTypeCode"".
- Do NOT include explanations in the stem, instruction, or answer choice text.

Rationale formatting rules (MUST follow exactly):
- The rationale MUST include a separate explanation for the correct answer and for each incorrect answer choice.
- For each correct answer choice key (e.g., A), include a sentence that begins EXACTLY with:
    ""This is the correct answer for {LETTER}. It is 100% correct because""
- For each incorrect answer, include a sentence that begins EXACTLY with:
    ""This is a distractor. It is 100% incorrect because""
- Include at least one Microsoft Learn URL in the rationale, using this form:
    https://learn.microsoft.com/...
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
