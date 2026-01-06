using AIExamForge.Application.Generation.DTOs;

namespace AIExamForge.Api.Services;

public interface IGenerationBackgroundQueue
{
    ValueTask EnqueueAsync(GenerationWorkItem item, CancellationToken cancellationToken);
    ValueTask<GenerationWorkItem> DequeueAsync(CancellationToken cancellationToken);
}

public sealed record GenerationWorkItem(
    Guid GenerationId,
    string UserId,
    GenerationRequestDto Request);
