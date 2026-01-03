using System.Text.Json;
using AIExamForge.Application.Validation.Interfaces;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Application.Validation;

/// <summary>
/// Validation pipeline for generated questions.
/// Orchestrates: Schema validation → Lint validation → Result aggregation.
/// Per NFR-014: If any rule fails, generation must fail or regenerate.
/// Silent correction is NOT allowed.
/// </summary>
public sealed class QuestionValidationPipeline
{
    private readonly ISchemaValidator _schemaValidator;
    private readonly ILintValidator _lintValidator;
    private readonly ILogger<QuestionValidationPipeline> _logger;

    public QuestionValidationPipeline(
        ISchemaValidator schemaValidator,
        ILintValidator lintValidator,
        ILogger<QuestionValidationPipeline> logger)
    {
        _schemaValidator = schemaValidator;
        _lintValidator = lintValidator;
        _logger = logger;
    }

    /// <summary>
    /// Validates a single question through the complete pipeline.
    /// </summary>
    /// <param name="questionJson">The question JSON to validate.</param>
    /// <param name="itemTypeCode">The item type code for schema selection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comprehensive validation result.</returns>
    public async Task<QuestionPipelineResult> ValidateQuestionAsync(
        string questionJson,
        string itemTypeCode,
        CancellationToken cancellationToken = default)
    {
        var result = new QuestionPipelineResult(Guid.NewGuid().ToString("N")[..8]);

        try
        {
            // Step 1: Parse JSON to extract text fields for lint validation
            var textFields = ExtractTextFields(questionJson);

            // Step 2: Schema validation
            _logger.LogDebug("Validating question against schema for item type {ItemType}", itemTypeCode);
            var schemaResult = await _schemaValidator.ValidateQuestionAsync(questionJson, itemTypeCode, cancellationToken);

            if (!schemaResult.IsValid)
            {
                foreach (var error in schemaResult.SchemaErrors)
                {
                    result.AddSchemaError(error.Path, error.Message, error.SchemaKeyword);
                }

                _logger.LogWarning("Schema validation failed for question: {ErrorCount} errors", schemaResult.SchemaErrors.Count);
            }

            // Step 3: Lint validation (exam writing style)
            _logger.LogDebug("Validating question text against lint rules");
            var lintViolations = _lintValidator.ValidateQuestion(
                textFields.Stem,
                textFields.Instruction,
                textFields.Choices,
                textFields.Rationale);

            foreach (var violation in lintViolations)
            {
                result.AddLintViolation(violation);
            }

            if (lintViolations.Count > 0)
            {
                var errorCount = lintViolations.Count(v => v.Severity == LintSeverity.Error);
                var warningCount = lintViolations.Count(v => v.Severity == LintSeverity.Warning);
                _logger.LogWarning("Lint validation found {Errors} errors and {Warnings} warnings",
                    errorCount, warningCount);
            }

            // Step 4: Additional item-type-specific validations
            var itemTypeErrors = ValidateItemTypeSpecific(questionJson, itemTypeCode, textFields);
            foreach (var error in itemTypeErrors)
            {
                result.AddItemTypeError(error);
            }

            result.MarkComplete();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse question JSON for validation");
            result.AddSchemaError("$", "Invalid JSON format: " + ex.Message);
            result.MarkComplete();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during question validation");
            result.AddSchemaError("$", "Validation error: " + ex.Message);
            result.MarkComplete();
        }

        return result;
    }

    /// <summary>
    /// Validates multiple questions in batch.
    /// </summary>
    public async Task<BatchValidationResult> ValidateBatchAsync(
        IEnumerable<(string Json, string ItemTypeCode)> questions,
        CancellationToken cancellationToken = default)
    {
        var results = new List<QuestionPipelineResult>();
        var index = 0;

        foreach (var (json, itemTypeCode) in questions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("Validating question {Index}", index);
            var result = await ValidateQuestionAsync(json, itemTypeCode, cancellationToken);
            results.Add(result);
            index++;
        }

        return new BatchValidationResult(results);
    }

