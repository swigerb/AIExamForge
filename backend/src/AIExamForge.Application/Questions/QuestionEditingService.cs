using System.Text.Json;
using AIExamForge.Application.Questions.DTOs;
using AIExamForge.Application.Validation.Interfaces;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Repositories;
using AIExamForge.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Application.Questions;

/// <summary>
/// T128: Service for editing existing questions.
/// Validates edits against schema and lint rules before persisting.
/// </summary>
public sealed class QuestionEditingService
{
    private readonly IQuestionSetRepository _questionSetRepository;
    private readonly ISchemaValidator _schemaValidator;
    private readonly ILintValidator _lintValidator;
    private readonly ILogger<QuestionEditingService> _logger;

    public QuestionEditingService(
        IQuestionSetRepository questionSetRepository,
        ISchemaValidator schemaValidator,
        ILintValidator lintValidator,
        ILogger<QuestionEditingService> logger)
    {
        _questionSetRepository = questionSetRepository;
        _schemaValidator = schemaValidator;
        _lintValidator = lintValidator;
        _logger = logger;
    }

    /// <summary>
    /// Updates a question with the provided changes.
    /// T130: Validates against schema and lint rules before persisting.
    /// </summary>
    public async Task<QuestionEditResult> UpdateQuestionAsync(
        Guid questionId,
        string userId,
        UpdateQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating question {QuestionId} for user {UserId}", questionId, userId);

        // Find the question
        var (questionSet, question) = await FindQuestionAsync(questionId, userId, cancellationToken);
        if (questionSet == null || question == null)
        {
            return QuestionEditResult.NotFound(questionId, "Question not found.");
        }

        // Build updated values (merge with existing)
        var newStem = request.Stem ?? question.Stem;
        var newInstruction = request.Instruction ?? question.Instruction;
        var newRationale = request.Rationale ?? question.Rationale;
        var newContentJson = request.Content.HasValue
            ? JsonSerializer.Serialize(request.Content.Value)
            : question.ContentJson;

        var newDifficulty = question.Difficulty;
        if (!string.IsNullOrWhiteSpace(request.Difficulty) &&
            Enum.TryParse<QuestionDifficulty>(request.Difficulty, true, out var parsedDifficulty))
        {
            newDifficulty = parsedDifficulty;
        }

        var newTags = question.Tags;
        if (request.Tags != null)
        {
            newTags = QuestionTags.Create(
                exam: request.Tags.Exam ?? question.Tags.Exam,
                objective: request.Tags.Objective ?? question.Tags.Objective,
                section: request.Tags.Section ?? question.Tags.Section,
                topics: request.Tags.Topics ?? question.Tags.Topics.ToList());
        }

        // T130: Validate against schema
        var schemaResult = await _schemaValidator.ValidateQuestionAsync(
            newContentJson,
            question.ItemType.Code,
            cancellationToken);

        if (!schemaResult.IsValid)
        {
            _logger.LogWarning("Schema validation failed for question {QuestionId}: {Errors}",
                questionId, string.Join(", ", schemaResult.SchemaErrors.Select(e => e.Message)));

            return QuestionEditResult.ValidationFailed(
                questionId,
                "Schema validation failed",
                schemaResult.SchemaErrors.Select(e => e.Message).ToList());
        }

        // T130: Validate against lint rules
        var choiceTexts = ExtractChoiceTexts(newContentJson);
        var lintViolations = _lintValidator.ValidateQuestion(
            newStem,
            newInstruction,
            choiceTexts,
            newRationale);

        var errors = lintViolations.Where(v => v.Severity == LintSeverity.Error).ToList();
        var warnings = lintViolations.Where(v => v.Severity == LintSeverity.Warning).ToList();

        if (errors.Count > 0)
        {
            _logger.LogWarning("Lint validation failed for question {QuestionId}: {Errors}",
                questionId, string.Join(", ", errors.Select(e => e.Message)));

            return QuestionEditResult.ValidationFailed(
                questionId,
                "Lint validation failed",
                errors.Select(e => e.Message).ToList());
        }

        // Create updated question
        var updatedQuestion = Question.Create(
            question.ItemType,
            question.Title, // Title is not editable
            newStem,
            newDifficulty,
            newTags,
            question.Scoring, // Scoring is not editable
            newContentJson,
            question.QuestionSetId,
            question.OrderIndex,
            newInstruction,
            newRationale);

        // Replace question in set
        questionSet.ReplaceQuestion(questionId, updatedQuestion);
        await _questionSetRepository.UpdateAsync(questionSet, cancellationToken);

        _logger.LogInformation("Successfully updated question {OldId} -> {NewId}",
            questionId, updatedQuestion.Id);

        return QuestionEditResult.Succeeded(
            questionId,
            updatedQuestion.Id,
            warnings.Select(w => w.Message).ToList());
    }

    /// <summary>
    /// Finds a question by ID within a user's question sets.
    /// </summary>
    private async Task<(QuestionSet? Set, Question? Question)> FindQuestionAsync(
        Guid questionId,
        string userId,
        CancellationToken cancellationToken)
    {
        var questionSets = await _questionSetRepository.GetByUserIdAsync(userId, cancellationToken);

        foreach (var set in questionSets)
        {
            var setWithQuestions = await _questionSetRepository.GetByIdWithQuestionsAsync(set.Id, cancellationToken);
            if (setWithQuestions == null)
            {
                continue;
            }

            var question = setWithQuestions.Questions.FirstOrDefault(q => q.Id == questionId);
            if (question != null)
            {
                return (setWithQuestions, question);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Extracts choice texts from question content JSON for lint validation.
    /// </summary>
    private static IEnumerable<string> ExtractChoiceTexts(string contentJson)
    {
        var results = new List<string>();
        
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;

            // Check for choices array
            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                foreach (var choice in choices.EnumerateArray())
                {
                    if (choice.TryGetProperty("text", out var text))
                    {
                        results.Add(text.GetString() ?? string.Empty);
                    }
                }
            }

            // Check for options array (alternative name)
            if (root.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array)
            {
                foreach (var option in options.EnumerateArray())
                {
                    if (option.TryGetProperty("text", out var text))
                    {
                        results.Add(text.GetString() ?? string.Empty);
                    }
                }
            }
        }
        catch
        {
            // If parsing fails, return empty
        }
        
        return results;
    }
}

/// <summary>
/// Result of a question edit operation.
/// </summary>
public sealed record QuestionEditResult
{
    public Guid OriginalQuestionId { get; init; }
    public Guid? NewQuestionId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> ValidationErrors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];

    public static QuestionEditResult Succeeded(Guid originalId, Guid newId, List<string>? warnings = null) => new()
    {
        OriginalQuestionId = originalId,
        NewQuestionId = newId,
        Success = true,
        Warnings = warnings ?? []
    };

    public static QuestionEditResult NotFound(Guid originalId, string message) => new()
    {
        OriginalQuestionId = originalId,
        Success = false,
        ErrorMessage = message
    };

    public static QuestionEditResult ValidationFailed(Guid originalId, string message, List<string> errors) => new()
    {
        OriginalQuestionId = originalId,
        Success = false,
        ErrorMessage = message,
        ValidationErrors = errors
    };
}
