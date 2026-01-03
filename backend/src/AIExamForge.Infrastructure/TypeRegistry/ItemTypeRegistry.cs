using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Interfaces;
using AIExamForge.Domain.ValueObjects;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Infrastructure.TypeRegistry;

/// <summary>
/// Implementation of IItemTypeRegistry that loads schemas and prompts from the file system.
/// Caches loaded content at startup for performance.
/// </summary>
public sealed class ItemTypeRegistry : IItemTypeRegistry
{
    private readonly ILogger<ItemTypeRegistry> _logger;
    private readonly Dictionary<string, string> _schemas = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _prompts = new(StringComparer.OrdinalIgnoreCase);
    private string _baseQuestionSchema = string.Empty;
    private string _masterQuestionSchema = string.Empty;

    private static readonly string[] ValidCodes =
    [
        "M1", "BA", "DA", "SC", "DOMC", "CHOOSE_N",
        "AS", "BL", "MW", "HA", "DD", "MG",
        "GI", "MS", "PA", "CA", "AN", "LABS", "SA"
    ];

    public ItemTypeRegistry(ILogger<ItemTypeRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes the registry by loading all schemas and prompts from disk.
    /// Should be called at application startup.
    /// </summary>
    /// <param name="schemasBasePath">Base path to the docs/schemas/common directory</param>
    /// <param name="promptsBasePath">Base path to the prompts directory</param>
    public async Task InitializeAsync(string schemasBasePath, string promptsBasePath)
    {
        _logger.LogInformation("Initializing ItemTypeRegistry from {SchemasPath} and {PromptsPath}",
            schemasBasePath, promptsBasePath);

        // Load master question schema
        var masterSchemaPath = Path.Combine(schemasBasePath, "question.schema.json");
        if (File.Exists(masterSchemaPath))
        {
            _masterQuestionSchema = await File.ReadAllTextAsync(masterSchemaPath);
            _logger.LogDebug("Loaded master question schema from {Path}", masterSchemaPath);
        }
        else
        {
            _logger.LogWarning("Master question schema not found at {Path}", masterSchemaPath);
        }

        // Load base question schema
        var baseSchemaPath = Path.Combine(schemasBasePath, "question-base.schema.json");
        if (File.Exists(baseSchemaPath))
        {
            _baseQuestionSchema = await File.ReadAllTextAsync(baseSchemaPath);
            _logger.LogDebug("Loaded base question schema from {Path}", baseSchemaPath);
        }
        else
        {
            _logger.LogWarning("Base question schema not found at {Path}", baseSchemaPath);
        }

        // Load item-type schemas
        var itemTypeSchemasPath = Path.Combine(schemasBasePath, "item-types");
        if (Directory.Exists(itemTypeSchemasPath))
        {
            foreach (var code in ValidCodes)
            {
                var schemaPath = Path.Combine(itemTypeSchemasPath, $"{code}.schema.json");
                if (File.Exists(schemaPath))
                {
                    _schemas[code] = await File.ReadAllTextAsync(schemaPath);
                    _logger.LogDebug("Loaded schema for {Code} from {Path}", code, schemaPath);
                }
                else
                {
                    _logger.LogWarning("Schema not found for item type {Code} at {Path}", code, schemaPath);
                }
            }
        }
        else
        {
            _logger.LogWarning("Item-type schemas directory not found at {Path}", itemTypeSchemasPath);
        }

        // Load prompts
        var itemTypePromptsPath = Path.Combine(promptsBasePath, "item-types");
        if (Directory.Exists(itemTypePromptsPath))
        {
            foreach (var code in ValidCodes)
            {
                var promptPath = Path.Combine(itemTypePromptsPath, $"{code}.md");
                if (File.Exists(promptPath))
                {
                    _prompts[code] = await File.ReadAllTextAsync(promptPath);
                    _logger.LogDebug("Loaded prompt for {Code} from {Path}", code, promptPath);
                }
                else
                {
                    _logger.LogWarning("Prompt not found for item type {Code} at {Path}", code, promptPath);
                }
            }
        }
        else
        {
            _logger.LogWarning("Item-type prompts directory not found at {Path}", itemTypePromptsPath);
        }

        _logger.LogInformation("ItemTypeRegistry initialized with {SchemaCount} schemas and {PromptCount} prompts",
            _schemas.Count, _prompts.Count);
    }

    public QuestionTypeDefinition GetDefinition(string code)
    {
        return QuestionTypeDefinition.GetByCode(code);
    }

    public QuestionTypeDefinition GetDefinition(ItemTypeCode code)
    {
        return QuestionTypeDefinition.GetByCode(code);
    }

    public IReadOnlyCollection<QuestionTypeDefinition> GetAllDefinitions()
    {
        return QuestionTypeDefinition.All.Values.ToList().AsReadOnly();
    }

    public string GetSchema(string code)
    {
        if (!_schemas.TryGetValue(code, out var schema))
        {
            throw new ArgumentException($"Schema not found for item type: {code}", nameof(code));
        }
        return schema;
    }

    public string GetSchema(ItemTypeCode code)
    {
        return GetSchema(code.Code);
    }

    public string GetPrompt(string code)
    {
        if (!_prompts.TryGetValue(code, out var prompt))
        {
            throw new ArgumentException($"Prompt not found for item type: {code}", nameof(code));
        }
        return prompt;
    }

    public string GetPrompt(ItemTypeCode code)
    {
        return GetPrompt(code.Code);
    }

    public string GetBaseQuestionSchema()
    {
        if (string.IsNullOrEmpty(_baseQuestionSchema))
        {
            throw new InvalidOperationException("Base question schema not loaded. Call InitializeAsync first.");
        }
        return _baseQuestionSchema;
    }

    public string GetMasterQuestionSchema()
    {
        if (string.IsNullOrEmpty(_masterQuestionSchema))
        {
            throw new InvalidOperationException("Master question schema not loaded. Call InitializeAsync first.");
        }
        return _masterQuestionSchema;
    }

    public bool IsValidCode(string code)
    {
        return ItemTypeCode.TryCreate(code, out _);
    }
}
