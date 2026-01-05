namespace AIExamForge.Application.Generation.DTOs;

using System.Text.Json;
using System.Text.Json.Serialization;

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
[JsonConverter(typeof(RenderModeJsonConverter))]
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

public sealed class RenderModeJsonConverter : JsonConverter<RenderMode>
{
    public override RenderMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => Parse(reader.GetString()),
            JsonTokenType.Number when reader.TryGetInt32(out var n) => (RenderMode)n,
            _ => throw new JsonException($"Invalid renderMode token type: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, RenderMode value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    private static RenderMode Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("renderMode cannot be empty");
        }

        var normalized = raw.Trim().Replace("_", "-").Replace(" ", "-").ToLowerInvariant();
        return normalized switch
        {
            "exam" or "exam-mode" or "exammode" => RenderMode.Exam,
            "study" or "study-mode" or "studymode" => RenderMode.Study,
            _ => throw new JsonException($"Invalid renderMode value: '{raw}'")
        };
    }
}

/// <summary>
/// Tone profile for generated question text.
/// </summary>
[JsonConverter(typeof(ToneProfileJsonConverter))]
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

public sealed class ToneProfileJsonConverter : JsonConverter<ToneProfile>
{
    public override ToneProfile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => Parse(reader.GetString()),
            JsonTokenType.Number when reader.TryGetInt32(out var n) => (ToneProfile)n,
            _ => throw new JsonException($"Invalid toneProfile token type: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, ToneProfile value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    private static ToneProfile Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("toneProfile cannot be empty");
        }

        var normalized = raw.Trim().Replace("_", "-").Replace(" ", "-").ToLowerInvariant();
        return normalized switch
        {
            "concise" => ToneProfile.Concise,
            "professional" => ToneProfile.Professional,
            "examlike" or "exam-like" or "exam-like-mode" => ToneProfile.ExamLike,
            _ => throw new JsonException($"Invalid toneProfile value: '{raw}'")
        };
    }
}