    /// <summary>
    /// Validates a question entity (already parsed).
    /// </summary>
    public async Task<QuestionPipelineResult> ValidateQuestionEntityAsync(
        Question question,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(new
        {
            id = question.Id.ToString(),
            itemType = question.ItemType.Code,
            title = question.Title,
            stem = question.Stem,
            instruction = question.Instruction,
            difficulty = question.Difficulty.ToString().ToLowerInvariant(),
            content = JsonDocument.Parse(question.ContentJson).RootElement,
            rationale = question.Rationale
        });

        return await ValidateQuestionAsync(json, question.ItemType.Code, cancellationToken);
    }

    private QuestionTextFields ExtractTextFields(string questionJson)
    {
        using var doc = JsonDocument.Parse(questionJson);
        var root = doc.RootElement;

        var stem = root.TryGetProperty("stem", out var stemProp) ? stemProp.GetString() ?? "" : "";
        var instruction = root.TryGetProperty("instruction", out var instrProp) ? instrProp.GetString() : null;
        var rationale = root.TryGetProperty("rationale", out var ratProp) ? ratProp.GetString() : null;

        var choices = new List<string>();
        if (root.TryGetProperty("content", out var content) &&
            content.TryGetProperty("choices", out var choicesArray) &&
            choicesArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choicesArray.EnumerateArray())
            {
                if (choice.TryGetProperty("text", out var text))
                {
                    choices.Add(text.GetString() ?? "");
                }
            }
        }

