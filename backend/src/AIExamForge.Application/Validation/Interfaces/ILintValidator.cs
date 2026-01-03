using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Application.Validation.Interfaces;

/// <summary>
/// Validates question text against exam writing style rules per docs/exam-writing-style-guide.md.
/// </summary>
public interface ILintValidator
{
    /// <summary>
    /// Validates question text against all applicable lint rules.
    /// </summary>
    /// <param name="text">The text to validate (stem, choices, rationale, etc.)</param>
    /// <param name="context">Context for validation (e.g., "stem", "choice", "rationale")</param>
    /// <returns>Collection of lint violations found</returns>
    IReadOnlyList<LintViolation> Validate(string text, string context = "text");

    /// <summary>
    /// Validates all text fields in a question.
    /// </summary>
    /// <param name="stem">The question stem</param>
    /// <param name="instruction">Optional instruction text</param>
    /// <param name="choices">Answer choices</param>
    /// <param name="rationale">Optional rationale/explanation</param>
    /// <returns>Collection of all lint violations found</returns>
    IReadOnlyList<LintViolation> ValidateQuestion(
        string stem,
        string? instruction,
        IEnumerable<string> choices,
        string? rationale);

    /// <summary>
    /// Validates a specific text against a single rule.
    /// </summary>
    /// <param name="text">The text to validate</param>
    /// <param name="rule">The rule to check</param>
    /// <returns>Lint violation if rule is violated, null otherwise</returns>
    LintViolation? ValidateRule(string text, LintRule rule);
}
