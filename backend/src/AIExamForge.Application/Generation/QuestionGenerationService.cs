using System.Text.Json;
using System.Text.Json.Serialization;
using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Application.Generation.Interfaces;
using AIExamForge.Application.Mcp;
using AIExamForge.Application.Validation.Interfaces;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Interfaces;
using AIExamForge.Domain.Models;
using AIExamForge.Domain.Repositories;
using AIExamForge.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Application.Generation;

/// <summary>
/// Orchestrates the complete question generation pipeline:
/// 1. MCP content retrieval
/// 2. Prompt assembly
/// 3. LLM generation
/// 4. Schema + lint validation
/// 5. Question persistence
/// 
/// Per Constitution Principle VI: MCP is the exclusive content source.
/// </summary>
public sealed class QuestionGenerationService : Interfaces.IQuestionGenerationService
{
    private readonly McpRetrievalService _mcpService;
    private readonly IPromptAssembler _promptAssembler;
    private readonly ILlmClient _llmClient;
    private readonly ISchemaValidator _schemaValidator;
    private readonly ILintValidator _lintValidator;
    private readonly IItemTypeRegistry _itemTypeRegistry;
    private readonly IOutlineRepository _outlineRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<QuestionGenerationService> _logger;

    private static readonly JsonSerializerOptions SchemaValidationJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const int MaxRetryAttempts = 3;

