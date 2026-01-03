using AIExamForge.Application.Validation.Interfaces;
using AIExamForge.Domain.Interfaces;
using AIExamForge.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NJsonSchema;

namespace AIExamForge.Application.Validation;

/// <summary>
/// Validates question JSON against JSON schemas using NJsonSchema.
/// </summary>
public sealed class SchemaValidator : ISchemaValidator
{
    private readonly IItemTypeRegistry _itemTypeRegistry;
    private readonly ILogger<SchemaValidator> _logger;
    private readonly Dictionary<string, JsonSchema> _schemaCache = new(StringComparer.OrdinalIgnoreCase);
    private JsonSchema? _baseSchema;

    public SchemaValidator(
        IItemTypeRegistry itemTypeRegistry,
        ILogger<SchemaValidator> logger)
    {
        _itemTypeRegistry = itemTypeRegistry;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateQuestionAsync(
        string questionJson,
        string itemTypeCode,
        CancellationToken cancellationToken = default)
    {
        var allErrors = new List<SchemaError>();

        // First validate against base schema
        var baseResult = await ValidateBaseSchemaAsync(questionJson, cancellationToken);
        if (!baseResult.IsValid)
        {
            allErrors.AddRange(baseResult.SchemaErrors);
        }

        // Then validate against item type-specific schema
        var itemTypeResult = await ValidateItemTypeSchemaAsync(questionJson, itemTypeCode, cancellationToken);
        if (!itemTypeResult.IsValid)
        {
            allErrors.AddRange(itemTypeResult.SchemaErrors);
        }

        return allErrors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.FailSchema(allErrors);
    }

    public async Task<ValidationResult> ValidateBaseSchemaAsync(
        string questionJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var schema = await GetOrLoadBaseSchemaAsync(cancellationToken);
            if (schema == null)
            {
                return ValidationResult.FailSchema([new SchemaError { Path = "", Message = "Base question schema not available" }]);
            }

            var validationErrors = schema.Validate(questionJson);
            if (validationErrors.Count == 0)
            {
                return ValidationResult.Success();
            }

            var errors = validationErrors
                .Select(e => new SchemaError 
                { 
                    Path = e.Path ?? "", 
                    Message = e.Kind.ToString(),
                    SchemaKeyword = e.Kind.ToString()
                })
                .ToList();

            return ValidationResult.FailSchema(errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating against base schema");
            return ValidationResult.FailSchema([new SchemaError { Path = "", Message = $"Schema validation error: {ex.Message}" }]);
        }
    }

    public async Task<ValidationResult> ValidateItemTypeSchemaAsync(
        string questionJson,
        string itemTypeCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var schema = await GetOrLoadItemTypeSchemaAsync(itemTypeCode, cancellationToken);
            if (schema == null)
            {
                return ValidationResult.FailSchema([new SchemaError { Path = "", Message = $"Schema not available for item type: {itemTypeCode}" }]);
            }

            var validationErrors = schema.Validate(questionJson);
            if (validationErrors.Count == 0)
            {
                return ValidationResult.Success();
            }

            var errors = validationErrors
                .Select(e => new SchemaError 
                { 
                    Path = e.Path ?? "", 
                    Message = $"[{itemTypeCode}] {e.Kind}",
                    SchemaKeyword = e.Kind.ToString()
                })
                .ToList();

            return ValidationResult.FailSchema(errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating against item type schema {ItemTypeCode}", itemTypeCode);
            return ValidationResult.FailSchema([new SchemaError { Path = "", Message = $"Schema validation error for {itemTypeCode}: {ex.Message}" }]);
        }
    }

    private async Task<JsonSchema?> GetOrLoadBaseSchemaAsync(CancellationToken cancellationToken)
    {
        if (_baseSchema != null)
        {
            return _baseSchema;
        }

        try
        {
            var schemaJson = _itemTypeRegistry.GetBaseQuestionSchema();
            _baseSchema = await JsonSchema.FromJsonAsync(schemaJson, cancellationToken);
            return _baseSchema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load base question schema");
            return null;
        }
    }

    private async Task<JsonSchema?> GetOrLoadItemTypeSchemaAsync(
        string itemTypeCode,
        CancellationToken cancellationToken)
    {
        if (_schemaCache.TryGetValue(itemTypeCode, out var cached))
        {
            return cached;
        }

        try
        {
            var schemaJson = _itemTypeRegistry.GetSchema(itemTypeCode);
            var schema = await JsonSchema.FromJsonAsync(schemaJson, cancellationToken);
            _schemaCache[itemTypeCode] = schema;
            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load schema for item type {ItemTypeCode}", itemTypeCode);
            return null;
        }
    }
}
