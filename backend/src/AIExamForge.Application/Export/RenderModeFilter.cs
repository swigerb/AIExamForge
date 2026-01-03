using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Models;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Application.Export;

/// <summary>
/// T142: Filter that strips explanations when in Exam Mode.
/// Per FR-050: Exam Mode exports do not include rationales.
/// Per FR-051: Study Mode exports include rationales in a separate section.
/// </summary>
public static class RenderModeFilter
{
    /// <summary>
    /// Filters a question based on render mode.
    /// In Exam Mode, removes rationale/explanation content.
    /// </summary>
    /// <param name="question">The question to filter.</param>
    /// <param name="context">Export context containing render mode.</param>
    /// <returns>Filtered question data.</returns>
    public static FilteredQuestion FilterQuestion(Question question, ExportContext context)
    {
        ArgumentNullException.ThrowIfNull(question, nameof(question));
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        return new FilteredQuestion
        {
            Id = question.Id,
            ItemTypeCode = question.ItemType.Code,
            Title = question.Title,
            Stem = question.Stem,
            Instruction = question.Instruction,
            Difficulty = question.Difficulty,
            Content = question.ContentJson,
            Tags = question.Tags,
            Scoring = question.Scoring,
            // Only include rationale in Study Mode
            Rationale = context.IncludeExplanations ? question.Rationale : null,
            OrderIndex = question.OrderIndex
        };
    }

    /// <summary>
    /// Filters all questions in a set based on render mode.
    /// </summary>
    /// <param name="context">Export context with questions and render mode.</param>
    /// <returns>Collection of filtered questions.</returns>
    public static IReadOnlyList<FilteredQuestion> FilterQuestions(ExportContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        return context.QuestionSet.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => FilterQuestion(q, context))
            .ToList();
    }

    /// <summary>
    /// Gets all rationales for Study Mode exports (separate section).
    /// Per FR-051: Study Mode includes rationales in a separate section.
    /// </summary>
    /// <param name="context">Export context.</param>
    /// <returns>Collection of question rationales, or empty if Exam Mode.</returns>
    public static IReadOnlyList<QuestionRationale> GetRationalesSection(ExportContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        if (!context.IncludeExplanations)
        {
            return [];
        }

        return context.QuestionSet.Questions
            .Where(q => !string.IsNullOrWhiteSpace(q.Rationale))
            .OrderBy(q => q.OrderIndex)
            .Select((q, index) => new QuestionRationale
            {
                QuestionNumber = index + 1,
                QuestionId = q.Id,
                QuestionStemPreview = q.Stem.Length > 100 ? q.Stem[..100] + "..." : q.Stem,
                Rationale = q.Rationale!
            })
            .ToList();
    }
}

/// <summary>
/// Question data filtered based on render mode.
/// </summary>
public sealed record FilteredQuestion
{
    public Guid Id { get; init; }
    public required string ItemTypeCode { get; init; }
    public required string Title { get; init; }
    public required string Stem { get; init; }
    public string? Instruction { get; init; }
    public QuestionDifficulty Difficulty { get; init; }
    public required string Content { get; init; }
    public required QuestionTags Tags { get; init; }
    public required QuestionScoring Scoring { get; init; }
    public string? Rationale { get; init; }
    public int OrderIndex { get; init; }
}

/// <summary>
/// Rationale data for the separate explanations section in Study Mode.
/// </summary>
public sealed record QuestionRationale
{
    public int QuestionNumber { get; init; }
    public Guid QuestionId { get; init; }
    public required string QuestionStemPreview { get; init; }
    public required string Rationale { get; init; }
}
