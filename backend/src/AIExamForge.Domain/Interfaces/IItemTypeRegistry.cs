using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Interfaces;

/// <summary>
/// Registry for item type definitions, schemas, and prompts.
/// Provides access to the 19 supported item types per docs/item-types.md.
/// </summary>
public interface IItemTypeRegistry
{
    /// <summary>
    /// Gets the definition for a specific item type code.
    /// </summary>
    /// <param name="code">The item type code (e.g., "M1", "BA", "CHOOSE_N")</param>
    /// <returns>The item type definition</returns>
    /// <exception cref="ArgumentException">If the code is not valid</exception>
    QuestionTypeDefinition GetDefinition(string code);

    /// <summary>
    /// Gets the definition for a specific item type code.
    /// </summary>
    /// <param name="code">The item type code</param>
    /// <returns>The item type definition</returns>
    QuestionTypeDefinition GetDefinition(ItemTypeCode code);

    /// <summary>
    /// Gets all registered item type definitions.
    /// </summary>
    IReadOnlyCollection<QuestionTypeDefinition> GetAllDefinitions();

    /// <summary>
    /// Gets the JSON schema for validating questions of a specific type.
    /// </summary>
    /// <param name="code">The item type code</param>
    /// <returns>The JSON schema string</returns>
    string GetSchema(string code);

    /// <summary>
    /// Gets the JSON schema for validating questions of a specific type.
    /// </summary>
    /// <param name="code">The item type code</param>
    /// <returns>The JSON schema string</returns>
    string GetSchema(ItemTypeCode code);

    /// <summary>
    /// Gets the prompt fragment for generating questions of a specific type.
    /// </summary>
    /// <param name="code">The item type code</param>
    /// <returns>The prompt fragment markdown</returns>
    string GetPrompt(string code);

    /// <summary>
    /// Gets the prompt fragment for generating questions of a specific type.
    /// </summary>
    /// <param name="code">The item type code</param>
    /// <returns>The prompt fragment markdown</returns>
    string GetPrompt(ItemTypeCode code);

    /// <summary>
    /// Gets the base question schema that all item types must conform to.
    /// </summary>
    string GetBaseQuestionSchema();

    /// <summary>
    /// Gets the master question schema.
    /// </summary>
    string GetMasterQuestionSchema();

    /// <summary>
    /// Checks if an item type code is valid.
    /// </summary>
    /// <param name="code">The item type code to check</param>
    /// <returns>True if valid, false otherwise</returns>
    bool IsValidCode(string code);
}

