using AIExamForge.Domain.Entities;

namespace AIExamForge.Domain.Repositories;

/// <summary>
/// Repository interface for FileUpload entity operations.
/// </summary>
public interface IFileUploadRepository
{
    Task<FileUpload?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileUpload>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileUpload>> GetExpiredUploadsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets files that should be cleaned up (uploaded before the cutoff time and not already deleted).
    /// </summary>
    Task<IEnumerable<FileUpload>> GetFilesForCleanupAsync(DateTimeOffset cutoffTime, CancellationToken cancellationToken = default);
    
    Task AddAsync(FileUpload fileUpload, CancellationToken cancellationToken = default);
    Task UpdateAsync(FileUpload fileUpload, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