    public QuestionGenerationService(
        McpRetrievalService mcpService,
        IPromptAssembler promptAssembler,
        ILlmClient llmClient,
        ISchemaValidator schemaValidator,
        ILintValidator lintValidator,
        IItemTypeRegistry itemTypeRegistry,
        IOutlineRepository outlineRepository,
        IUnitOfWork unitOfWork,
        ILogger<QuestionGenerationService> logger)
    {
        _mcpService = mcpService;
        _promptAssembler = promptAssembler;
        _llmClient = llmClient;
        _schemaValidator = schemaValidator;
        _lintValidator = lintValidator;
        _itemTypeRegistry = itemTypeRegistry;
        _outlineRepository = outlineRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Generates questions for the given request.
    /// </summary>
    public async Task<GenerationResult> GenerateAsync(
        GenerationRequestDto requestDto,
        string userId,
        Guid? generationId = null,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("[{RequestId}] Starting generation: {Count} questions, {Types} types",
            requestId, requestDto.QuestionCount, requestDto.QuestionTypes.Count);

        // Create domain entity
        var itemTypes = requestDto.QuestionTypes.Select(ItemTypeCode.Create).ToList();
        var difficultyDistribution = requestDto.Difficulty is not null
            ? Domain.ValueObjects.DifficultyDistribution.Create(
                requestDto.Difficulty.EasyPercent,
                requestDto.Difficulty.MediumPercent,
                requestDto.Difficulty.HardPercent)
            : Domain.ValueObjects.DifficultyDistribution.Default;

        var generationRequest = generationId is Guid id
            ? GenerationRequest.CreateWithId(
                id,
                userId,
                requestDto.OutlineId,
                requestDto.SelectedNodeIds,
                requestDto.QuestionCount,
                itemTypes,
                difficultyDistribution,
                toneProfile: requestDto.ToneProfile?.ToString())
            : GenerationRequest.Create(
                userId,
                requestDto.OutlineId,
                requestDto.SelectedNodeIds,
                requestDto.QuestionCount,
                itemTypes,
                difficultyDistribution,
                toneProfile: requestDto.ToneProfile?.ToString());

        await _unitOfWork.GenerationRequests.AddAsync(generationRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            // Step 1: Retrieve MCP content
            generationRequest.StartContentRetrieval();
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var outlineNodes = await GetOutlineNodesAsync(requestDto.OutlineId, requestDto.SelectedNodeIds, cancellationToken);
            var mcpContent = await _mcpService.RetrieveForNodesAsync(outlineNodes, generationRequest.Id, cancellationToken);

            if (!mcpContent.HasContent)
            {
                generationRequest.Fail("No content retrieved from MCP for the selected outline nodes.");
                await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return GenerationResult.Failed(generationRequest.Id, "No content available for generation.");
            }

            _logger.LogInformation("[{RequestId}] Retrieved MCP content: {Count} items, {Sources} sources",
                requestId, mcpContent.Items.Count, mcpContent.AllSourceUrls.Count);

            // Step 2: Assemble prompt
            generationRequest.StartGeneration();
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var prompt = await _promptAssembler.AssembleAsync(requestDto, outlineNodes, mcpContent, cancellationToken);

            // Step 3: Generate with LLM (with retry logic)
            var llmResponse = await GenerateWithRetryAsync(prompt, generationRequest, cancellationToken);

            if (!llmResponse.Success || string.IsNullOrEmpty(llmResponse.Content))
            {
                generationRequest.Fail(llmResponse.ErrorMessage ?? "LLM generation failed without error message.");
                await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return GenerationResult.Failed(generationRequest.Id, llmResponse.ErrorMessage ?? "Generation failed.");
            }

            // Step 4: Parse and validate questions
            var parsedQuestions = ParseLlmResponse(llmResponse.Content);
            if (parsedQuestions.Count == 0)
            {
                generationRequest.Fail("LLM response contained no valid questions.");
                await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return GenerationResult.Failed(generationRequest.Id, "No questions could be parsed from LLM response.");
            }

            generationRequest.StartValidation(parsedQuestions.Count);
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Step 5: Validate and create question set
            var questionSet = QuestionSet.Create(
                userId,
                generationRequest.Id,
                renderMode: MapRenderMode(requestDto.RenderMode));

            var validationResults = await ValidateAndAddQuestionsAsync(
                parsedQuestions,
                questionSet,
                mcpContent.AllSourceUrls.ToList(),
                cancellationToken);

            if (questionSet.TotalQuestions == 0)
            {
                generationRequest.Fail("All generated questions failed validation.");
                await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return GenerationResult.Failed(generationRequest.Id, "All questions failed validation.", validationResults);
            }

            // Step 6: Persist question set
            await _unitOfWork.QuestionSets.AddAsync(questionSet, cancellationToken);
            generationRequest.Complete(questionSet.Id, questionSet.TotalQuestions);
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[{RequestId}] Generation complete: {Valid}/{Total} questions validated",
                requestId, questionSet.TotalQuestions, parsedQuestions.Count);

            return GenerationResult.Succeeded(
                generationRequest.Id,
                questionSet.Id,
                questionSet.TotalQuestions,
                parsedQuestions.Count - questionSet.TotalQuestions,
                mcpContent.AllSourceUrls.ToList());
        }
        catch (OperationCanceledException)
        {
            generationRequest.Cancel();
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] Generation failed with exception", requestId);
            generationRequest.Fail($"Unexpected error: {ex.Message}");
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, CancellationToken.None);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            return GenerationResult.Failed(generationRequest.Id, $"An unexpected error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates questions with streaming progress updates.
    /// </summary>
    public async Task<GenerationResult> GenerateStreamingAsync(
        GenerationRequestDto requestDto,
        string userId,
        Guid? generationId,
        Func<GenerationProgress, Task> onProgress,
        CancellationToken cancellationToken = default)
    {
        await onProgress(GenerationProgress.Started("Initializing generation..."));

        // Create or reuse domain entity
        var itemTypes = requestDto.QuestionTypes.Select(ItemTypeCode.Create).ToList();
        var difficultyDistribution = requestDto.Difficulty is not null
            ? Domain.ValueObjects.DifficultyDistribution.Create(
                requestDto.Difficulty.EasyPercent,
                requestDto.Difficulty.MediumPercent,
                requestDto.Difficulty.HardPercent)
            : Domain.ValueObjects.DifficultyDistribution.Default;

        GenerationRequest generationRequest;
        if (generationId is Guid id)
        {
            var existing = await _unitOfWork.GenerationRequests.GetByIdAsync(id, cancellationToken);
            if (existing is not null)
            {
                if (existing.UserId != userId)
                {
                    throw new InvalidOperationException("GenerationRequest user mismatch.");
                }

                if (existing.OutlineId != requestDto.OutlineId)
                {
                    throw new InvalidOperationException("GenerationRequest outline mismatch.");
                }

                generationRequest = existing;
            }
            else
            {
                generationRequest = GenerationRequest.CreateWithId(
                    id,
                    userId,
                    requestDto.OutlineId,
                    requestDto.SelectedNodeIds,
                    requestDto.QuestionCount,
                    itemTypes,
                    difficultyDistribution,
                    toneProfile: requestDto.ToneProfile?.ToString());

                await _unitOfWork.GenerationRequests.AddAsync(generationRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            generationRequest = GenerationRequest.Create(
                userId,
                requestDto.OutlineId,
                requestDto.SelectedNodeIds,
                requestDto.QuestionCount,
                itemTypes,
                difficultyDistribution,
                toneProfile: requestDto.ToneProfile?.ToString());

            await _unitOfWork.GenerationRequests.AddAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        try
        {
            // Step 1: MCP retrieval
            await onProgress(GenerationProgress.RetrievingContent("Retrieving content from Microsoft Learn..."));
            generationRequest.StartContentRetrieval();
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var outlineNodes = await GetOutlineNodesAsync(requestDto.OutlineId, requestDto.SelectedNodeIds, cancellationToken);
            var mcpContent = await _mcpService.RetrieveForNodesAsync(outlineNodes, generationRequest.Id, cancellationToken);

            if (!mcpContent.HasContent)
            {
                await onProgress(GenerationProgress.Failed("No content found for selected topics."));
                generationRequest.Fail("No content retrieved from MCP.");
                await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return GenerationResult.Failed(generationRequest.Id, "No content available.");
            }

            await onProgress(GenerationProgress.ContentRetrieved(mcpContent.Items.Count, mcpContent.AllSourceUrls.Count));

            // Step 2: Prompt assembly
            await onProgress(GenerationProgress.AssemblingPrompt("Preparing generation prompt..."));
            var prompt = await _promptAssembler.AssembleAsync(requestDto, outlineNodes, mcpContent, cancellationToken);

            // Step 3: LLM generation with streaming
            await onProgress(GenerationProgress.Generating("Generating questions...", 0));
            generationRequest.StartGeneration();
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var llmResponse = await _llmClient.GenerateStreamingAsync(
                prompt,
                async chunk =>
                {
                    // Estimate progress based on chunk content
                    // This is a rough estimate as we stream
                    await onProgress(GenerationProgress.Generating("Generating...", 50));
                },
                cancellationToken);

            if (!llmResponse.Success || string.IsNullOrEmpty(llmResponse.Content))
            {
                await onProgress(GenerationProgress.Failed("Generation failed."));
                generationRequest.Fail(llmResponse.ErrorMessage ?? "LLM generation failed.");
                await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return GenerationResult.Failed(generationRequest.Id, llmResponse.ErrorMessage ?? "Generation failed.");
            }

            // Step 4: Parse and validate
            await onProgress(GenerationProgress.Validating("Validating generated questions..."));
            var parsedQuestions = ParseLlmResponse(llmResponse.Content);
            generationRequest.StartValidation(parsedQuestions.Count);
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var questionSet = QuestionSet.Create(
                userId,
                generationRequest.Id,
                renderMode: MapRenderMode(requestDto.RenderMode));

            var validationResults = await ValidateAndAddQuestionsAsync(
                parsedQuestions,
                questionSet,
                mcpContent.AllSourceUrls.ToList(),
                cancellationToken);

            if (questionSet.TotalQuestions == 0)
            {
                await onProgress(GenerationProgress.Failed("All questions failed validation."));
                generationRequest.Fail("All questions failed validation.");
                await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return GenerationResult.Failed(generationRequest.Id, "All questions failed validation.", validationResults);
            }

            // Step 5: Persist
            await _unitOfWork.QuestionSets.AddAsync(questionSet, cancellationToken);
            generationRequest.Complete(questionSet.Id, questionSet.TotalQuestions);
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await onProgress(GenerationProgress.Completed(questionSet.TotalQuestions, parsedQuestions.Count - questionSet.TotalQuestions));

            return GenerationResult.Succeeded(
                generationRequest.Id,
                questionSet.Id,
                questionSet.TotalQuestions,
                parsedQuestions.Count - questionSet.TotalQuestions,
                mcpContent.AllSourceUrls.ToList());
        }
        catch (OperationCanceledException)
        {
            await onProgress(GenerationProgress.Cancelled());
            generationRequest.Cancel();
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, CancellationToken.None);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await onProgress(GenerationProgress.Failed($"Error: {ex.Message}"));
            generationRequest.Fail(ex.Message);
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, CancellationToken.None);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            return GenerationResult.Failed(generationRequest.Id, ex.Message);
        }
    }

    private async Task<LlmResponse> GenerateWithRetryAsync(
        PromptPayload prompt,
        GenerationRequest generationRequest,
        CancellationToken cancellationToken)
    {
        LlmResponse? lastResponse = null;

        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            _logger.LogDebug("LLM generation attempt {Attempt}/{Max}", attempt, MaxRetryAttempts);

            lastResponse = await _llmClient.GenerateAsync(prompt, cancellationToken);

            if (lastResponse.Success && !string.IsNullOrEmpty(lastResponse.Content))
            {
                return lastResponse;
            }

            generationRequest.IncrementRetryCount();
            await _unitOfWork.GenerationRequests.UpdateAsync(generationRequest, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (attempt < MaxRetryAttempts)
            {
                // Exponential backoff
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                await Task.Delay(delay, cancellationToken);
            }
        }

        return lastResponse ?? LlmResponse.Failed("Generation failed after all retry attempts.", "retry-exhausted");
    }

    private async Task<IReadOnlyList<OutlineNode>> GetOutlineNodesAsync(
        Guid outlineId,
        IReadOnlyList<Guid> nodeIds,
        CancellationToken cancellationToken)
    {
        var outline = await _outlineRepository.GetByIdAsync(outlineId, cancellationToken);
        if (outline is null)
        {
            throw new InvalidOperationException($"Outline {outlineId} not found.");
        }

        var nodeIdSet = nodeIds.ToHashSet();
        var selectedNodes = outline.GetAllNodes()
            .Where(n => nodeIdSet.Contains(n.Id))
            .ToList();

        if (selectedNodes.Count != nodeIds.Count)
        {
            var foundIds = selectedNodes.Select(n => n.Id).ToHashSet();
            var missingIds = nodeIds.Where(id => !foundIds.Contains(id)).ToList();
            _logger.LogWarning("Some selected nodes not found: {MissingIds}", string.Join(", ", missingIds));
        }

        return selectedNodes;
    }

    private List<ParsedQuestion> ParseLlmResponse(string jsonContent)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Expected format: { "questions": [...] }
            var wrapper = JsonSerializer.Deserialize<QuestionWrapper>(jsonContent, options);
            if (wrapper?.Questions is null || wrapper.Questions.Count == 0)
            {
                _logger.LogWarning("LLM response contained no questions array");
                return [];
            }

            return wrapper.Questions;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response as JSON");
            return [];
        }
    }

