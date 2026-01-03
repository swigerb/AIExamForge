using System.ClientModel;
using System.Diagnostics;
using System.Text;
using Azure;
using Azure.AI.OpenAI;
using AIExamForge.Application.Generation.Interfaces;
using AIExamForge.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AIExamForge.Infrastructure.OpenAI;

/// <summary>
/// Azure OpenAI client for LLM-based question generation.
/// Uses the Azure OpenAI service with JSON mode enabled.
/// </summary>
public sealed class AzureOpenAiClient : ILlmClient
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<AzureOpenAiClient> _logger;
    private readonly AzureOpenAiOptions _options;

    public AzureOpenAiClient(
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Create Azure OpenAI client
        var credential = new AzureKeyCredential(_options.ApiKey);
        var openAiClient = new AzureOpenAIClient(new Uri(_options.Endpoint), credential);
        _chatClient = openAiClient.GetChatClient(_options.DeploymentName);
    }

    /// <inheritdoc />
    public async Task<LlmResponse> GenerateAsync(
        PromptPayload prompt,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting LLM generation [{RequestId}]: {ItemTypes}, {Count} questions",
                requestId, string.Join(",", prompt.ItemTypes), prompt.QuestionCount);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt.SystemPrompt),
                new UserChatMessage(prompt.UserPrompt + "\n\n" + prompt.GetJsonSchemaInstruction())
            };

            var completionOptions = new ChatCompletionOptions
            {
                Temperature = _options.Temperature,
                MaxOutputTokenCount = _options.MaxTokens,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await _chatClient.CompleteChatAsync(messages, completionOptions, cancellationToken);
            var chatCompletion = response.Value;

            stopwatch.Stop();

            var content = chatCompletion.Content.FirstOrDefault()?.Text ?? string.Empty;

            _logger.LogInformation("LLM generation [{RequestId}] completed in {TimeMs}ms. Tokens: {Input}/{Output}",
                requestId,
                stopwatch.ElapsedMilliseconds,
                chatCompletion.Usage?.InputTokenCount ?? 0,
                chatCompletion.Usage?.OutputTokenCount ?? 0);

            return LlmResponse.Successful(
                content,
                chatCompletion.Usage?.InputTokenCount ?? 0,
                chatCompletion.Usage?.OutputTokenCount ?? 0,
                stopwatch.ElapsedMilliseconds,
                _options.DeploymentName,
                requestId,
                chatCompletion.FinishReason.ToString());
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "Azure OpenAI error [{RequestId}]: {Status}", requestId, ex.Status);
            return LlmResponse.Failed($"Azure OpenAI error: {ex.Message}", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during LLM generation [{RequestId}]", requestId);
            return LlmResponse.Failed($"Generation failed: {ex.Message}", requestId);
        }
    }

    /// <inheritdoc />
    public async Task<LlmResponse> GenerateStreamingAsync(
        PromptPayload prompt,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();
        var contentBuilder = new StringBuilder();

        try
        {
            _logger.LogInformation("Starting streaming LLM generation [{RequestId}]", requestId);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt.SystemPrompt),
                new UserChatMessage(prompt.UserPrompt + "\n\n" + prompt.GetJsonSchemaInstruction())
            };

            var completionOptions = new ChatCompletionOptions
            {
                Temperature = _options.Temperature,
                MaxOutputTokenCount = _options.MaxTokens,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var streamingResult = _chatClient.CompleteChatStreamingAsync(messages, completionOptions, cancellationToken);

            await foreach (var update in streamingResult)
            {
                foreach (var contentPart in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(contentPart.Text))
                    {
                        contentBuilder.Append(contentPart.Text);
                        await onChunk(contentPart.Text);
                    }
                }
            }

            stopwatch.Stop();

            _logger.LogInformation("Streaming LLM generation [{RequestId}] completed in {TimeMs}ms",
                requestId, stopwatch.ElapsedMilliseconds);

            return LlmResponse.Successful(
                contentBuilder.ToString(),
                0, // Token counts not available in streaming mode
                0,
                stopwatch.ElapsedMilliseconds,
                _options.DeploymentName,
                requestId);
        }
        catch (ClientResultException ex)
        {
            _logger.LogError(ex, "Azure OpenAI streaming error [{RequestId}]: {Status}", requestId, ex.Status);
            return LlmResponse.Failed($"Azure OpenAI error: {ex.Message}", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during streaming LLM generation [{RequestId}]", requestId);
            return LlmResponse.Failed($"Generation failed: {ex.Message}", requestId);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple test call
            var messages = new List<ChatMessage>
            {
                new UserChatMessage("Hello")
            };

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 5
            };

            await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Configuration options for Azure OpenAI client.
/// </summary>
public sealed class AzureOpenAiOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "AzureOpenAI";

    /// <summary>
    /// Azure OpenAI endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name (model deployment).
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-4-turbo";

    /// <summary>
    /// Temperature for generation (0.0-1.0). Default: 0.7.
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Maximum tokens for response. Default: 4096.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
}