        return new QuestionTextFields(stem, instruction, choices, rationale);
    }

    private List<ItemTypeValidationError> ValidateItemTypeSpecific(
        string questionJson,
        string itemTypeCode,
        QuestionTextFields textFields)
    {
        var errors = new List<ItemTypeValidationError>();

        try
        {
            using var doc = JsonDocument.Parse(questionJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("content", out var content))
            {
                errors.Add(CreateItemTypeError(itemTypeCode, "content_required", "Content object is required for all question types."));
                return errors;
            }

            // Validate based on item type category
            switch (itemTypeCode.ToUpperInvariant())
            {
                case "M1":
                case "BA":
                case "DA":
                case "SC":
                case "DOMC":
                    // Dichotomous: Must have choices with exactly one correct
                    ValidateDichotomousContent(content, itemTypeCode, errors);
                    break;

                case "CHOOSE_N":
                    // Multiple select with N correct
                    ValidateChooseNContent(content, errors);
                    break;

                case "DD":
                case "MG":
                    // Drag and drop / Matching
                    ValidateMatchingContent(content, itemTypeCode, errors);
                    break;

                case "HA":
                case "GI":
                    // Hot area / Graphic items
                    ValidateGraphicContent(content, itemTypeCode, errors);
                    break;

                case "SA":
                case "AN":
                    // Short answer / Numeric
                    ValidateFreeResponseContent(content, itemTypeCode, errors);
                    break;

                default:
                    // For other types, basic choice validation if choices exist
                    if (content.TryGetProperty("choices", out _))
                    {
                        ValidateBasicChoices(content, itemTypeCode, errors);
                    }
                    break;
            }
        }
        catch (JsonException)
        {
            // Already handled in main validation
        }

        return errors;
    }

    private static ItemTypeValidationError CreateItemTypeError(string itemTypeCode, string constraint, string message)
    {
        return new ItemTypeValidationError
        {
            ItemType = ItemTypeCode.Create(itemTypeCode),
            Constraint = constraint,
            Message = message
        };
    }

    private static void ValidateDichotomousContent(JsonElement content, string itemTypeCode, List<ItemTypeValidationError> errors)
    {
        if (!content.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            errors.Add(CreateItemTypeError(itemTypeCode, "choices_required", "Dichotomous items must have a choices array."));
            return;
        }

        var choiceArray = choices.EnumerateArray().ToList();
        if (choiceArray.Count < 2)
        {
            errors.Add(CreateItemTypeError(itemTypeCode, "minimum_choices", "Dichotomous items must have at least 2 choices."));
        }

        var correctCount = choiceArray.Count(c =>
            c.TryGetProperty("isCorrect", out var isCorrect) && isCorrect.GetBoolean());

        if (correctCount != 1)
        {
            errors.Add(CreateItemTypeError(itemTypeCode, "single_correct",
                $"Dichotomous items must have exactly 1 correct choice. Found: {correctCount}"));
        }
    }

    private static void ValidateChooseNContent(JsonElement content, List<ItemTypeValidationError> errors)
    {
        const string itemType = "CHOOSE_N";
        if (!content.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            errors.Add(CreateItemTypeError(itemType, "choices_required", "CHOOSE_N items must have a choices array."));
            return;
        }

        var correctCount = choices.EnumerateArray().Count(c =>
            c.TryGetProperty("isCorrect", out var isCorrect) && isCorrect.GetBoolean());

        if (correctCount < 2)
        {
            errors.Add(CreateItemTypeError(itemType, "multiple_correct",
                $"CHOOSE_N items must have at least 2 correct choices. Found: {correctCount}"));
        }
    }

    private static void ValidateMatchingContent(JsonElement content, string itemTypeCode, List<ItemTypeValidationError> errors)
    {
        var hasSources = content.TryGetProperty("sources", out var sources) &&
                        sources.ValueKind == JsonValueKind.Array &&
                        sources.GetArrayLength() > 0;

        var hasTargets = content.TryGetProperty("targets", out var targets) &&
                        targets.ValueKind == JsonValueKind.Array &&
                        targets.GetArrayLength() > 0;

        if (!hasSources || !hasTargets)
        {
            errors.Add(CreateItemTypeError(itemTypeCode, "matching_structure",
                "Matching/drag-drop items must have non-empty sources and targets arrays."));
        }
    }

    private static void ValidateGraphicContent(JsonElement content, string itemTypeCode, List<ItemTypeValidationError> errors)
    {
        var hasImage = content.TryGetProperty("imageUrl", out var imageUrl) &&
                      imageUrl.ValueKind == JsonValueKind.String &&
                      !string.IsNullOrWhiteSpace(imageUrl.GetString());

        var hasAreas = content.TryGetProperty("hotspots", out var hotspots) &&
                      hotspots.ValueKind == JsonValueKind.Array &&
                      hotspots.GetArrayLength() > 0;

        if (!hasImage)
        {
            errors.Add(CreateItemTypeError(itemTypeCode, "image_required", "Graphic items must have an imageUrl."));
        }

        if (!hasAreas)
        {
            errors.Add(CreateItemTypeError(itemTypeCode, "hotspots_required", "Graphic items must have at least one hotspot/area."));
        }
    }

    private static void ValidateFreeResponseContent(JsonElement content, string itemTypeCode, List<ItemTypeValidationError> errors)
    {
        var hasCorrectAnswer = content.TryGetProperty("correctAnswer", out var answer) &&
                              (answer.ValueKind == JsonValueKind.String || answer.ValueKind == JsonValueKind.Number);

        if (!hasCorrectAnswer)
        {
            errors.Add(CreateItemTypeError(itemTypeCode, "answer_required", "Free response items must have a correctAnswer."));
        }
    }

    private static void ValidateBasicChoices(JsonElement content, string itemTypeCode, List<ItemTypeValidationError> errors)
    {
        if (!content.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var choiceCount = 0;
        foreach (var choice in choices.EnumerateArray())
        {
            choiceCount++;
            if (!choice.TryGetProperty("text", out var text) ||
                text.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(text.GetString()))
            {
                errors.Add(CreateItemTypeError(itemTypeCode, "choice_text_required",
                    $"Choice {choiceCount} must have non-empty text."));
            }
        }
    }

    private sealed record QuestionTextFields(
        string Stem,
        string? Instruction,
        List<string> Choices,
        string? Rationale);
}