    private async Task<List<QuestionValidationResult>> ValidateAndAddQuestionsAsync(
        List<ParsedQuestion> parsedQuestions,
        QuestionSet questionSet,
        List<string> sourceUrls,
        CancellationToken cancellationToken)
    {
        var results = new List<QuestionValidationResult>();
        var orderIndex = 0;

        foreach (var parsed in parsedQuestions)
        {
            var validationResult = await ValidateQuestionAsync(parsed, cancellationToken);
            results.Add(validationResult);

            if (validationResult.IsValid)
            {
                try
                {
                    var itemType = ItemTypeCode.Create(parsed.ItemTypeCode ?? "M1");

                    // Create tags with proper required fields
                    var tags = QuestionTags.Create(
                        exam: parsed.Tags?.Exam ?? "General",
                        objective: parsed.Tags?.Objective ?? "General Knowledge",
                        section: parsed.Tags?.Section ?? "Main",
                        topics: parsed.Tags?.Topics);

                    // Create scoring based on item type
                    var scoring = itemType.IsDichotomous
                        ? QuestionScoring.Dichotomous(1)
                        : QuestionScoring.Polytomous(parsed.Scoring?.MaxPoints ?? 1);

                    var difficulty = Enum.TryParse<QuestionDifficulty>(parsed.Difficulty, true, out var d)
                        ? d
                        : QuestionDifficulty.Medium;

                    var question = Question.Create(
                        itemType,
                        parsed.Title ?? $"Question {orderIndex + 1}",
                        parsed.Stem ?? "",
                        difficulty,
                        tags,
                        scoring,
                        NormalizeContentJson(parsed.Content),
                        questionSet.Id,
                        orderIndex++,
                        parsed.Instruction,
                        parsed.Rationale);

                    questionSet.AddQuestion(question);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create Question entity from parsed data");
                    validationResult.AddError($"Entity creation failed: {ex.Message}");
                }
            }
        }

        return results;
    }

