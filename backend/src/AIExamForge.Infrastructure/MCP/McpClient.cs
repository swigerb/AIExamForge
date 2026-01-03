using System.Diagnostics;
using System.Net.Http.Json;
using AIExamForge.Application.Mcp.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIExamForge.Infrastructure.MCP;

/// <summary>
/// Client for Microsoft Learn MCP (Model Context Protocol) server.
/// Per Constitution Principle VI: MCP is the exclusive content source for question generation.
/// </summary>
public sealed class McpClient : IMcpClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpClient> _logger;
    private readonly McpClientOptions _options;
    private bool _disposed;

    public McpClient(
        HttpClient httpClient,
        IOptions<McpClientOptions> options,
        ILogger<McpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Configure base address and timeout
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<McpQueryResult> QueryAsync(string topic, CancellationToken cancellationToken = default)
    {
        return await QueryAsync([topic], null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<McpQueryResult> QueryAsync(
        IEnumerable<string> topics,
        IEnumerable<string>? keywords = null,
        CancellationToken cancellationToken = default)
    {
        var queryId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var topicList = topics.ToList();
            var queryString = string.Join(" ", topicList);
            if (keywords != null)
            {
                queryString += " " + string.Join(" ", keywords);
            }

            _logger.LogDebug("MCP Query [{QueryId}]: {Query}", queryId, queryString);

            // Build the request for Microsoft Learn MCP server
            var request = new McpSearchRequest
            {
                Query = queryString,
                MaxResults = _options.MaxResults
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/search",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("MCP Query [{QueryId}] failed: {Status} - {Error}",
                    queryId, response.StatusCode, errorContent);

                return McpQueryResult.Failed(
                    $"MCP server returned {response.StatusCode}: {errorContent}",
                    queryId);
            }

            var result = await response.Content.ReadFromJsonAsync<McpSearchResponse>(cancellationToken);
            
            if (result == null || result.Results.Count == 0)
            {
                _logger.LogWarning("MCP Query [{QueryId}] returned no results for: {Query}", queryId, queryString);
                return McpQueryResult.Empty(queryId, stopwatch.ElapsedMilliseconds);
            }

            var chunks = result.Results
                .Select(r => new McpContentChunk
                {
                    Content = r.Content,
                    SourceUrl = r.Url,
                    Title = r.Title,
                    RelevanceScore = r.Score
                })
                .ToList()
                .AsReadOnly();

            _logger.LogInformation("MCP Query [{QueryId}] returned {Count} results in {TimeMs}ms",
                queryId, chunks.Count, stopwatch.ElapsedMilliseconds);

            return McpQueryResult.Successful(chunks, queryId, stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("MCP Query [{QueryId}] was cancelled", queryId);
            throw;
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("MCP Query [{QueryId}] timed out after {Timeout}s",
                queryId, _options.TimeoutSeconds);
            return McpQueryResult.Failed("Search took too long. Please try again.", queryId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "MCP Query [{QueryId}] failed due to HTTP error", queryId);
            return McpQueryResult.Failed($"Failed to connect to MCP server: {ex.Message}", queryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Query [{QueryId}] failed unexpectedly", queryId);
            return McpQueryResult.Failed($"Unexpected error: {ex.Message}", queryId);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration options for MCP client.
/// </summary>
public sealed class McpClientOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "McpClient";

    /// <summary>
    /// Base URL for the MCP server.
    /// </summary>
    public string BaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Timeout for MCP queries in seconds. Default: 10 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of results to return per query. Default: 10.
    /// </summary>
    public int MaxResults { get; set; } = 10;
}

/// <summary>
/// Request model for MCP search endpoint.
/// </summary>
internal sealed record McpSearchRequest
{
    public required string Query { get; init; }
    public int MaxResults { get; init; } = 10;
}

/// <summary>
/// Response model from MCP search endpoint.
/// </summary>
internal sealed record McpSearchResponse
{
    public IReadOnlyList<McpSearchResult> Results { get; init; } = [];
}

/// <summary>
/// Individual search result from MCP.
/// </summary>
internal sealed record McpSearchResult
{
    public string Content { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public double? Score { get; init; }
}
