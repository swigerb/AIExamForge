namespace AIExamForge.Domain.ValueObjects;

/// <summary>
/// Result of validating a question against schemas and lint rules.
/// Per NFR-014: If any rule fails, generation must fail or regenerate.
/// Silent correction is not allowed.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => SchemaErrors.Count == 0 && LintViolations.All(v => v.Severity != LintSeverity.Error);

    /// <summary>
    /// Schema validation errors from JSON Schema validation.
    /// </summary>
    public IReadOnlyList<SchemaError> SchemaErrors { get; init; } = [];

    /// <summary>
    /// Lint rule violations from style guide enforcement.
    /// </summary>
    public IReadOnlyList<LintViolation> LintViolations { get; init; } = [];

    /// <summary>
    /// Item-type-specific validation errors that schema cannot express.
    /// </summary>
    public IReadOnlyList<ItemTypeValidationError> ItemTypeErrors { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new();

    /// <summary>
    /// Creates a failed validation result with schema errors.
    /// </summary>
    public static ValidationResult FailSchema(IEnumerable<SchemaError> errors) =>
        new() { SchemaErrors = errors.ToList() };

    /// <summary>
    /// Creates a failed validation result with lint violations.
    /// </summary>
    public static ValidationResult FailLint(IEnumerable<LintViolation> violations) =>
        new() { LintViolations = violations.ToList() };

    /// <summary>
    /// Combines multiple validation results.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results) => new()
    {
        SchemaErrors = results.SelectMany(r => r.SchemaErrors).ToList(),
        LintViolations = results.SelectMany(r => r.LintViolations).ToList(),
        ItemTypeErrors = results.SelectMany(r => r.ItemTypeErrors).ToList()
    };
}

/// <summary>
/// Schema validation error from JSON Schema validation.
/// </summary>
public sealed record SchemaError
{
    /// <summary>
    /// JSON path to the invalid property.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Error message from the schema validator.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The schema keyword that failed (e.g., "required", "type", "enum").
    /// </summary>
    public string? SchemaKeyword { get; init; }
}

/// <summary>
/// Item-type-specific validation error for constraints that schema cannot express.
/// </summary>
public sealed record ItemTypeValidationError
{
    /// <summary>
    /// The item type code this error applies to.
    /// </summary>
    public required ItemTypeCode ItemType { get; init; }

    /// <summary>
    /// The constraint that was violated.
    /// </summary>
    public required string Constraint { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }
}