    private async Task<QuestionValidationResult> ValidateQuestionAsync(
        ParsedQuestion parsed,
        CancellationToken cancellationToken)
    {
        parsed.Id ??= Guid.NewGuid().ToString("N");
        var result = new QuestionValidationResult(parsed.Id);

        var itemTypeCode = parsed.ItemTypeCode ?? "M1";
        ItemTypeCode itemType;
        try
        {
            itemType = ItemTypeCode.Create(itemTypeCode);
        }
        catch
        {
            itemTypeCode = "M1";
            itemType = ItemTypeCode.Create(itemTypeCode);
        }

        var difficulty = NormalizeDifficultyForSchema(parsed.Difficulty);

        var normalizedContent = NormalizeContentElement(parsed.Content);

        var questionForSchema = new
        {
            id = parsed.Id,
            itemTypeCode,
            title = parsed.Title,
            stem = parsed.Stem,
            instruction = parsed.Instruction,
            difficulty,
            tags = new
            {
                exam = parsed.Tags?.Exam,
                objective = parsed.Tags?.Objective,
                section = parsed.Tags?.Section,
                topics = parsed.Tags?.Topics.Count > 0 ? parsed.Tags.Topics : null
            },
            scoring = new
            {
                model = itemType.IsDichotomous ? "dichotomous" : "polytomous",
                maxPoints = parsed.Scoring?.MaxPoints ?? 1
            },
            content = normalizedContent,
            rationale = parsed.Rationale
        };

        // Schema validation
        var questionJson = JsonSerializer.Serialize(questionForSchema, SchemaValidationJsonOptions);
        var schemaResult = await _schemaValidator.ValidateQuestionAsync(
            questionJson,
            itemTypeCode,
            cancellationToken);

        if (!schemaResult.IsValid)
        {
            foreach (var error in schemaResult.SchemaErrors)
            {
                result.AddSchemaError($"{error.Path}: {error.Message}");
            }
        }

        // Lint validation - extract choices from content if available
        var choices = ExtractChoicesFromContent(normalizedContent);

        var lintViolations = _lintValidator.ValidateQuestion(
            parsed.Stem ?? "",
            parsed.Instruction,
            choices,
            parsed.Rationale);

        foreach (var violation in lintViolations)
        {
            result.AddLintViolation(violation);
        }

        return result;
    }

