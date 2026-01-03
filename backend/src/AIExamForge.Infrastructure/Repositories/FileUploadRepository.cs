using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Repositories;
using AIExamForge.Domain.ValueObjects;
using AIExamForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AIExamForge.Infrastructure.Repositories;

public class FileUploadRepository : IFileUploadRepository
{
    private readonly AIExamForgeDbContext _context;

    public FileUploadRepository(AIExamForgeDbContext context)
    {
        _context = context;
    }

    public async Task<FileUpload?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FileUploads.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<FileUpload>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.FileUploads
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FileUpload>> GetExpiredUploadsAsync(CancellationToken cancellationToken = default)
    {
        // Files that have been processed but not yet cleaned up (e.g., after 24 hours)
        var expirationThreshold = DateTimeOffset.UtcNow.AddHours(-24);
        return await _context.FileUploads
            .Where(f => f.ProcessedAt < expirationThreshold && f.Status != FileUploadStatus.Deleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<FileUpload>> GetFilesForCleanupAsync(DateTimeOffset cutoffTime, CancellationToken cancellationToken = default)
    {
        // Get files uploaded before the cutoff time that haven't been deleted yet
        return await _context.FileUploads
            .Where(f => f.UploadedAt < cutoffTime && f.Status != FileUploadStatus.Deleted)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FileUpload fileUpload, CancellationToken cancellationToken = default)
    {
        await _context.FileUploads.AddAsync(fileUpload, cancellationToken);
    }

    public Task UpdateAsync(FileUpload fileUpload, CancellationToken cancellationToken = default)
    {
        _context.FileUploads.Update(fileUpload);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fileUpload = await _context.FileUploads.FindAsync([id], cancellationToken);
        if (fileUpload != null)
        {
            _context.FileUploads.Remove(fileUpload);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FileUploads.AnyAsync(f => f.Id == id, cancellationToken);
    }
}
