using AIExamForge.Domain.Models;

namespace AIExamForge.Application.Generation.Interfaces;

/// <summary>
/// Interface for LLM (Large Language Model) client operations.
/// Used to generate exam questions from assembled prompts.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Generates a response from the LLM using the provided prompt.
    /// </summary>
    /// <param name="prompt">The assembled prompt payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The LLM response containing generated content.</returns>
    Task<LlmResponse> GenerateAsync(
        PromptPayload prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a response with streaming for progress updates.
    /// </summary>
    /// <param name="prompt">The assembled prompt payload.</param>
    /// <param name="onChunk">Callback for each streamed chunk.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete LLM response after streaming finishes.</returns>
    Task<LlmResponse> GenerateStreamingAsync(
        PromptPayload prompt,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the LLM service is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is available.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from an LLM generation request.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>
    /// Whether the generation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The generated content (expected to be JSON for question generation).
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of input tokens used.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Number of output tokens generated.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Total tokens used (input + output).
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Time taken for generation in milliseconds.
    /// </summary>
    public long GenerationTimeMs { get; init; }

    /// <summary>
    /// The model used for generation.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Unique request ID for tracing.
    /// </summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>
    /// The finish reason (e.g., "stop", "length", "content_filter").
    /// </summary>
    public string? FinishReason { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static LlmResponse Successful(
        string content,
        int inputTokens,
        int outputTokens,
        long generationTimeMs,
        string model,
        string requestId,
        string? finishReason = null) => new()
    {
        Success = true,
        Content = content,
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        GenerationTimeMs = generationTimeMs,
        Model = model,
        RequestId = requestId,
        FinishReason = finishReason
    };

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static LlmResponse Failed(string errorMessage, string requestId) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        RequestId = requestId
    };
}
