using AIExamForge.Domain.Entities;

namespace AIExamForge.Domain.Repositories;

/// <summary>
/// Repository interface for Outline entity operations.
/// </summary>
public interface IOutlineRepository
{
    Task<Outline?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Outline>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Outline>> GetByUserIdPagedAsync(string userId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(Outline outline, CancellationToken cancellationToken = default);
    Task UpdateAsync(Outline outline, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
