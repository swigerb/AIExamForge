using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIExamForge.Application.Mcp.Interfaces;
using AIExamForge.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIExamForge.Infrastructure.MCP;

/// <summary>
/// Client for Microsoft Learn MCP (Model Context Protocol) server.
/// Per Constitution Principle VI: MCP is the exclusive content source for question generation.
/// </summary>
public sealed partial class McpClient : IMcpClient
{
    private static readonly ActivitySource ActivitySource = new("AIExamForge.Mcp");

    private readonly HttpClient _httpClient;
    private readonly ILogger<McpClient> _logger;
    private readonly McpClientOptions _options;
    private readonly DiagnosticsLoggingOptions _diagnostics;

    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private string? _mcpSessionId;
    private string? _negotiatedProtocolVersion;

    private readonly SemaphoreSlim _toolsLock = new(1, 1);
    private IReadOnlyList<McpToolDefinition> _cachedTools = [];
    private DateTimeOffset _toolsLastRefreshedUtc;
    private volatile bool _toolsCacheStale;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] SupportedProtocolVersions =
    [
        // Prefer newer versions first; fall back for compatibility.
        "2025-11-25",
        "2025-06-18",
        "2025-03-26",
        "2024-11-05"
    ];

    public McpClient(
        HttpClient httpClient,
        IOptions<McpClientOptions> options,
        IOptions<DiagnosticsLoggingOptions> diagnostics,
        ILogger<McpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _diagnostics = diagnostics.Value;
        _logger = logger;

        // Configure base address and timeout.
        // The Microsoft Learn MCP endpoint is a Streamable HTTP MCP endpoint.
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/'));
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
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

        using var activity = ActivitySource.StartActivity("mcp.query", ActivityKind.Internal);
        activity?.SetTag("mcp.query_id", queryId);

        try
        {
            var topicList = topics.ToList();
            var queryString = string.Join(" ", topicList);
            if (keywords != null)
            {
                queryString += " " + string.Join(" ", keywords);
            }

            activity?.SetTag("mcp.topic.count", topicList.Count);
            activity?.SetTag("mcp.query", queryString);
            _logger.LogDebug("MCP Query [{QueryId}]: {Query}", queryId, queryString);

            await EnsureInitializedAsync(queryId, cancellationToken);
            await EnsureToolsCachedAsync(queryId, cancellationToken);

            var searchTool = ResolveDocsSearchTool();
            if (searchTool == null)
            {
                return McpQueryResult.Failed(
                    "Microsoft Learn MCP server did not advertise a documentation search tool.",
                    queryId);
            }

            var searchPayload = await CallToolAsync(
                searchTool.Name,
                new Dictionary<string, object?> { ["query"] = queryString },
                queryId,
                cancellationToken);

            if (!TryReadStructuredContent(searchPayload, out DocsSearchResponse? searchResult) || searchResult == null)
            {
                return McpQueryResult.Failed("Microsoft Learn MCP search returned an unexpected response.", queryId);
            }

            var docs = searchResult.Results
                .Where(r => !string.IsNullOrWhiteSpace(r.ContentUrl))
                .Take(_options.MaxResults)
                .ToList();

            if (docs.Count == 0)
            {
                _logger.LogWarning("MCP Query [{QueryId}] returned no results for: {Query}", queryId, queryString);
                return McpQueryResult.Empty(queryId, stopwatch.ElapsedMilliseconds);
            }

            var fetchTool = ResolveDocsFetchTool();

            var chunks = new List<McpContentChunk>(capacity: docs.Count);
            var fetchCount = 0;

            foreach (var doc in docs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Prefer fetched full doc content when available, but keep the client resilient:
                // fall back to the search excerpt if fetch fails.
                string content;
                if (fetchTool != null && fetchCount < _options.MaxFetchDocuments)
                {
                    var fetched = await TryFetchDocumentAsync(fetchTool.Name, doc.ContentUrl, queryId, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(fetched))
                    {
                        content = fetched;
                        fetchCount++;
                    }
                    else
                    {
                        content = doc.Content;
                    }
                }
                else
                {
                    content = doc.Content;
                }

                content = TrimContent(content, _options.MaxCharsPerDocument);

                chunks.Add(new McpContentChunk
                {
                    Content = content,
                    SourceUrl = doc.ContentUrl,
                    Title = doc.Title,
                    RelevanceScore = null
                });
            }

            _logger.LogInformation(
                "MCP Query [{QueryId}] returned {Count} docs in {TimeMs}ms (fetched={Fetched})",
                queryId,
                chunks.Count,
                stopwatch.ElapsedMilliseconds,
                fetchCount);

            activity?.SetTag("mcp.docs.count", chunks.Count);
            activity?.SetTag("mcp.docs.fetched.count", fetchCount);
            activity?.SetTag("mcp.elapsed_ms", stopwatch.ElapsedMilliseconds);

            return McpQueryResult.Successful(chunks.AsReadOnly(), queryId, stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("MCP Query [{QueryId}] was cancelled", queryId);
            activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
            throw;
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("MCP Query [{QueryId}] timed out after {Timeout}s",
                queryId, _options.TimeoutSeconds);
            activity?.SetStatus(ActivityStatusCode.Error, "timeout");
            return McpQueryResult.Failed("Search took too long. Please try again.", queryId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "MCP Query [{QueryId}] failed due to HTTP error", queryId);
            activity?.AddEvent(new ActivityEvent(
                "exception",
                default,
                new ActivityTagsCollection
                {
                    ["exception.type"] = ex.GetType().FullName,
                    ["exception.message"] = ex.Message,
                    ["exception.stacktrace"] = ex.ToString()
                }));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return McpQueryResult.Failed($"Failed to connect to MCP server: {ex.Message}", queryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Query [{QueryId}] failed unexpectedly", queryId);
            activity?.AddEvent(new ActivityEvent(
                "exception",
                default,
                new ActivityTagsCollection
                {
                    ["exception.type"] = ex.GetType().FullName,
                    ["exception.message"] = ex.Message,
                    ["exception.stacktrace"] = ex.ToString()
                }));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return McpQueryResult.Failed($"Unexpected error: {ex.Message}", queryId);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync("avail", cancellationToken);
            await EnsureToolsCachedAsync("avail", cancellationToken);
            return _cachedTools.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<McpCodeSampleSearchResult> SearchCodeSamplesAsync(
        string query,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var queryId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return McpCodeSampleSearchResult.Failed("Query is required.", queryId);
            }

            await EnsureInitializedAsync(queryId, cancellationToken);
            await EnsureToolsCachedAsync(queryId, cancellationToken);

            var tool = ResolveCodeSampleSearchTool();
            if (tool == null)
            {
                return McpCodeSampleSearchResult.Failed(
                    "Microsoft Learn MCP server did not advertise a code sample search tool.",
                    queryId);
            }

            var args = new Dictionary<string, object?>
            {
                ["query"] = query
            };

            if (!string.IsNullOrWhiteSpace(language))
            {
                args["language"] = language;
            }

            var payload = await CallToolAsync(tool.Name, args, queryId, cancellationToken);

            if (!TryReadStructuredContent(payload, out CodeSampleSearchResponse? result) || result == null)
            {
                return McpCodeSampleSearchResult.Failed(
                    "Microsoft Learn MCP code sample search returned an unexpected response.",
                    queryId);
            }

            var samples = result.Results
                .Select(r => new McpCodeSample
                {
                    Title = r.Title,
                    Url = r.Url,
                    Language = r.Language,
                    Code = r.Code
                })
                .ToList()
                .AsReadOnly();

            return McpCodeSampleSearchResult.Successful(samples, queryId, stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("MCP code sample search [{QueryId}] was cancelled", queryId);
            throw;
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("MCP code sample search [{QueryId}] timed out after {Timeout}s",
                queryId, _options.TimeoutSeconds);
            return McpCodeSampleSearchResult.Failed("Search took too long. Please try again.", queryId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "MCP code sample search [{QueryId}] failed due to HTTP error", queryId);
            return McpCodeSampleSearchResult.Failed($"Failed to connect to MCP server: {ex.Message}", queryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP code sample search [{QueryId}] failed unexpectedly", queryId);
            return McpCodeSampleSearchResult.Failed($"Unexpected error: {ex.Message}", queryId);
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
    /// For Microsoft Learn: https://learn.microsoft.com/api/mcp
    /// </summary>
    public string BaseUrl { get; set; } = "https://learn.microsoft.com/api/mcp";

    /// <summary>
    /// Timeout for MCP queries in seconds. Default: 10 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of results to return per query. Default: 10.
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Maximum number of documents to fetch in full (via docs fetch tool).
    /// </summary>
    public int MaxFetchDocuments { get; set; } = 3;

    /// <summary>
    /// Maximum number of characters to keep per document chunk.
    /// </summary>
    public int MaxCharsPerDocument { get; set; } = 12000;

    /// <summary>
    /// Tool cache TTL in seconds.
    /// </summary>
    public int ToolCacheTtlSeconds { get; set; } = 600;
}

public sealed partial class McpClient
{
    private async Task EnsureInitializedAsync(string queryId, CancellationToken cancellationToken)
    {
        if (_negotiatedProtocolVersion != null)
        {
            return;
        }

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            if (_negotiatedProtocolVersion != null)
            {
                return;
            }

            foreach (var candidate in SupportedProtocolVersions)
            {
                var initId = 1;
                var request = new JsonRpcRequest<InitializeParams>
                {
                    Id = initId,
                    Method = "initialize",
                    Params = new InitializeParams
                    {
                        ProtocolVersion = candidate,
                        Capabilities = new InitializeCapabilities
                        {
                            Roots = new InitializeListChangedCapability { ListChanged = false },
                            Sampling = new(),
                            Elicitation = new()
                        },
                        ClientInfo = new ImplementationInfo
                        {
                            Name = "AIExamForge",
                            Title = "AIExamForge Backend",
                            Version = "1.0.0"
                        }
                    }
                };

                var initResponse = await SendRequestAsync<InitializeParams, InitializeResult>(
                    request,
                    includeSessionHeaders: false,
                    mcpProtocolVersionHeader: null,
                    queryId,
                    cancellationToken);

                if (initResponse.Error != null)
                {
                    _logger.LogWarning(
                        "MCP initialize failed for protocol {ProtocolVersion}: {Code} {Message}",
                        candidate,
                        initResponse.Error.Code,
                        initResponse.Error.Message);
                    continue;
                }

                if (initResponse.Result == null)
                {
                    continue;
                }

                _negotiatedProtocolVersion = initResponse.Result.ProtocolVersion;
                if (!string.IsNullOrWhiteSpace(initResponse.McpSessionId))
                {
                    _mcpSessionId = initResponse.McpSessionId;
                }

                _logger.LogInformation(
                    "MCP initialized: protocol={ProtocolVersion} session={SessionIdPresent}",
                    _negotiatedProtocolVersion,
                    !string.IsNullOrWhiteSpace(_mcpSessionId));

                // Per lifecycle, send notifications/initialized after initialize succeeds.
                var initialized = new JsonRpcNotification
                {
                    Method = "notifications/initialized"
                };

                await SendNotificationAsync(
                    initialized,
                    queryId,
                    cancellationToken);

                return;
            }

            throw new InvalidOperationException("Failed to initialize MCP session: no compatible protocol version.");
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task EnsureToolsCachedAsync(string queryId, CancellationToken cancellationToken)
    {
        if (!_toolsCacheStale && _cachedTools.Count > 0 &&
            DateTimeOffset.UtcNow - _toolsLastRefreshedUtc < TimeSpan.FromSeconds(_options.ToolCacheTtlSeconds))
        {
            return;
        }

        await _toolsLock.WaitAsync(cancellationToken);
        try
        {
            if (!_toolsCacheStale && _cachedTools.Count > 0 &&
                DateTimeOffset.UtcNow - _toolsLastRefreshedUtc < TimeSpan.FromSeconds(_options.ToolCacheTtlSeconds))
            {
                return;
            }

            await RefreshToolsAsync(queryId, cancellationToken);
        }
        finally
        {
            _toolsLock.Release();
        }
    }

    private async Task RefreshToolsAsync(string queryId, CancellationToken cancellationToken)
    {
        var tools = new List<McpToolDefinition>();
        string? cursor = null;

        for (var page = 0; page < 10; page++)
        {
            var request = new JsonRpcRequest<ToolsListParams>
            {
                Id = 10 + page,
                Method = "tools/list",
                Params = new ToolsListParams { Cursor = cursor }
            };

            var resp = await SendRequestAsync<ToolsListParams, ToolsListResult>(
                request,
                includeSessionHeaders: true,
                mcpProtocolVersionHeader: _negotiatedProtocolVersion,
                queryId,
                cancellationToken);

            if (resp.Error != null)
            {
                throw new InvalidOperationException($"tools/list failed: {resp.Error.Code} {resp.Error.Message}");
            }

            if (resp.Result?.Tools != null)
            {
                tools.AddRange(resp.Result.Tools);
            }

            cursor = resp.Result?.NextCursor;
            if (string.IsNullOrWhiteSpace(cursor))
            {
                break;
            }
        }

        _cachedTools = tools
            .GroupBy(t => t.Name)
            .Select(g => g.First())
            .ToList()
            .AsReadOnly();

        _toolsLastRefreshedUtc = DateTimeOffset.UtcNow;
        _toolsCacheStale = false;

        _logger.LogInformation("MCP tools cached: {Count}", _cachedTools.Count);
    }

    private McpToolDefinition? ResolveDocsSearchTool()
    {
        // Prefer the known Microsoft Learn docs tool name when present.
        var exact = _cachedTools.FirstOrDefault(t =>
            string.Equals(t.Name, "microsoft_docs_search", StringComparison.OrdinalIgnoreCase));

        // Fall back to a scored heuristic for resiliency, but avoid picking non-doc tools
        // like microsoft_code_sample_search (which also uses `query`).
        return exact ?? _cachedTools
            .OrderByDescending(t => ScoreToolForDocsSearch(t))
            .FirstOrDefault(t => ScoreToolForDocsSearch(t) > 0);
    }

    private McpToolDefinition? ResolveDocsFetchTool()
    {
        // Prefer the known Microsoft Learn docs tool name when present.
        var exact = _cachedTools.FirstOrDefault(t =>
            string.Equals(t.Name, "microsoft_docs_fetch", StringComparison.OrdinalIgnoreCase));

        // Fall back to a scored heuristic for resiliency.
        return exact ?? _cachedTools
            .OrderByDescending(t => ScoreToolForDocsFetch(t))
            .FirstOrDefault(t => ScoreToolForDocsFetch(t) > 0);
    }

    private McpToolDefinition? ResolveCodeSampleSearchTool()
    {
        // Prefer the known Microsoft Learn code sample tool name when present.
        var exact = _cachedTools.FirstOrDefault(t =>
            string.Equals(t.Name, "microsoft_code_sample_search", StringComparison.OrdinalIgnoreCase));

        return exact ?? _cachedTools
            .OrderByDescending(t => ScoreToolForCodeSampleSearch(t))
            .FirstOrDefault(t => ScoreToolForCodeSampleSearch(t) > 0);
    }

    private static int ScoreToolForCodeSampleSearch(McpToolDefinition tool)
    {
        if (tool.InputSchema?.Properties?.ContainsKey("query") != true)
        {
            return 0;
        }

        var isCodeSamples = tool.Name.Contains("code", StringComparison.OrdinalIgnoreCase) ||
            (tool.Description?.Contains("code", StringComparison.OrdinalIgnoreCase) == true) ||
            (tool.Description?.Contains("snippet", StringComparison.OrdinalIgnoreCase) == true) ||
            (tool.Description?.Contains("sample", StringComparison.OrdinalIgnoreCase) == true);

        if (!isCodeSamples)
        {
            return 0;
        }

        var score = 10;

        if (tool.Name.Contains("search", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (tool.InputSchema?.Properties?.ContainsKey("language") == true)
        {
            score += 1;
        }

        return score;
    }

    private static int ScoreToolForDocsSearch(McpToolDefinition tool)
    {
        if (tool.InputSchema?.Properties?.ContainsKey("query") != true)
        {
            return 0;
        }

        // Require an explicit docs/documentation signal so we don't pick tools like
        // microsoft_code_sample_search just because they accept `query`.
        var isDocs = tool.Name.Contains("docs", StringComparison.OrdinalIgnoreCase) ||
            (tool.Description?.Contains("documentation", StringComparison.OrdinalIgnoreCase) == true);

        if (!isDocs)
        {
            return 0;
        }

        var score = 10;

        if (tool.Name.Contains("search", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        return score;
    }

    private static int ScoreToolForDocsFetch(McpToolDefinition tool)
    {
        if (tool.InputSchema?.Properties?.ContainsKey("url") != true)
        {
            return 0;
        }

        var isDocs = tool.Name.Contains("docs", StringComparison.OrdinalIgnoreCase) ||
            (tool.Description?.Contains("documentation", StringComparison.OrdinalIgnoreCase) == true);

        if (!isDocs)
        {
            return 0;
        }

        var score = 10;

        if (tool.Name.Contains("fetch", StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        return score;
    }

    private async Task<string?> TryFetchDocumentAsync(
        string fetchToolName,
        string url,
        string queryId,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await CallToolAsync(
                fetchToolName,
                new Dictionary<string, object?> { ["url"] = url },
                queryId,
                cancellationToken);

            return ExtractToolText(payload);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MCP fetch failed for {Url}", url);
            return null;
        }
    }

    private async Task<McpToolCallResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        string queryId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("mcp.tools.call", ActivityKind.Internal);
        activity?.SetTag("mcp.query_id", queryId);
        activity?.SetTag("mcp.tool.name", toolName);
        activity?.SetTag("mcp.tool.args.count", arguments.Count);
        activity?.SetTag("mcp.session_id", _mcpSessionId);
        activity?.SetTag("mcp.protocol_version", _negotiatedProtocolVersion);
        activity?.SetTag("rpc.system", "jsonrpc");
        activity?.SetTag("rpc.method", "tools/call");

        var request = new JsonRpcRequest<ToolsCallParams>
        {
            Id = Random.Shared.Next(1000, 9999),
            Method = "tools/call",
            Params = new ToolsCallParams
            {
                Name = toolName,
                Arguments = arguments
            }
        };

        try
        {
            var response = await SendRequestWithRefreshOnFailureAsync<ToolsCallParams, McpToolCallResult>(
                request,
                queryId,
                cancellationToken);

            activity?.SetTag("rpc.request_id", request.Id);

            if (response.Error != null)
            {
                activity?.SetTag("mcp.jsonrpc.error.code", response.Error.Code);
                activity?.SetTag("mcp.jsonrpc.error.message", response.Error.Message);
                throw new InvalidOperationException($"tools/call failed: {response.Error.Code} {response.Error.Message}");
            }

            if (response.Result == null)
            {
                throw new InvalidOperationException("tools/call returned no result.");
            }

            if (response.Result.IsError == true)
            {
                var message = ExtractToolText(response.Result) ?? "Tool returned an error.";
                throw new InvalidOperationException(message);
            }

            return response.Result;
        }
        catch (Exception ex)
        {
            activity?.AddEvent(new ActivityEvent(
                "exception",
                default,
                new ActivityTagsCollection
                {
                    ["exception.type"] = ex.GetType().FullName,
                    ["exception.message"] = ex.Message,
                    ["exception.stacktrace"] = ex.ToString()
                }));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task<JsonRpcEnvelopeResponse<TResult>> SendRequestWithRefreshOnFailureAsync<TParams, TResult>(
        JsonRpcRequest<TParams> request,
        string queryId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendRequestAsync<TParams, TResult>(
                request,
                includeSessionHeaders: true,
                mcpProtocolVersionHeader: _negotiatedProtocolVersion,
                queryId,
                cancellationToken);
        }
        catch (HttpRequestException) when (_toolsCacheStale)
        {
            // If the server told us tools changed, refresh and retry once.
            await EnsureToolsCachedAsync(queryId, cancellationToken);
            return await SendRequestAsync<TParams, TResult>(
                request,
                includeSessionHeaders: true,
                mcpProtocolVersionHeader: _negotiatedProtocolVersion,
                queryId,
                cancellationToken);
        }
        catch (McpSessionExpiredException)
        {
            // Per Streamable HTTP: HTTP 404 with Mcp-Session-Id means session expired.
            await ResetSessionAsync(queryId, cancellationToken);
            await EnsureInitializedAsync(queryId, cancellationToken);
            await EnsureToolsCachedAsync(queryId, cancellationToken);
            return await SendRequestAsync<TParams, TResult>(
                request,
                includeSessionHeaders: true,
                mcpProtocolVersionHeader: _negotiatedProtocolVersion,
                queryId,
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // If the tool schema changed, refresh and retry once.
            _toolsCacheStale = true;
            await EnsureToolsCachedAsync(queryId, cancellationToken);
            return await SendRequestAsync<TParams, TResult>(
                request,
                includeSessionHeaders: true,
                mcpProtocolVersionHeader: _negotiatedProtocolVersion,
                queryId,
                cancellationToken);
        }
    }

    private async Task ResetSessionAsync(string queryId, CancellationToken cancellationToken)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogWarning("MCP session expired; resetting.");
            _mcpSessionId = null;
            _negotiatedProtocolVersion = null;
            _toolsCacheStale = true;
            _cachedTools = [];
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task<JsonRpcEnvelopeResponse<TResult>> SendRequestAsync<TParams, TResult>(
        JsonRpcRequest<TParams> request,
        bool includeSessionHeaders,
        string? mcpProtocolVersionHeader,
        string queryId,
        CancellationToken cancellationToken)
    {
        var bodyJson = JsonSerializer.Serialize(request, JsonOptions);

        if (_diagnostics.Enabled && _diagnostics.LogMcpRequestBodies)
        {
            _logger.LogInformation(
                "DIAGNOSTICS MCP [{QueryId}] REQUEST method={Method} id={Id} session={SessionId}\n{Body}",
                queryId,
                request.Method,
                request.Id,
                _mcpSessionId,
                Truncate(bodyJson, _diagnostics.MaxChars));
        }

        // Per Streamable HTTP:
        // - POST a single JSON-RPC message
        // - Accept must list application/json and text/event-stream
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (mcpProtocolVersionHeader != null)
        {
            httpRequest.Headers.TryAddWithoutValidation("MCP-Protocol-Version", mcpProtocolVersionHeader);
        }

        if (includeSessionHeaders && !string.IsNullOrWhiteSpace(_mcpSessionId))
        {
            httpRequest.Headers.TryAddWithoutValidation("Mcp-Session-Id", _mcpSessionId);
        }

        var response = await ExecuteWithRetriesAsync(
            () => _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken),
            queryId,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound && includeSessionHeaders && !string.IsNullOrWhiteSpace(_mcpSessionId))
        {
            throw new McpSessionExpiredException();
        }

        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (string.Equals(contentType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            var sseEnvelope = await ReadSseResponseAsync<TResult>(response, request.Id, queryId, cancellationToken);
            if (_diagnostics.Enabled && _diagnostics.LogMcpResponseBodies)
            {
                var sseJson = JsonSerializer.Serialize(new { sseEnvelope.McpSessionId, sseEnvelope.Error, sseEnvelope.Result }, JsonOptions);
                _logger.LogInformation(
                    "DIAGNOSTICS MCP [{QueryId}] RESPONSE(SSE) id={Id} session={SessionId}\n{Body}",
                    queryId,
                    request.Id,
                    sseEnvelope.McpSessionId,
                    Truncate(sseJson, _diagnostics.MaxChars));
            }

            return sseEnvelope;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (_diagnostics.Enabled && _diagnostics.LogMcpResponseBodies)
        {
            _logger.LogInformation(
                "DIAGNOSTICS MCP [{QueryId}] RESPONSE id={Id} session={SessionId}\n{Body}",
                queryId,
                request.Id,
                TryGetSessionId(response),
                Truncate(json, _diagnostics.MaxChars));
        }
        var envelope = JsonSerializer.Deserialize<JsonRpcResponse<TResult>>(json, JsonOptions);
        if (envelope == null)
        {
            throw new InvalidOperationException("Failed to deserialize MCP JSON response.");
        }

        return new JsonRpcEnvelopeResponse<TResult>
        {
            McpSessionId = TryGetSessionId(response),
            Error = envelope.Error,
            Result = envelope.Result
        };
    }

    private async Task SendNotificationAsync(JsonRpcNotification notification, string queryId, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(notification, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (_negotiatedProtocolVersion != null)
        {
            request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", _negotiatedProtocolVersion);
        }

        if (!string.IsNullOrWhiteSpace(_mcpSessionId))
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _mcpSessionId);
        }

        var resp = await ExecuteWithRetriesAsync(
            () => _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken),
            queryId,
            cancellationToken);

        // Notifications should be accepted with 202 or a JSON response.
        if (resp.StatusCode is not HttpStatusCode.Accepted and not HttpStatusCode.OK)
        {
            resp.EnsureSuccessStatusCode();
        }
    }

    private async Task<JsonRpcEnvelopeResponse<TResult>> ReadSseResponseAsync<TResult>(
        HttpResponseMessage response,
        object? expectedId,
        string queryId,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        JsonRpcResponse<TResult>? lastResponse = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                break;
            }

            // Only handle data lines.
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(data))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("method", out var method) &&
                method.ValueKind == JsonValueKind.String)
            {
                if (string.Equals(method.GetString(), "notifications/tools/list_changed", StringComparison.OrdinalIgnoreCase))
                {
                    _toolsCacheStale = true;
                }

                continue;
            }

            // Try parse as response.
            var parsed = doc.RootElement.Deserialize<JsonRpcResponse<TResult>>(JsonOptions);
            if (parsed == null)
            {
                continue;
            }

            if (expectedId == null)
            {
                lastResponse = parsed;
                continue;
            }

            if (parsed.Id != null && JsonRpcIdsEqual(parsed.Id, expectedId))
            {
                lastResponse = parsed;
                break;
            }
        }

        if (lastResponse == null)
        {
            throw new InvalidOperationException("SSE stream ended without a JSON-RPC response.");
        }

        return new JsonRpcEnvelopeResponse<TResult>
        {
            McpSessionId = TryGetSessionId(response),
            Error = lastResponse.Error,
            Result = lastResponse.Result
        };
    }

    private static bool JsonRpcIdsEqual(object? a, object? b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        // IDs can be numbers or strings.
        return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static string? TryGetSessionId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    private static async Task<HttpResponseMessage> ExecuteWithRetriesAsync(
        Func<Task<HttpResponseMessage>> action,
        string queryId,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        var delayMs = 250;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                await Task.Delay(delayMs, cancellationToken);
                delayMs *= 2;
            }
            catch (TaskCanceledException) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(delayMs, cancellationToken);
                delayMs *= 2;
            }
        }

        // Should be unreachable.
        throw new InvalidOperationException($"MCP HTTP request failed after retries. QueryId={queryId}");
    }

    private static string TrimContent(string content, int maxChars)
    {
        if (maxChars <= 0)
        {
            return content;
        }

        if (content.Length <= maxChars)
        {
            return content;
        }

        return content[..maxChars];
    }

    private static string? ExtractToolText(McpToolCallResult result)
    {
        if (result.Content == null || result.Content.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var item in result.Content)
        {
            if (!string.Equals(item.Type, "text", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.Text))
            {
                if (sb.Length > 0)
                {
                    sb.Append("\n\n");
                }
                sb.Append(item.Text);
            }
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    private static bool TryReadStructuredContent<T>(McpToolCallResult toolResult, out T? value)
    {
        value = default;

        if (toolResult.StructuredContent.HasValue)
        {
            try
            {
                value = toolResult.StructuredContent.Value.Deserialize<T>(JsonOptions);
                return value != null;
            }
            catch
            {
                // ignored
            }
        }

        // Back-compat: if a tool returns JSON in a text block, try to parse it.
        var text = ExtractToolText(toolResult);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            value = doc.RootElement.Deserialize<T>(JsonOptions);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    private sealed class McpSessionExpiredException : Exception;

    private sealed record JsonRpcEnvelopeResponse<TResult>
    {
        public TResult? Result { get; init; }
        public JsonRpcError? Error { get; init; }
        public string? McpSessionId { get; init; }
    }

    private sealed record JsonRpcRequest<TParams>
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; init; } = "2.0";

        [JsonPropertyName("id")]
        public object? Id { get; init; }

        [JsonPropertyName("method")]
        public required string Method { get; init; }

        [JsonPropertyName("params")]
        public required TParams Params { get; init; }
    }

    private sealed record JsonRpcNotification
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; init; } = "2.0";

        [JsonPropertyName("method")]
        public required string Method { get; init; }
    }

    private sealed record JsonRpcResponse<TResult>
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; init; } = "2.0";

        [JsonPropertyName("id")]
        public object? Id { get; init; }

        [JsonPropertyName("result")]
        public TResult? Result { get; init; }

        [JsonPropertyName("error")]
        public JsonRpcError? Error { get; init; }
    }

    private sealed record JsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; init; }

        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;

        [JsonPropertyName("data")]
        public JsonElement? Data { get; init; }
    }

    private sealed record InitializeParams
    {
        [JsonPropertyName("protocolVersion")]
        public required string ProtocolVersion { get; init; }

        [JsonPropertyName("capabilities")]
        public required InitializeCapabilities Capabilities { get; init; }

        [JsonPropertyName("clientInfo")]
        public required ImplementationInfo ClientInfo { get; init; }
    }

    private sealed record InitializeCapabilities
    {
        [JsonPropertyName("roots")]
        public InitializeListChangedCapability? Roots { get; init; }

        [JsonPropertyName("sampling")]
        public object? Sampling { get; init; }

        [JsonPropertyName("elicitation")]
        public object? Elicitation { get; init; }
    }

    private sealed record InitializeListChangedCapability
    {
        [JsonPropertyName("listChanged")]
        public bool ListChanged { get; init; }
    }

    private sealed record ImplementationInfo
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("title")]
        public required string Title { get; init; }

        [JsonPropertyName("version")]
        public required string Version { get; init; }
    }

    private sealed record InitializeResult
    {
        [JsonPropertyName("protocolVersion")]
        public required string ProtocolVersion { get; init; }

        [JsonPropertyName("capabilities")]
        public JsonElement? Capabilities { get; init; }

        [JsonPropertyName("serverInfo")]
        public JsonElement? ServerInfo { get; init; }

        [JsonPropertyName("instructions")]
        public string? Instructions { get; init; }
    }

    private sealed record ToolsListParams
    {
        [JsonPropertyName("cursor")]
        public string? Cursor { get; init; }
    }

    private sealed record ToolsListResult
    {
        [JsonPropertyName("tools")]
        public IReadOnlyList<McpToolDefinition> Tools { get; init; } = [];

        [JsonPropertyName("nextCursor")]
        public string? NextCursor { get; init; }
    }

    private sealed record ToolsCallParams
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("arguments")]
        public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    }

    private sealed record McpToolCallResult
    {
        [JsonPropertyName("content")]
        public IReadOnlyList<McpToolContentItem> Content { get; init; } = [];

        [JsonPropertyName("structuredContent")]
        public JsonElement? StructuredContent { get; init; }

        [JsonPropertyName("isError")]
        public bool? IsError { get; init; }
    }

    private sealed record McpToolContentItem
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed record McpToolDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("inputSchema")]
        public JsonSchema? InputSchema { get; init; }
    }

    private sealed record JsonSchema
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("properties")]
        public Dictionary<string, JsonElement>? Properties { get; init; }
    }

    private sealed record DocsSearchResponse
    {
        [JsonPropertyName("results")]
        public IReadOnlyList<DocsSearchResult> Results { get; init; } = [];
    }

    private sealed record DocsSearchResult
    {
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;

        [JsonPropertyName("contentUrl")]
        public string ContentUrl { get; init; } = string.Empty;
    }

    private sealed record CodeSampleSearchResponse
    {
        [JsonPropertyName("results")]
        public IReadOnlyList<CodeSampleSearchResult> Results { get; init; } = [];
    }

    private sealed record CodeSampleSearchResult
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("code")]
        public string? Code { get; init; }
    }
}
