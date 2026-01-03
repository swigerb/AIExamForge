using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Repositories;
using AIExamForge.Domain.ValueObjects;
using AIExamForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AIExamForge.Infrastructure.Repositories;

public class GenerationRequestRepository : IGenerationRequestRepository
{
    private readonly AIExamForgeDbContext _context;

    public GenerationRequestRepository(AIExamForgeDbContext context)
    {
        _context = context;
    }

    public async Task<GenerationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GenerationRequests.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<GenerationRequest>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.GenerationRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GenerationRequest>> GetByUserIdPagedAsync(string userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _context.GenerationRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GenerationRequest>> GetByStatusAsync(GenerationStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.GenerationRequests
            .Where(r => r.Status == status)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GenerationRequest>> GetPendingRequestsAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        return await _context.GenerationRequests
            .Where(r => r.Status == GenerationStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.GenerationRequests
            .CountAsync(r => r.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(GenerationRequest request, CancellationToken cancellationToken = default)
    {
        await _context.GenerationRequests.AddAsync(request, cancellationToken);
    }

    public Task UpdateAsync(GenerationRequest request, CancellationToken cancellationToken = default)
    {
        _context.GenerationRequests.Update(request);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _context.GenerationRequests.FindAsync([id], cancellationToken);
        if (request != null)
        {
            _context.GenerationRequests.Remove(request);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GenerationRequests.AnyAsync(r => r.Id == id, cancellationToken);
    }
}
