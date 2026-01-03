using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Application.Validation.Interfaces;

/// <summary>
/// Validates question JSON against JSON schemas.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Validates a question JSON against the base question schema and the specific item type schema.
    /// </summary>
    /// <param name="questionJson">The question JSON to validate</param>
    /// <param name="itemTypeCode">The item type code for schema selection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any errors</returns>
    Task<ValidationResult> ValidateQuestionAsync(
        string questionJson,
        string itemTypeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a question JSON against only the base question schema.
    /// </summary>
    /// <param name="questionJson">The question JSON to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any errors</returns>
    Task<ValidationResult> ValidateBaseSchemaAsync(
        string questionJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a question JSON against only the item type-specific schema.
    /// </summary>
    /// <param name="questionJson">The question JSON to validate</param>
    /// <param name="itemTypeCode">The item type code for schema selection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any errors</returns>
    Task<ValidationResult> ValidateItemTypeSchemaAsync(
        string questionJson,
        string itemTypeCode,
        CancellationToken cancellationToken = default);
}
