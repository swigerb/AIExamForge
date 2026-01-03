using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Repositories;
using AIExamForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AIExamForge.Infrastructure.Repositories;

public class OutlineRepository : IOutlineRepository
{
    private readonly AIExamForgeDbContext _context;

    public OutlineRepository(AIExamForgeDbContext context)
    {
        _context = context;
    }

    public async Task<Outline?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Note: RootNodes are loaded separately since it's a backing field
        return await _context.Outlines
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Outline>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Outlines
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Outline>> GetByUserIdPagedAsync(string userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _context.Outlines
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Outlines
            .CountAsync(o => o.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Outline outline, CancellationToken cancellationToken = default)
    {
        await _context.Outlines.AddAsync(outline, cancellationToken);
    }

    public Task UpdateAsync(Outline outline, CancellationToken cancellationToken = default)
    {
        _context.Outlines.Update(outline);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var outline = await _context.Outlines.FindAsync([id], cancellationToken);
        if (outline != null)
        {
            _context.Outlines.Remove(outline);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Outlines.AnyAsync(o => o.Id == id, cancellationToken);
    }
}
