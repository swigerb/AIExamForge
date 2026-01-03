using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Repositories;

/// <summary>
/// Repository interface for GenerationRequest entity operations.
/// </summary>
public interface IGenerationRequestRepository
{
    Task<GenerationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GenerationRequest>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GenerationRequest>> GetByUserIdPagedAsync(string userId, int skip, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GenerationRequest>> GetByStatusAsync(GenerationStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GenerationRequest>> GetPendingRequestsAsync(int maxCount, CancellationToken cancellationToken = default);
    Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(GenerationRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(GenerationRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