/// <summary>
/// Result of validating a single question through the pipeline.
/// </summary>
public sealed class QuestionPipelineResult
{
    public string ValidationId { get; }
    public bool IsValid => _schemaErrors.Count == 0 &&
                          !_lintViolations.Any(v => v.Severity == LintSeverity.Error) &&
                          _itemTypeErrors.Count == 0;
    public bool IsComplete { get; private set; }

    public IReadOnlyList<PipelineSchemaError> SchemaErrors => _schemaErrors.AsReadOnly();
    public IReadOnlyList<LintViolation> LintViolations => _lintViolations.AsReadOnly();
    public IReadOnlyList<ItemTypeValidationError> ItemTypeErrors => _itemTypeErrors.AsReadOnly();

    public int TotalErrors => _schemaErrors.Count +
                             _lintViolations.Count(v => v.Severity == LintSeverity.Error) +
                             _itemTypeErrors.Count;

    public int TotalWarnings => _lintViolations.Count(v => v.Severity == LintSeverity.Warning);

    private readonly List<PipelineSchemaError> _schemaErrors = [];
    private readonly List<LintViolation> _lintViolations = [];
    private readonly List<ItemTypeValidationError> _itemTypeErrors = [];

    public QuestionPipelineResult(string validationId)
    {
        ValidationId = validationId;
    }

    public void AddSchemaError(string path, string message, string? keyword = null)
    {
        _schemaErrors.Add(new PipelineSchemaError
        {
            Path = path,
            Message = message,
            SchemaKeyword = keyword
        });
    }

    public void AddLintViolation(LintViolation violation)
    {
        _lintViolations.Add(violation);
    }

    public void AddItemTypeError(ItemTypeValidationError error)
    {
        _itemTypeErrors.Add(error);
    }

    public void MarkComplete()
    {
        IsComplete = true;
    }

    /// <summary>
    /// Gets a summary string of all validation issues.
    /// </summary>
    public string GetSummary()
    {
        if (IsValid)
        {
            return "Validation passed.";
        }

        var parts = new List<string>();

        if (_schemaErrors.Count > 0)
        {
            parts.Add($"{_schemaErrors.Count} schema error(s)");
        }

        var lintErrors = _lintViolations.Count(v => v.Severity == LintSeverity.Error);
        if (lintErrors > 0)
        {
            parts.Add($"{lintErrors} lint error(s)");
        }

        if (_itemTypeErrors.Count > 0)
        {
            parts.Add($"{_itemTypeErrors.Count} item-type error(s)");
        }

        return string.Join(", ", parts);
    }
}

/// <summary>
/// Schema error from the validation pipeline.
/// </summary>
public sealed record PipelineSchemaError
{
    public required string Path { get; init; }
    public required string Message { get; init; }
    public string? SchemaKeyword { get; init; }
}

/// <summary>
/// Result of validating a batch of questions.
/// </summary>
public sealed class BatchValidationResult
{
    public IReadOnlyList<QuestionPipelineResult> Results { get; }
    public int TotalQuestions => Results.Count;
    public int ValidQuestions => Results.Count(r => r.IsValid);
    public int InvalidQuestions => Results.Count(r => !r.IsValid);
    public bool AllValid => Results.All(r => r.IsValid);
    public double ValidationRate => TotalQuestions > 0 ? (double)ValidQuestions / TotalQuestions : 0;

    public BatchValidationResult(IReadOnlyList<QuestionPipelineResult> results)
    {
        Results = results;
    }

    /// <summary>
    /// Gets only the results that failed validation.
    /// </summary>
    public IEnumerable<QuestionPipelineResult> GetFailedResults() =>
        Results.Where(r => !r.IsValid);

    /// <summary>
    /// Gets a summary of the batch validation.
    /// </summary>
    public string GetSummary() =>
        $"Validated {TotalQuestions} questions: {ValidQuestions} passed, {InvalidQuestions} failed ({ValidationRate:P0})";
}
