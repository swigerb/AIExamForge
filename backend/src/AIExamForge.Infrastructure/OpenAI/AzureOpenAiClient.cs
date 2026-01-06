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
    private readonly object _clientLock = new();
    private ChatClient? _chatClient;
    private readonly ILogger<AzureOpenAiClient> _logger;
    private readonly AzureOpenAiOptions _options;

    public AzureOpenAiClient(
        IOptions<AzureOpenAiOptions> options,
        ILogger<AzureOpenAiClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private ChatClient GetChatClient()
    {
        if (_chatClient is not null)
        {
            return _chatClient;
        }

        lock (_clientLock)
        {
            if (_chatClient is not null)
            {
                return _chatClient;
            }

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI API key is not configured. Set AzureOpenAI:ApiKey (for example via environment variable AZUREOPENAI__APIKEY, user-secrets, or Key Vault).");
            }

            if (string.IsNullOrWhiteSpace(_options.Endpoint))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI endpoint is not configured. Set AzureOpenAI:Endpoint to your resource base URL (e.g., https://<resource>.openai.azure.com/)."
                );
            }

            if (string.IsNullOrWhiteSpace(_options.DeploymentName))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI deployment name is not configured. Set AzureOpenAI:DeploymentName to an existing deployment in your Azure OpenAI resource."
                );
            }

            var credential = new AzureKeyCredential(_options.ApiKey);
            var normalizedEndpoint = NormalizeEndpoint(_options.Endpoint);
            var openAiClient = new AzureOpenAIClient(new Uri(normalizedEndpoint), credential);
            _chatClient = openAiClient.GetChatClient(_options.DeploymentName);
            return _chatClient;
        }
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return string.Empty;
        }

        // The Azure SDK expects the *resource base URL* (scheme + host [+ port]), not a full
        // /openai/deployments/.../chat/completions URL.
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            var builder = new UriBuilder(uri.Scheme, uri.Host)
            {
                Port = uri.IsDefaultPort ? -1 : uri.Port,
                Path = string.Empty,
                Query = string.Empty,
                Fragment = string.Empty
            };

            var baseUri = builder.Uri;
            return baseUri.AbsoluteUri.EndsWith('/') ? baseUri.AbsoluteUri : baseUri.AbsoluteUri + "/";
        }

        return endpoint;
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

            var response = await GetChatClient().CompleteChatAsync(messages, completionOptions, cancellationToken);
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

            if (ex.Status == 404)
            {
                var endpointHost = Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var uri)
                    ? uri.Host
                    : _options.Endpoint;

                return LlmResponse.Failed(
                    $"Azure OpenAI 404 Resource not found. This almost always means configuration mismatch (wrong AzureOpenAI:Endpoint or AzureOpenAI:DeploymentName). " +
                    $"Endpoint host: '{endpointHost}'. DeploymentName: '{_options.DeploymentName}'. " +
                    "Note: Endpoint must be the resource base URL (e.g., https://<resource>.openai.azure.com/), not a /chat/completions URL.",
                    requestId);
            }

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

            var streamingResult = GetChatClient().CompleteChatStreamingAsync(messages, completionOptions, cancellationToken);

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

            if (ex.Status == 404)
            {
                var endpointHost = Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var uri)
                    ? uri.Host
                    : _options.Endpoint;

                return LlmResponse.Failed(
                    $"Azure OpenAI 404 Resource not found. This almost always means configuration mismatch (wrong AzureOpenAI:Endpoint or AzureOpenAI:DeploymentName). " +
                    $"Endpoint host: '{endpointHost}'. DeploymentName: '{_options.DeploymentName}'. " +
                    "Note: Endpoint must be the resource base URL (e.g., https://<resource>.openai.azure.com/), not a /chat/completions URL.",
                    requestId);
            }

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

            await GetChatClient().CompleteChatAsync(messages, options, cancellationToken);
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
