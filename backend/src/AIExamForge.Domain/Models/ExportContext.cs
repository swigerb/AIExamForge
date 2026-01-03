using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Models;

/// <summary>
/// T137: Context for exporting questions, including render mode and metadata.
/// Per FR-049-055: Exam Mode omits rationales, Study Mode includes them.
/// </summary>
public sealed class ExportContext
{
    /// <summary>
    /// The question set to export.
    /// </summary>
    public required QuestionSet QuestionSet { get; init; }

    /// <summary>
    /// The render mode controlling rationale visibility.
    /// </summary>
    public required RenderMode RenderMode { get; init; }

    /// <summary>
    /// Title for the exported document.
    /// </summary>
    public string Title { get; init; } = "Practice Exam Questions";

    /// <summary>
    /// Optional subtitle/description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The exam name (from QuestionSet or override).
    /// </summary>
    public string ExamName => QuestionSet.ExamName ?? "Practice Exam";

    /// <summary>
    /// Number of questions in the set.
    /// </summary>
    public int QuestionCount => QuestionSet.TotalQuestions;

    /// <summary>
    /// Generation timestamp for metadata.
    /// </summary>
    public DateTimeOffset GeneratedAt => QuestionSet.CreatedAt;

    /// <summary>
    /// Whether to include explanations (based on render mode).
    /// Per FR-050: Exam Mode = no rationales.
    /// Per FR-051: Study Mode = rationales in separate section.
    /// </summary>
    public bool IncludeExplanations => RenderMode == RenderMode.Study;

    /// <summary>
    /// Standard disclaimer text per FR-016 and FR-020c.
    /// Must appear in all exports.
    /// </summary>
    public static string DisclaimerText =>
        """
        DISCLAIMER: These practice questions were generated using AI technology and 
        content from Microsoft Learn documentation. While we strive for accuracy, 
        AI-generated content may contain errors or inaccuracies. These questions 
        are intended for study purposes only and should not be considered official 
        certification materials. Always refer to official Microsoft documentation 
        and certification study guides for authoritative exam preparation information.

        Microsoft, Azure, and related trademarks are property of Microsoft Corporation.
        """;

    /// <summary>
    /// Short disclaimer for headers/footers.
    /// </summary>
    public static string ShortDisclaimer =>
        "AI-generated practice questions. Not official certification materials.";

    /// <summary>
    /// Creates an export context from a question set with the specified render mode.
    /// </summary>
    public static ExportContext Create(
        QuestionSet questionSet,
        RenderMode renderMode,
        string? title = null,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(questionSet, nameof(questionSet));

        return new ExportContext
        {
            QuestionSet = questionSet,
            RenderMode = renderMode,
            Title = title ?? "Practice Exam Questions",
            Description = description
        };
    }
}
