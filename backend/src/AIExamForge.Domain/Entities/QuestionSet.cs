using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Entities;

/// <summary>
/// Represents a collection of generated questions per data-model.md.
/// </summary>
public sealed class QuestionSet
{
    public const int MaxQuestionsPerSet = 50;

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public Guid GenerationRequestId { get; private set; }
    public string? ExamName { get; private set; }

    public IReadOnlyList<Question> Questions => _questions.AsReadOnly();
    private readonly List<Question> _questions = [];

    public int TotalQuestions => _questions.Count;
    public int DichotomousCount => _questions.Count(q => q.ItemType.IsDichotomous);
    public int PolytomousCount => _questions.Count(q => q.ItemType.IsPolytomous);

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastModifiedAt { get; private set; }

    /// <summary>
    /// The render mode for displaying questions.
    /// Study mode (default) shows rationales separately.
    /// Exam mode hides rationales entirely.
    /// </summary>
    public RenderMode RenderMode { get; private set; }

    private QuestionSet() { } // EF Core constructor

    public static QuestionSet Create(
        string userId,
        Guid generationRequestId,
        string? examName = null,
        RenderMode? renderMode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));

        return new QuestionSet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GenerationRequestId = generationRequestId,
            ExamName = examName,
            RenderMode = renderMode ?? RenderMode.Study, // Default per render-modes.md
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void AddQuestion(Question question)
    {
        ArgumentNullException.ThrowIfNull(question, nameof(question));

        if (_questions.Count >= MaxQuestionsPerSet)
        {
            throw new InvalidOperationException($"Cannot add more than {MaxQuestionsPerSet} questions to a set.");
        }

        _questions.Add(question);
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void AddQuestions(IEnumerable<Question> questions)
    {
        ArgumentNullException.ThrowIfNull(questions, nameof(questions));

        foreach (var question in questions)
        {
            AddQuestion(question);
        }
    }

    public void RemoveQuestion(Guid questionId)
    {
        var question = _questions.FirstOrDefault(q => q.Id == questionId)
            ?? throw new ArgumentException($"Question not found: {questionId}", nameof(questionId));

        _questions.Remove(question);
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Replaces an existing question with a new one at the same position.
    /// Used for regeneration scenarios.
    /// </summary>
    public void ReplaceQuestion(Guid oldQuestionId, Question newQuestion)
    {
        ArgumentNullException.ThrowIfNull(newQuestion, nameof(newQuestion));

        var index = _questions.FindIndex(q => q.Id == oldQuestionId);
        if (index < 0)
        {
            throw new ArgumentException($"Question not found: {oldQuestionId}", nameof(oldQuestionId));
        }

        _questions[index] = newQuestion;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateRenderMode(RenderMode renderMode)
    {
        RenderMode = renderMode;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateExamName(string? examName)
    {
        ExamName = examName;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets questions by difficulty level.
    /// </summary>
    public IEnumerable<Question> GetQuestionsByDifficulty(QuestionDifficulty difficulty)
        => _questions.Where(q => q.Difficulty == difficulty);

    /// <summary>
    /// Gets questions by item type code.
    /// </summary>
    public IEnumerable<Question> GetQuestionsByItemType(ItemTypeCode itemType)
        => _questions.Where(q => q.ItemType == itemType);

    /// <summary>
    /// Gets the difficulty distribution of current questions.
    /// </summary>
    public DifficultyDistribution GetActualDifficultyDistribution()
    {
        if (_questions.Count == 0)
        {
            return DifficultyDistribution.Default;
        }

        var easy = _questions.Count(q => q.Difficulty == QuestionDifficulty.Easy) * 100 / _questions.Count;
        var medium = _questions.Count(q => q.Difficulty == QuestionDifficulty.Medium) * 100 / _questions.Count;
        var hard = 100 - easy - medium; // Ensure sum is 100

        return DifficultyDistribution.Create(easy, medium, hard);
    }
}
