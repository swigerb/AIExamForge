namespace AIExamForge.Domain.ValueObjects;

/// <summary>
/// Represents a lint rule violation per docs/exam-writing-style-rules.md.
/// </summary>
public sealed record LintViolation
{
    /// <summary>
    /// The rule that was violated.
    /// </summary>
    public required LintRule Rule { get; init; }

    /// <summary>
    /// Human-readable description of the violation.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The field or location where the violation was found (e.g., "stem", "choices[2]", "explanation").
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// The violating text, if applicable.
    /// </summary>
    public string? ViolatingText { get; init; }

    /// <summary>
    /// Severity of the violation. All exam style violations are errors (hard fail).
    /// </summary>
    public LintSeverity Severity { get; init; } = LintSeverity.Error;
}

/// <summary>
/// Severity level for lint violations per docs/exam-writing-style-rules.md Section 11.
/// </summary>
public enum LintSeverity
{
    /// <summary>
    /// Warning: Should be reviewed but doesn't invalidate the item.
    /// Note: Per the authoritative specs, all violations are hard fails.
    /// </summary>
    Warning = 0,

    /// <summary>
    /// Error: Item is invalid and must be rejected or regenerated.
    /// </summary>
    Error = 1
}
