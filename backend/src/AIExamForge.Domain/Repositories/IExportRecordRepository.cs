using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Repositories;

/// <summary>
/// Repository interface for ExportRecord entity operations.
/// </summary>
public interface IExportRecordRepository
{
    Task<ExportRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExportRecord>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExportRecord>> GetByUserIdPagedAsync(string userId, int skip, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExportRecord>> GetByQuestionSetIdAsync(Guid questionSetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExportRecord>> GetByFormatAsync(ExportFormat format, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExportRecord>> GetExpiredExportsAsync(CancellationToken cancellationToken = default);
    Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(ExportRecord exportRecord, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExportRecord exportRecord, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
