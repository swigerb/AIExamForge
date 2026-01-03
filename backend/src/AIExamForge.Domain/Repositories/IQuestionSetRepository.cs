using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Repositories;

/// <summary>
/// Repository interface for QuestionSet entity operations.
/// </summary>
public interface IQuestionSetRepository
{
    Task<QuestionSet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuestionSet?> GetByIdWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuestionSet>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuestionSet>> GetByUserIdPagedAsync(string userId, int skip, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuestionSet>> GetByGenerationRequestIdAsync(Guid generationRequestId, CancellationToken cancellationToken = default);
    Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(QuestionSet questionSet, CancellationToken cancellationToken = default);
    Task UpdateAsync(QuestionSet questionSet, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets questions by item type across all user's question sets.
    /// </summary>
    Task<IReadOnlyList<Question>> GetQuestionsByItemTypeAsync(string userId, ItemTypeCode itemType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets questions by difficulty across all user's question sets.
    /// </summary>
    Task<IReadOnlyList<Question>> GetQuestionsByDifficultyAsync(string userId, QuestionDifficulty difficulty, CancellationToken cancellationToken = default);
}
