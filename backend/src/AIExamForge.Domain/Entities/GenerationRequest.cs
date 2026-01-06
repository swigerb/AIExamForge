using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Entities;

/// <summary>
/// Represents a question generation request per data-model.md.
/// </summary>
public sealed class GenerationRequest
{
    public const int MaxQuestionsPerRequest = 50; // Per FR-024

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public Guid OutlineId { get; private set; }
    public string? ExamName { get; private set; }

    /// <summary>
    /// Selected node IDs from the outline for question generation.
    /// </summary>
    public IReadOnlyList<Guid> SelectedNodeIds => _selectedNodeIds.AsReadOnly();
    private List<Guid> _selectedNodeIds = [];

    public int QuestionCount { get; private set; }

    /// <summary>
    /// Selected item type codes for generation.
    /// </summary>
    public IReadOnlyList<ItemTypeCode> ItemTypes => _itemTypes.AsReadOnly();
    private List<ItemTypeCode> _itemTypes = [];

    public DifficultyDistribution DifficultyDistribution { get; private set; } = null!;
    public string? ToneProfile { get; private set; }
    public GenerationStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int? QuestionsGenerated { get; private set; }
    public int? QuestionsValidated { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// The resulting question set, if generation completed.
    /// </summary>
    public Guid? QuestionSetId { get; private set; }

    private GenerationRequest() { } // EF Core constructor

    public static GenerationRequest Create(
        string userId,
        Guid outlineId,
        IEnumerable<Guid> selectedNodeIds,
        int questionCount,
        IEnumerable<ItemTypeCode> itemTypes,
        DifficultyDistribution? difficultyDistribution = null,
        string? examName = null,
        string? toneProfile = null)
    {
        return CreateInternal(
            id: null,
            userId,
            outlineId,
            selectedNodeIds,
            questionCount,
            itemTypes,
            difficultyDistribution,
            examName,
            toneProfile);
    }

    public static GenerationRequest CreateWithId(
        Guid id,
        string userId,
        Guid outlineId,
        IEnumerable<Guid> selectedNodeIds,
        int questionCount,
        IEnumerable<ItemTypeCode> itemTypes,
        DifficultyDistribution? difficultyDistribution = null,
        string? examName = null,
        string? toneProfile = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Generation ID cannot be empty.", nameof(id));
        }

        return CreateInternal(
            id,
            userId,
            outlineId,
            selectedNodeIds,
            questionCount,
            itemTypes,
            difficultyDistribution,
            examName,
            toneProfile);
    }

    private static GenerationRequest CreateInternal(
        Guid? id,
        string userId,
        Guid outlineId,
        IEnumerable<Guid> selectedNodeIds,
        int questionCount,
        IEnumerable<ItemTypeCode> itemTypes,
        DifficultyDistribution? difficultyDistribution,
        string? examName,
        string? toneProfile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));

        var nodeIdList = selectedNodeIds.ToList();
        if (nodeIdList.Count == 0)
        {
            throw new ArgumentException("At least one outline node must be selected.", nameof(selectedNodeIds));
        }

        if (questionCount < 1 || questionCount > MaxQuestionsPerRequest)
        {
            throw new ArgumentOutOfRangeException(
                nameof(questionCount),
                $"Question count must be between 1 and {MaxQuestionsPerRequest}. Got: {questionCount}");
        }

        var itemTypeList = itemTypes.ToList();
        if (itemTypeList.Count == 0)
        {
            throw new ArgumentException("At least one item type must be selected.", nameof(itemTypes));
        }

        var request = new GenerationRequest
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            OutlineId = outlineId,
            QuestionCount = questionCount,
            DifficultyDistribution = difficultyDistribution ?? DifficultyDistribution.Default,
            ExamName = examName,
            ToneProfile = toneProfile,
            Status = GenerationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        request._selectedNodeIds.AddRange(nodeIdList);
        request._itemTypes.AddRange(itemTypeList);

        return request;
    }

    public void StartContentRetrieval()
    {
        if (Status != GenerationStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot start. Current status: {Status}");
        }

        Status = GenerationStatus.RetrievingContent;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void StartGeneration()
    {
        if (Status != GenerationStatus.RetrievingContent)
        {
            throw new InvalidOperationException($"Cannot start generation. Current status: {Status}");
        }

        Status = GenerationStatus.Generating;
    }

    public void StartValidation(int questionsGenerated)
    {
        if (Status != GenerationStatus.Generating)
        {
            throw new InvalidOperationException($"Cannot start validation. Current status: {Status}");
        }

        Status = GenerationStatus.Validating;
        QuestionsGenerated = questionsGenerated;
    }

    public void Complete(Guid questionSetId, int questionsValidated)
    {
        Status = questionsValidated == QuestionsGenerated
            ? GenerationStatus.Completed
            : GenerationStatus.CompletedPartial;

        QuestionSetId = questionSetId;
        QuestionsValidated = questionsValidated;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage, nameof(errorMessage));

        Status = GenerationStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = GenerationStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void IncrementRetryCount()
    {
        RetryCount++;
    }
}
