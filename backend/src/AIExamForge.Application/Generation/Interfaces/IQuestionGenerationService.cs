using AIExamForge.Application.Generation.DTOs;

namespace AIExamForge.Application.Generation.Interfaces;

/// <summary>
/// Abstraction for question generation orchestration.
/// Keeps API endpoints testable by allowing deterministic fakes.
/// </summary>
public interface IQuestionGenerationService
{
    Task<GenerationResult> GenerateAsync(
        GenerationRequestDto requestDto,
        string userId,
        CancellationToken cancellationToken = default);

    Task<GenerationResult> RegenerateAllAsync(
        Guid generationRequestId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<QuestionRegenerationResult> RegenerateQuestionAsync(
        Guid questionId,
        string userId,
        CancellationToken cancellationToken = default);
}
