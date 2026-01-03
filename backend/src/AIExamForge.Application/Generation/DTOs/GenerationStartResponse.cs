namespace AIExamForge.Application.Generation.DTOs;

/// <summary>
/// Response when starting a generation request.
/// Per openapi.yaml: Returns generationId, status (Pending), and streamUrl.
/// </summary>
public sealed record GenerationStartResponse
{
    /// <summary>
    /// Unique identifier for the generation request.
    /// </summary>
    public required Guid GenerationId { get; init; }

    /// <summary>
    /// Initial status (always "Pending" when created).
    /// </summary>
    public string Status { get; init; } = "Pending";

    /// <summary>
    /// URL to stream generation progress via SSE.
    /// </summary>
    public required string StreamUrl { get; init; }

    /// <summary>
    /// Creates a GenerationStartResponse from a generation ID.
    /// </summary>
    public static GenerationStartResponse FromId(Guid generationId, string baseUrl)
    {
        return new GenerationStartResponse
        {
            GenerationId = generationId,
            StreamUrl = $"{baseUrl.TrimEnd('/')}/api/generations/{generationId}/stream"
        };
    }
}
