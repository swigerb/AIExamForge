using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Application.Generation.Interfaces;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Interfaces;
using AIExamForge.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIExamForge.Application.Generation;

/// <summary>
/// Assembles prompts for question generation by composing system, exam style, and item type prompts.
/// Per Constitution Principle VI: Uses MCP content exclusively.
/// </summary>
public sealed class PromptAssembler : IPromptAssembler
{
    private readonly IItemTypeRegistry _itemTypeRegistry;
    private readonly ILogger<PromptAssembler> _logger;
    private readonly DiagnosticsLoggingOptions _diagnostics;

    // Cached prompts loaded at startup
    private readonly string _systemPrompt;
    private readonly string _examStylePrompt;

    public PromptAssembler(
        IItemTypeRegistry itemTypeRegistry,
        IOptions<DiagnosticsLoggingOptions> diagnostics,
        ILogger<PromptAssembler> logger)
    {
        _itemTypeRegistry = itemTypeRegistry;
        _logger = logger;
        _diagnostics = diagnostics.Value;

        // Load base prompts from files
        _systemPrompt = LoadPromptFile("system-mcp-only.md");
        _examStylePrompt = LoadPromptFile("exam-style.md");
    }

    /// <inheritdoc />
    public Task<PromptPayload> AssembleAsync(
        GenerationRequestDto request,
        IReadOnlyList<OutlineNode> outlineNodes,
        McpContentCollection mcpContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Assembling prompt for {Count} questions with types: {Types}",
            request.QuestionCount, string.Join(", ", request.QuestionTypes));

        // Build the user prompt
        var userPromptBuilder = new System.Text.StringBuilder();

        // Add exam style guidelines
        userPromptBuilder.AppendLine("# Exam Writing Guidelines");
        userPromptBuilder.AppendLine(_examStylePrompt);
        userPromptBuilder.AppendLine();

        // Add item type instructions for each requested type
        userPromptBuilder.AppendLine("# Question Types to Generate");
        foreach (var itemType in request.QuestionTypes.Distinct())
        {
            var typePrompt = GetItemTypePrompt(itemType);
            userPromptBuilder.AppendLine($"## {itemType}");
            userPromptBuilder.AppendLine(typePrompt);
            userPromptBuilder.AppendLine();
        }

        // Add the MCP content
        userPromptBuilder.AppendLine("# Source Content (from Microsoft Learn)");
        userPromptBuilder.AppendLine("Use ONLY the following content to generate questions:");
        userPromptBuilder.AppendLine();
        userPromptBuilder.AppendLine(mcpContent.GetCombinedContent());
        userPromptBuilder.AppendLine();

        // Add outline context
        userPromptBuilder.AppendLine("# Outline Context");
        foreach (var node in outlineNodes)
        {
            userPromptBuilder.AppendLine($"- Section: {node.Title}");
            if (!string.IsNullOrWhiteSpace(node.Content))
            {
                userPromptBuilder.AppendLine($"  Content: {node.Content}");
            }
        }
        userPromptBuilder.AppendLine();

        // Add generation parameters
        userPromptBuilder.AppendLine("# Generation Parameters");
        userPromptBuilder.AppendLine($"- Number of questions: {request.QuestionCount}");
        userPromptBuilder.AppendLine($"- Question types: {string.Join(", ", request.QuestionTypes)}");
        userPromptBuilder.AppendLine($"- Render mode: {request.RenderMode}");

        if (request.Difficulty != null)
        {
            userPromptBuilder.AppendLine($"- Difficulty distribution: {request.Difficulty.EasyPercent}% easy, {request.Difficulty.MediumPercent}% medium, {request.Difficulty.HardPercent}% hard");
        }

        if (request.ToneProfile.HasValue)
        {
            userPromptBuilder.AppendLine($"- Tone: {request.ToneProfile.Value}");
        }

