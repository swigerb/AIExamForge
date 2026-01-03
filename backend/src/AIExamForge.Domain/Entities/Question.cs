using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Entities;

/// <summary>
/// Represents a generated exam question per question-base.schema.json and question.schema.json.
/// </summary>
public sealed class Question
{
    public const int MinTitleLength = 3;
    public const int MaxTitleLength = 120;
    public const int MinStemLength = 10;
    public const int MinIdLength = 8;
    public const int MaxTopics = 20;

    public Guid Id { get; private set; }

    /// <summary>
    /// The short item type code (e.g., M1, SC, DD).
    /// Must be one of the 19 valid item types.
    /// </summary>
    public ItemTypeCode ItemType { get; private set; } = null!;

    /// <summary>
    /// Short display title (3-120 characters).
    /// </summary>
    public string Title { get; private set; } = null!;

    /// <summary>
    /// The exam item stem/scenario text (no explanations/teaching text).
    /// Minimum 10 characters.
    /// </summary>
    public string Stem { get; private set; } = null!;

    /// <summary>
    /// Optional instruction statement shown before the stem when required by item type.
    /// </summary>
    public string? Instruction { get; private set; }

    public QuestionDifficulty Difficulty { get; private set; }

    /// <summary>
    /// Tagging information for the question.
    /// </summary>
    public QuestionTags Tags { get; private set; } = null!;

    /// <summary>
    /// Scoring model (dichotomous/polytomous) and max points.
    /// </summary>
    public QuestionScoring Scoring { get; private set; } = null!;

    /// <summary>
    /// Item-type-specific content as JSON.
    /// Must match the schema for ItemType.
    /// </summary>
    public string ContentJson { get; private set; } = null!;

    /// <summary>
    /// Study-mode explanation only. Must not be embedded in stem/choices.
    /// Hidden in Exam render mode per render-modes.md.
    /// </summary>
    public string? Rationale { get; private set; }

    /// <summary>
    /// The parent question set.
    /// </summary>
    public Guid QuestionSetId { get; private set; }

    /// <summary>
    /// Order within the question set.
    /// </summary>
    public int OrderIndex { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastModifiedAt { get; private set; }

    private Question() { } // EF Core constructor

    public static Question Create(
        ItemTypeCode itemType,
        string title,
        string stem,
        QuestionDifficulty difficulty,
        QuestionTags tags,
        QuestionScoring scoring,
        string contentJson,
        Guid questionSetId,
        int orderIndex,
        string? instruction = null,
        string? rationale = null)
    {
        // Validate title length per schema
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        if (title.Length < MinTitleLength || title.Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"Title must be between {MinTitleLength} and {MaxTitleLength} characters. Got: {title.Length}",
                nameof(title));
        }

        // Validate stem length per schema
        ArgumentException.ThrowIfNullOrWhiteSpace(stem, nameof(stem));
        if (stem.Length < MinStemLength)
        {
            throw new ArgumentException(
                $"Stem must be at least {MinStemLength} characters. Got: {stem.Length}",
                nameof(stem));
        }

        ArgumentNullException.ThrowIfNull(itemType, nameof(itemType));
        ArgumentNullException.ThrowIfNull(tags, nameof(tags));
        ArgumentNullException.ThrowIfNull(scoring, nameof(scoring));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentJson, nameof(contentJson));

        // Validate scoring model matches item type
        var expectedModel = itemType.IsDichotomous ? "dichotomous" : "polytomous";
        if (scoring.Model != expectedModel)
        {
            throw new ArgumentException(
                $"Scoring model '{scoring.Model}' does not match item type '{itemType.Code}' which requires '{expectedModel}'.",
                nameof(scoring));
        }

        return new Question
        {
            Id = Guid.NewGuid(),
            ItemType = itemType,
            Title = title,
            Stem = stem,
            Instruction = instruction,
            Difficulty = difficulty,
            Tags = tags,
            Scoring = scoring,
            ContentJson = contentJson,
            Rationale = rationale,
            QuestionSetId = questionSetId,
            OrderIndex = orderIndex,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateStem(string stem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stem, nameof(stem));
        if (stem.Length < MinStemLength)
        {
            throw new ArgumentException(
                $"Stem must be at least {MinStemLength} characters. Got: {stem.Length}",
                nameof(stem));
        }

        Stem = stem;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateContent(string contentJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentJson, nameof(contentJson));
        ContentJson = contentJson;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateRationale(string? rationale)
    {
        Rationale = rationale;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateOrderIndex(int orderIndex)
    {
        if (orderIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderIndex), "Order index cannot be negative.");
        }

        OrderIndex = orderIndex;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Tagging information per question-base.schema.json.
/// </summary>
public sealed record QuestionTags
{
    public string Exam { get; }
    public string Objective { get; }
    public string Section { get; }
    public IReadOnlyList<string> Topics { get; }

    private QuestionTags(string exam, string objective, string section, IReadOnlyList<string> topics)
    {
        Exam = exam;
        Objective = objective;
        Section = section;
        Topics = topics;
    }

    public static QuestionTags Create(string exam, string objective, string section, IEnumerable<string>? topics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exam, nameof(exam));
        if (exam.Length < 2)
        {
            throw new ArgumentException("Exam must be at least 2 characters.", nameof(exam));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(objective, nameof(objective));
        if (objective.Length < 2)
        {
            throw new ArgumentException("Objective must be at least 2 characters.", nameof(objective));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(section, nameof(section));
        if (section.Length < 2)
        {
            throw new ArgumentException("Section must be at least 2 characters.", nameof(section));
        }

        var topicList = topics?.ToList() ?? [];
        if (topicList.Count > Question.MaxTopics)
        {
            throw new ArgumentException($"Cannot have more than {Question.MaxTopics} topics.", nameof(topics));
        }

        foreach (var topic in topicList)
        {
            if (string.IsNullOrWhiteSpace(topic) || topic.Length < 2)
            {
                throw new ArgumentException("Each topic must be at least 2 characters.", nameof(topics));
            }
        }

        return new QuestionTags(exam, objective, section, topicList.AsReadOnly());
    }
}

/// <summary>
/// Scoring information per question-base.schema.json.
/// </summary>
public sealed record QuestionScoring
{
    public const int MinMaxPoints = 1;
    public const int MaxMaxPoints = 20;

    public string Model { get; } // "dichotomous" or "polytomous"
    public int MaxPoints { get; }

    private QuestionScoring(string model, int maxPoints)
    {
        Model = model;
        MaxPoints = maxPoints;
    }

    public static QuestionScoring Dichotomous(int maxPoints = 1)
    {
        ValidateMaxPoints(maxPoints);
        return new QuestionScoring("dichotomous", maxPoints);
    }

    public static QuestionScoring Polytomous(int maxPoints)
    {
        ValidateMaxPoints(maxPoints);
        return new QuestionScoring("polytomous", maxPoints);
    }

    private static void ValidateMaxPoints(int maxPoints)
    {
        if (maxPoints < MinMaxPoints || maxPoints > MaxMaxPoints)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPoints),
                $"Max points must be between {MinMaxPoints} and {MaxMaxPoints}. Got: {maxPoints}");
        }
    }
}
