using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Repositories;
using AIExamForge.Domain.ValueObjects;
using AIExamForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AIExamForge.Infrastructure.Repositories;

public class ExportRecordRepository : IExportRecordRepository
{
    private readonly AIExamForgeDbContext _context;

    public ExportRecordRepository(AIExamForgeDbContext context)
    {
        _context = context;
    }

    public async Task<ExportRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ExportRecords.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<ExportRecord>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.ExportRecords
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExportRecord>> GetByUserIdPagedAsync(string userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _context.ExportRecords
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExportRecord>> GetByQuestionSetIdAsync(Guid questionSetId, CancellationToken cancellationToken = default)
    {
        return await _context.ExportRecords
            .Where(e => e.QuestionSetId == questionSetId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExportRecord>> GetByFormatAsync(ExportFormat format, CancellationToken cancellationToken = default)
    {
        return await _context.ExportRecords
            .Where(e => e.Format == format)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExportRecord>> GetExpiredExportsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _context.ExportRecords
            .Where(e => e.ExpiresAt < now && !e.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.ExportRecords
            .CountAsync(e => e.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(ExportRecord exportRecord, CancellationToken cancellationToken = default)
    {
        await _context.ExportRecords.AddAsync(exportRecord, cancellationToken);
    }

    public Task UpdateAsync(ExportRecord exportRecord, CancellationToken cancellationToken = default)
    {
        _context.ExportRecords.Update(exportRecord);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exportRecord = await _context.ExportRecords.FindAsync([id], cancellationToken);
        if (exportRecord != null)
        {
            _context.ExportRecords.Remove(exportRecord);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ExportRecords.AnyAsync(e => e.Id == id, cancellationToken);
    }
}