    private static string NormalizeDifficultyForSchema(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return "medium";
        }

        var trimmed = difficulty.Trim();
        if (string.Equals(trimmed, "easy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "medium", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "hard", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.ToLowerInvariant();
        }

        return Enum.TryParse<QuestionDifficulty>(trimmed, true, out var parsed)
            ? parsed.ToString().ToLowerInvariant()
            : "medium";
    }

    private static JsonElement? NormalizeContentElement(JsonElement? content)
    {
        if (content is null)
        {
            return null;
        }

        try
        {
            if (content.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                return content.Value.Clone();
            }

            if (content.Value.ValueKind == JsonValueKind.String)
            {
                var s = content.Value.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    using var doc = JsonDocument.Parse(s);
                    return doc.RootElement.Clone();
                }
            }

            return content.Value.Clone();
        }
        catch
        {
            return content.Value.Clone();
        }
    }

    private static string NormalizeContentJson(JsonElement? content)
    {
        var normalized = NormalizeContentElement(content);
        return normalized is null ? "null" : normalized.Value.GetRawText();
    }

    private static List<string> ExtractChoicesFromContent(JsonElement? content)
    {
        content = NormalizeContentElement(content);
        if (content is null || content.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        try
        {
            if (content.Value.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                return choices.EnumerateArray()
                    .Select(c => c.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "")
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();
            }
        }
        catch
        {
            // Ignore parsing errors, return empty list
        }

        return [];
    }

    private static Domain.ValueObjects.RenderMode MapRenderMode(DTOs.RenderMode dto)
    {
        return dto switch
        {
            DTOs.RenderMode.Exam => Domain.ValueObjects.RenderMode.Exam,
            DTOs.RenderMode.Study => Domain.ValueObjects.RenderMode.Study,
            _ => Domain.ValueObjects.RenderMode.Study
        };
    }

    #region Internal DTOs for JSON parsing

    private sealed class QuestionWrapper
    {
        public List<ParsedQuestion> Questions { get; set; } = [];
    }

    private sealed class ParsedQuestion
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("itemTypeCode")]
        public string? ItemTypeCode { get; set; }

        // Back-compat: allow older prompts to send "itemType".
        [JsonPropertyName("itemType")]
        public string? ItemType { set => ItemTypeCode = value; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("stem")]
        public string? Stem { get; set; }

        [JsonPropertyName("instruction")]
        public string? Instruction { get; set; }

        [JsonPropertyName("difficulty")]
        public string? Difficulty { get; set; }

        [JsonPropertyName("tags")]
        public ParsedTags? Tags { get; set; }

        [JsonPropertyName("scoring")]
        public ParsedScoring? Scoring { get; set; }

        [JsonPropertyName("content")]
        public JsonElement? Content { get; set; }

        [JsonPropertyName("rationale")]
        public string? Rationale { get; set; }
    }

    private sealed class ParsedTags
    {
        [JsonPropertyName("topics")]
        public List<string> Topics { get; set; } = [];

        [JsonPropertyName("exam")]
        public string? Exam { get; set; }

        [JsonPropertyName("objective")]
        public string? Objective { get; set; }

        [JsonPropertyName("section")]
        public string? Section { get; set; }
    }

    private sealed class ParsedScoring
    {
        [JsonPropertyName("maxPoints")]
        public int MaxPoints { get; set; } = 1;

        // Optional; schema validation derives this from item type if absent.
        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    #endregion

    #region Regeneration Methods (T123-T124)

    /// <summary>
    /// Regenerates all questions for an existing generation request.
    /// T123: Regenerate entire question set.
    /// </summary>
    public async Task<GenerationResult> RegenerateAllAsync(
        Guid generationRequestId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting regeneration for request {GenerationRequestId}", generationRequestId);

        // Get existing generation request
        var existingRequest = await _unitOfWork.GenerationRequests.GetByIdAsync(generationRequestId, cancellationToken);
        if (existingRequest == null)
        {
            return GenerationResult.Failed(generationRequestId, "Generation request not found.");
        }

        if (existingRequest.UserId != userId)
        {
            return GenerationResult.Failed(generationRequestId, "Not authorized to regenerate this request.");
        }

        // Create a new GenerationRequestDto from the existing request
        var requestDto = new GenerationRequestDto
        {
            OutlineId = existingRequest.OutlineId,
            SelectedNodeIds = existingRequest.SelectedNodeIds.ToList(),
            QuestionCount = existingRequest.QuestionCount,
            QuestionTypes = existingRequest.ItemTypes.Select(it => it.Code).ToList(),
            Difficulty = new DifficultyDistributionDto
            {
                EasyPercent = existingRequest.DifficultyDistribution.EasyPercent,
                MediumPercent = existingRequest.DifficultyDistribution.MediumPercent,
                HardPercent = existingRequest.DifficultyDistribution.HardPercent
            },
            ToneProfile = Enum.TryParse<ToneProfile>(existingRequest.ToneProfile, out var tone)
                ? tone
                : ToneProfile.Professional
        };

        // Generate fresh questions
        return await GenerateAsync(requestDto, userId, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Regenerates a single question within an existing question set.
    /// T124: Single question regeneration.
    /// </summary>
    public async Task<QuestionRegenerationResult> RegenerateQuestionAsync(
        Guid questionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting single question regeneration for {QuestionId}", questionId);

        // Get the question set containing this question
        var questionSets = await _unitOfWork.QuestionSets.GetByUserIdAsync(userId, cancellationToken);
        QuestionSet? targetSet = null;
        Question? targetQuestion = null;

        foreach (var set in questionSets)
        {
            var setWithQuestions = await _unitOfWork.QuestionSets.GetByIdWithQuestionsAsync(set.Id, cancellationToken);
            if (setWithQuestions == null)
            {
                continue;
            }

            targetQuestion = setWithQuestions.Questions.FirstOrDefault(q => q.Id == questionId);
            if (targetQuestion != null)
            {
                targetSet = setWithQuestions;
                break;
            }
        }

        if (targetSet == null || targetQuestion == null)
        {
            return QuestionRegenerationResult.Failed(questionId, "Question not found.");
        }

        // Get the generation request to retrieve context
        var genRequestId = targetSet.GenerationRequestId;
        var genRequest = await _unitOfWork.GenerationRequests.GetByIdAsync(genRequestId, cancellationToken);
        if (genRequest == null)
        {
            return QuestionRegenerationResult.Failed(questionId, "Original generation request not found.");
        }

        // Get outline for node information
        var outline = await _outlineRepository.GetByIdAsync(genRequest.OutlineId, cancellationToken);
        if (outline == null)
        {
            return QuestionRegenerationResult.Failed(questionId, "Outline not found.");
        }

        // Retrieve MCP content for the question's topics
        var topics = targetQuestion.Tags.Topics;
        if (topics.Count == 0)
        {
            // Fallback: use section from outline
            var allNodes = outline.GetAllNodes().ToList();
            var selectedNode = allNodes.FirstOrDefault(n => genRequest.SelectedNodeIds.Contains(n.Id));
            topics = [selectedNode?.Title ?? "General topic"];
        }

        // Retrieve MCP content for each topic
        var mcpContents = new List<McpContent>();
        foreach (var topic in topics)
        {
            var content = await _mcpService.RetrieveForTopicAsync(topic, cancellationToken);
            if (content != null)
            {
                mcpContents.Add(content);
            }
        }
        var mcpContent = new McpContentCollection { Items = mcpContents.AsReadOnly() };
        if (!mcpContent.HasContent)
        {
            return QuestionRegenerationResult.Failed(questionId, "Unable to retrieve MCP content for regeneration.");
        }

        // Build request for single question
        var singleRequestDto = new GenerationRequestDto
        {
            OutlineId = genRequest.OutlineId,
            SelectedNodeIds = genRequest.SelectedNodeIds.ToList(),
            QuestionCount = 1,
            QuestionTypes = [targetQuestion.ItemType.Code],
            Difficulty = new DifficultyDistributionDto
            {
                EasyPercent = targetQuestion.Difficulty == QuestionDifficulty.Easy ? 100 : 0,
                MediumPercent = targetQuestion.Difficulty == QuestionDifficulty.Medium ? 100 : 0,
                HardPercent = targetQuestion.Difficulty == QuestionDifficulty.Hard ? 100 : 0
            }
        };

        // Assemble prompt for single question
        // Get the selected nodes from the outline
        var outlineNodes = outline.GetAllNodes()
            .Where(n => genRequest.SelectedNodeIds.Contains(n.Id))
            .ToList();

        var prompt = await _promptAssembler.AssembleAsync(
            singleRequestDto,
            outlineNodes,
            mcpContent,
            cancellationToken);

        // Generate via LLM
        var llmResponse = await _llmClient.GenerateAsync(prompt, cancellationToken);
        if (!llmResponse.Success || string.IsNullOrWhiteSpace(llmResponse.Content))
        {
            return QuestionRegenerationResult.Failed(questionId, 
                llmResponse.ErrorMessage ?? "LLM returned empty response.");
        }

        // Parse and validate response
        var parsedQuestions = ParseLlmResponse(llmResponse.Content);
        if (parsedQuestions.Count == 0)
        {
            return QuestionRegenerationResult.Failed(questionId, "Failed to parse regenerated question.");
        }

        var parsedQuestion = parsedQuestions[0];

        // Create new question entity
        var itemType = ItemTypeCode.Create(parsedQuestion.ItemTypeCode ?? targetQuestion.ItemType.Code);
        var tags = QuestionTags.Create(
            exam: parsedQuestion.Tags?.Exam ?? targetQuestion.Tags.Exam,
            objective: parsedQuestion.Tags?.Objective ?? targetQuestion.Tags.Objective,
            section: parsedQuestion.Tags?.Section ?? targetQuestion.Tags.Section,
            topics: parsedQuestion.Tags?.Topics ?? targetQuestion.Tags.Topics.ToList());

        var scoring = itemType.IsDichotomous
            ? QuestionScoring.Dichotomous(1)
            : QuestionScoring.Polytomous(parsedQuestion.Scoring?.MaxPoints ?? 1);

        var difficulty = Enum.TryParse<QuestionDifficulty>(parsedQuestion.Difficulty, true, out var diff)
            ? diff
            : targetQuestion.Difficulty;

        var contentJson = parsedQuestion.Content.HasValue
            ? NormalizeContentJson(parsedQuestion.Content)
            : targetQuestion.ContentJson;

        var newQuestion = Question.Create(
            itemType,
            parsedQuestion.Title ?? targetQuestion.Title,
            parsedQuestion.Stem ?? targetQuestion.Stem,
            difficulty,
            tags,
            scoring,
            contentJson,
            targetSet.Id,
            targetQuestion.OrderIndex,
            parsedQuestion.Instruction,
            parsedQuestion.Rationale);

        // Validate
        var schemaResult = await _schemaValidator.ValidateQuestionAsync(contentJson, itemType.Code, cancellationToken);
        if (!schemaResult.IsValid)
        {
            return QuestionRegenerationResult.Failed(questionId, "Regenerated question failed schema validation.");
        }

        var choiceTexts = ExtractChoicesFromContent(parsedQuestion.Content);
        var lintViolations = _lintValidator.ValidateQuestion(
            parsedQuestion.Stem ?? targetQuestion.Stem,
            parsedQuestion.Instruction,
            choiceTexts,
            parsedQuestion.Rationale);

        if (lintViolations.Any(v => v.Severity == LintSeverity.Error))
        {
            return QuestionRegenerationResult.Failed(questionId, "Regenerated question failed lint validation.");
        }

        // Replace question in set
        targetSet.ReplaceQuestion(targetQuestion.Id, newQuestion);
        await _unitOfWork.QuestionSets.UpdateAsync(targetSet, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully regenerated question {OldId} -> {NewId}", questionId, newQuestion.Id);

        return QuestionRegenerationResult.Succeeded(questionId, newQuestion.Id);
    }

    #endregion
}

/// <summary>
/// Result of a question generation operation.
/// </summary>
public sealed record GenerationResult
{
    public Guid GenerationRequestId { get; init; }
    public Guid? QuestionSetId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int QuestionsGenerated { get; init; }
    public int QuestionsFailed { get; init; }
    public List<string> SourceUrls { get; init; } = [];
    public List<QuestionValidationResult>? ValidationResults { get; init; }

    public static GenerationResult Succeeded(
        Guid requestId,
        Guid questionSetId,
        int generated,
        int failed,
        List<string> sourceUrls) => new()
    {
        GenerationRequestId = requestId,
        QuestionSetId = questionSetId,
        Success = true,
        QuestionsGenerated = generated,
        QuestionsFailed = failed,
        SourceUrls = sourceUrls
    };

    public static GenerationResult Failed(
        Guid requestId,
        string error,
        List<QuestionValidationResult>? validationResults = null) => new()
    {
        GenerationRequestId = requestId,
        Success = false,
        ErrorMessage = error,
        ValidationResults = validationResults
    };
}

/// <summary>
/// Result of a single question regeneration operation.
/// </summary>
public sealed record QuestionRegenerationResult
{
    public Guid OriginalQuestionId { get; init; }
    public Guid? NewQuestionId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static QuestionRegenerationResult Succeeded(Guid originalId, Guid newId) => new()
    {
        OriginalQuestionId = originalId,
        NewQuestionId = newId,
        Success = true
    };

    public static QuestionRegenerationResult Failed(Guid originalId, string error) => new()
    {
        OriginalQuestionId = originalId,
        Success = false,
        ErrorMessage = error
    };
}

/// <summary>
/// Validation result for a single question.
/// </summary>
public sealed class QuestionValidationResult
{
    public string QuestionId { get; }
    public bool IsValid => _schemaErrors.Count == 0 && _lintViolations.Count == 0;
    public IReadOnlyList<string> SchemaErrors => _schemaErrors.AsReadOnly();
    public IReadOnlyList<LintViolation> LintViolations => _lintViolations.AsReadOnly();

    private readonly List<string> _schemaErrors = [];
    private readonly List<LintViolation> _lintViolations = [];

    public QuestionValidationResult(string questionId)
    {
        QuestionId = questionId;
    }

    public void AddSchemaError(string error) => _schemaErrors.Add(error);
    public void AddLintViolation(LintViolation violation) => _lintViolations.Add(violation);
    public void AddError(string error) => _schemaErrors.Add(error);
}

/// <summary>
/// Progress update during question generation.
/// </summary>
public sealed record GenerationProgress
{
    public GenerationProgressStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? PercentComplete { get; init; }
    public int? ContentChunksRetrieved { get; init; }
    public int? SourceCount { get; init; }
    public int? QuestionsGenerated { get; init; }
    public int? QuestionsFailed { get; init; }

    public static GenerationProgress Started(string message) => new()
    {
        Status = GenerationProgressStatus.Started,
        Message = message,
        PercentComplete = 0
    };

    public static GenerationProgress RetrievingContent(string message) => new()
    {
        Status = GenerationProgressStatus.RetrievingContent,
        Message = message,
        PercentComplete = 10
    };

    public static GenerationProgress ContentRetrieved(int chunks, int sources) => new()
    {
        Status = GenerationProgressStatus.ContentRetrieved,
        Message = $"Retrieved {chunks} content chunks from {sources} sources.",
        PercentComplete = 25,
        ContentChunksRetrieved = chunks,
        SourceCount = sources
    };

    public static GenerationProgress AssemblingPrompt(string message) => new()
    {
        Status = GenerationProgressStatus.AssemblingPrompt,
        Message = message,
        PercentComplete = 30
    };

    public static GenerationProgress Generating(string message, int percent) => new()
    {
        Status = GenerationProgressStatus.Generating,
        Message = message,
        PercentComplete = 30 + (percent / 2) // 30-80%
    };

    public static GenerationProgress Validating(string message) => new()
    {
        Status = GenerationProgressStatus.Validating,
        Message = message,
        PercentComplete = 85
    };

    public static GenerationProgress Completed(int generated, int failed) => new()
    {
        Status = GenerationProgressStatus.Completed,
        Message = $"Generation complete: {generated} questions created.",
        PercentComplete = 100,
        QuestionsGenerated = generated,
        QuestionsFailed = failed
    };

    public static GenerationProgress Failed(string message) => new()
    {
        Status = GenerationProgressStatus.Failed,
        Message = message
    };

    public static GenerationProgress Cancelled() => new()
    {
        Status = GenerationProgressStatus.Cancelled,
        Message = "Generation cancelled by user."
    };
}

/// <summary>
/// Status values for generation progress.
/// </summary>
public enum GenerationProgressStatus
{
    Started,
    RetrievingContent,
    ContentRetrieved,
    AssemblingPrompt,
    Generating,
    Validating,
    Completed,
    Failed,
    Cancelled
}