        // Map DTO difficulty to domain model
        Domain.Models.DifficultyDistribution? difficultyDist = null;
        if (request.Difficulty != null)
        {
            difficultyDist = new Domain.Models.DifficultyDistribution
            {
                EasyPercent = request.Difficulty.EasyPercent,
                MediumPercent = request.Difficulty.MediumPercent,
                HardPercent = request.Difficulty.HardPercent
            };
        }

        var payload = new PromptPayload
        {
            SystemPrompt = _systemPrompt,
            UserPrompt = userPromptBuilder.ToString(),
            QuestionCount = request.QuestionCount,
            ItemTypes = request.QuestionTypes.Distinct().ToList().AsReadOnly(),
            SourceUrls = mcpContent.AllSourceUrls,
            RenderMode = request.RenderMode.ToString(),
            Difficulty = difficultyDist,
            ToneProfile = request.ToneProfile?.ToString()
        };

        _logger.LogDebug("Assembled prompt [{PromptId}]: {Tokens} estimated tokens",
            payload.PromptId, payload.EstimatedTokenCount);

        if (_diagnostics.Enabled && _diagnostics.LogPromptBodies)
        {
            var schemaInstruction = payload.GetJsonSchemaInstruction();

            _logger.LogInformation(
                "DIAGNOSTICS Prompt [{PromptId}] types={Types} count={Count} tokens~{Tokens} systemLen={SystemLen} userLen={UserLen} schemaLen={SchemaLen}",
                payload.PromptId,
                string.Join(",", payload.ItemTypes),
                payload.QuestionCount,
                payload.EstimatedTokenCount,
                payload.SystemPrompt?.Length ?? 0,
                payload.UserPrompt?.Length ?? 0,
                schemaInstruction.Length);

            _logger.LogInformation(
                "DIAGNOSTICS Prompt [{PromptId}] SYSTEM:\n{System}",
                payload.PromptId,
                Truncate(payload.SystemPrompt ?? string.Empty, _diagnostics.MaxChars));

            _logger.LogInformation(
                "DIAGNOSTICS Prompt [{PromptId}] USER:\n{User}",
                payload.PromptId,
                Truncate(payload.UserPrompt ?? string.Empty, _diagnostics.MaxChars));

            _logger.LogInformation(
                "DIAGNOSTICS Prompt [{PromptId}] SCHEMA:\n{Schema}",
                payload.PromptId,
                Truncate(schemaInstruction, _diagnostics.MaxChars));
        }

        return Task.FromResult(payload);
    }

    private static string Truncate(string value, int maxChars)
    {
        if (maxChars <= 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
        {
            return value;
        }

        return value[..maxChars] + "\n[TRUNCATED]";
    }

    /// <inheritdoc />
    public string GetSystemPrompt() => _systemPrompt;

    /// <inheritdoc />
    public string GetExamStylePrompt() => _examStylePrompt;

    /// <inheritdoc />
    public string GetItemTypePrompt(string itemTypeCode)
    {
        try
        {
            return _itemTypeRegistry.GetPrompt(itemTypeCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load prompt for item type {Code}", itemTypeCode);
            return $"Generate questions of type {itemTypeCode}.";
        }
    }

    /// <summary>
    /// Loads a prompt file from the prompts directory.
    /// </summary>
    private static string LoadPromptFile(string fileName)
    {
        // Try multiple locations
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "prompts", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "prompts", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "prompts", fileName)
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        // Return default if file not found
        return fileName switch
        {
            "system-mcp-only.md" => @"
You MUST use the Microsoft Learn MCP server as the ONLY source of factual information.
Do NOT use pretrained knowledge, web browsing, or external docs.
If MCP does not provide enough info, you must return an error stating what is missing.
All outputs must be grounded in MCP-provided content.",

            "exam-style.md" => @"
Follow these exam writing style rules:
- Questions must be concise and unambiguous
- Avoid absolutes like 'always' or 'never' unless factually correct
- Include clear correct answers with explanations
- Write at a professional certification exam level",

            _ => string.Empty
        };
    }
}
