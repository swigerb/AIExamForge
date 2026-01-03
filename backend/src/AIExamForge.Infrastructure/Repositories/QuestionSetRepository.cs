using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Repositories;
using AIExamForge.Domain.ValueObjects;
using AIExamForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AIExamForge.Infrastructure.Repositories;

public class QuestionSetRepository : IQuestionSetRepository
{
    private readonly AIExamForgeDbContext _context;

    public QuestionSetRepository(AIExamForgeDbContext context)
    {
        _context = context;
    }

    public async Task<QuestionSet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.QuestionSets.FindAsync([id], cancellationToken);
    }

    public async Task<QuestionSet?> GetByIdWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.QuestionSets
            .Include(qs => qs.Questions)
            .FirstOrDefaultAsync(qs => qs.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<QuestionSet>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.QuestionSets
            .Where(qs => qs.UserId == userId)
            .OrderByDescending(qs => qs.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuestionSet>> GetByUserIdPagedAsync(string userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _context.QuestionSets
            .Where(qs => qs.UserId == userId)
            .OrderByDescending(qs => qs.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuestionSet>> GetByGenerationRequestIdAsync(Guid generationRequestId, CancellationToken cancellationToken = default)
    {
        return await _context.QuestionSets
            .Where(qs => qs.GenerationRequestId == generationRequestId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.QuestionSets
            .CountAsync(qs => qs.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(QuestionSet questionSet, CancellationToken cancellationToken = default)
    {
        await _context.QuestionSets.AddAsync(questionSet, cancellationToken);
    }

    public Task UpdateAsync(QuestionSet questionSet, CancellationToken cancellationToken = default)
    {
        _context.QuestionSets.Update(questionSet);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var questionSet = await _context.QuestionSets.FindAsync([id], cancellationToken);
        if (questionSet != null)
        {
            _context.QuestionSets.Remove(questionSet);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.QuestionSets.AnyAsync(qs => qs.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Question>> GetQuestionsByItemTypeAsync(string userId, ItemTypeCode itemType, CancellationToken cancellationToken = default)
    {
        return await _context.Questions
            .Where(q => _context.QuestionSets.Any(qs => qs.Id == q.QuestionSetId && qs.UserId == userId))
            .Where(q => q.ItemType == itemType)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Question>> GetQuestionsByDifficultyAsync(string userId, QuestionDifficulty difficulty, CancellationToken cancellationToken = default)
    {
        return await _context.Questions
            .Where(q => _context.QuestionSets.Any(qs => qs.Id == q.QuestionSetId && qs.UserId == userId))
            .Where(q => q.Difficulty == difficulty)
            .ToListAsync(cancellationToken);
    }
}
