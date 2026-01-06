using AIExamForge.Application.Mcp.Interfaces;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace AIExamForge.Application.Mcp;

/// <summary>
/// Service for retrieving content from Microsoft Learn MCP server.
/// Provides caching and query construction for exam question generation.
/// Per Constitution Principle VI: MCP is the exclusive content source.
/// </summary>
public sealed class McpRetrievalService
{
    private readonly IMcpClient _mcpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<McpRetrievalService> _logger;
    private readonly McpRetrievalOptions _options;
    private readonly Interfaces.IMcpQueryTraceStore? _traceStore;

    // Cache content for 30 minutes (content doesn't change frequently)
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public McpRetrievalService(
        IMcpClient mcpClient,
        IMemoryCache cache,
        IOptions<McpRetrievalOptions> options,
        ILogger<McpRetrievalService> logger,
        Interfaces.IMcpQueryTraceStore? traceStore = null)
    {
        _mcpClient = mcpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        _traceStore = traceStore;
    }

    /// <summary>
    /// Retrieves content for a single outline node.
    /// </summary>
    public async Task<McpContent?> RetrieveForNodeAsync(
        OutlineNode node,
        Guid? generationId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"mcp_node_{node.Id}";
        
        if (_cache.TryGetValue(cacheKey, out McpContent? cached))
        {
            _logger.LogDebug("Cache hit for node {NodeId}: {Title}", node.Id, node.Title);
            return cached;
        }

        var query = BuildQueryFromNode(node);
        if (generationId.HasValue)
        {
            _traceStore?.RecordQuery(generationId.Value, node.Id, query);
        }
        _logger.LogInformation("Querying MCP for node {NodeId}: {Query}", node.Id, query);

        var result = await _mcpClient.QueryAsync(query, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("MCP query failed for node {NodeId}: {Error}", node.Id, result.ErrorMessage);
            return McpContent.Create(query, string.Empty, Array.Empty<string>(), result.QueryTimeMs, node.Id);
        }

        if (result.ContentChunks.Count == 0)
        {
            _logger.LogWarning("No content found for node {NodeId}: {Query}", node.Id, query);
            return McpContent.Create(query, string.Empty, Array.Empty<string>(), result.QueryTimeMs, node.Id);
        }

        var combinedContent = result.GetCombinedContent();
        var sourceUrls = result.GetSourceUrls().ToList();
        var queryTimeMs = result.QueryTimeMs;

        if (ShouldIncludeCodeSamples(query, _options.CodeSampleMode))
        {
            var language = TryInferLanguage(query);
            var codeSamples = await _mcpClient.SearchCodeSamplesAsync(query, language, cancellationToken);

            if (codeSamples.Success && codeSamples.Samples.Count > 0)
            {
                combinedContent = AppendCodeSamples(
                    combinedContent,
                    codeSamples.Samples,
                    _options.MaxCodeSamples,
                    _options.MaxCharsPerCodeSample);
                sourceUrls.AddRange(codeSamples.Samples
                    .Select(s => s.Url)
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Select(u => u!));
                queryTimeMs += codeSamples.QueryTimeMs;
            }
        }

        var content = McpContent.Create(
            query,
            combinedContent,
            sourceUrls.Distinct().ToList().AsReadOnly(),
            queryTimeMs,
            node.Id);

        _cache.Set(cacheKey, content, CacheDuration);

        return content;
    }

    /// <summary>
    /// Retrieves content for multiple outline nodes.
    /// </summary>
    public async Task<McpContentCollection> RetrieveForNodesAsync(
        IEnumerable<OutlineNode> nodes,
        Guid? generationId,
        CancellationToken cancellationToken = default)
    {
        var nodeList = nodes.ToList();
        var contents = new List<McpContent>();

        _logger.LogInformation("Retrieving MCP content for {Count} nodes", nodeList.Count);

        foreach (var node in nodeList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var content = await RetrieveForNodeAsync(node, generationId, cancellationToken);
            if (content != null)
            {
                contents.Add(content);
            }
        }

        _logger.LogInformation("Retrieved {Retrieved}/{Total} nodes with content",
            contents.Count, nodeList.Count);

        return new McpContentCollection { Items = contents.AsReadOnly() };
    }

    /// <summary>
    /// Retrieves content for a specific topic string.
    /// </summary>
    public async Task<McpContent?> RetrieveForTopicAsync(
        string topic,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"mcp_topic_{topic.GetHashCode()}";
        
        if (_cache.TryGetValue(cacheKey, out McpContent? cached))
        {
            _logger.LogDebug("Cache hit for topic: {Topic}", topic);
            return cached;
        }

        _logger.LogInformation("Querying MCP for topic: {Topic}", topic);

        var result = await _mcpClient.QueryAsync(topic, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("MCP query failed for topic {Topic}: {Error}", topic, result.ErrorMessage);
            return null;
        }

        if (result.ContentChunks.Count == 0)
        {
            _logger.LogWarning("No content found for topic: {Topic}", topic);
            return null;
        }

        var combinedContent = result.GetCombinedContent();
        var sourceUrls = result.GetSourceUrls().ToList();
        var queryTimeMs = result.QueryTimeMs;

        if (ShouldIncludeCodeSamples(topic, _options.CodeSampleMode))
        {
            var language = TryInferLanguage(topic);
            var codeSamples = await _mcpClient.SearchCodeSamplesAsync(topic, language, cancellationToken);

            if (codeSamples.Success && codeSamples.Samples.Count > 0)
            {
                combinedContent = AppendCodeSamples(
                    combinedContent,
                    codeSamples.Samples,
                    _options.MaxCodeSamples,
                    _options.MaxCharsPerCodeSample);
                sourceUrls.AddRange(codeSamples.Samples
                    .Select(s => s.Url)
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Select(u => u!));
                queryTimeMs += codeSamples.QueryTimeMs;
            }
        }

        var content = McpContent.Create(
            topic,
            combinedContent,
            sourceUrls.Distinct().ToList().AsReadOnly(),
            queryTimeMs);

        _cache.Set(cacheKey, content, CacheDuration);

        return content;
    }

    /// <summary>
    /// Clears the MCP content cache.
    /// </summary>
    public void ClearCache()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0);
        }
        _logger.LogInformation("MCP content cache cleared");
    }

    /// <summary>
    /// Builds a search query from an outline node.
    /// </summary>
    private static string BuildQueryFromNode(OutlineNode node)
    {
        var parts = new List<string>();

        // Add the title
        parts.Add(node.Title);

        // Add content if available and meaningful
        if (!string.IsNullOrWhiteSpace(node.Content) && node.Content.Length > 10)
        {
            // Take first 200 chars of content
            var contentPreview = node.Content.Length > 200
                ? node.Content[..200]
                : node.Content;
            parts.Add(contentPreview);
        }

        return string.Join(" ", parts);
    }

    private static bool ShouldIncludeCodeSamples(string query, string mode)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        if (string.Equals(mode, "Never", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Apply a lightweight approximation of the microsoft-code-reference skill:
        // use code sample search when the query is about implementation, APIs, packages, or errors.
        return query.Contains("code", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("sample", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("snippet", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("example", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("implement", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("sdk", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("api", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("nuget", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("package", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("class ", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("interface ", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("method ", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("stack trace", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("403", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("401", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryInferLanguage(string query)
    {
        if (query.Contains("c#", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("dotnet", StringComparison.OrdinalIgnoreCase) ||
            query.Contains(".net", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("asp.net", StringComparison.OrdinalIgnoreCase))
        {
            return "csharp";
        }

        if (query.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            return "python";
        }

        if (query.Contains("typescript", StringComparison.OrdinalIgnoreCase))
        {
            return "typescript";
        }

        if (query.Contains("javascript", StringComparison.OrdinalIgnoreCase) || query.Contains("node", StringComparison.OrdinalIgnoreCase))
        {
            return "javascript";
        }

        if (query.Contains("java ", StringComparison.OrdinalIgnoreCase) || query.EndsWith(" java", StringComparison.OrdinalIgnoreCase))
        {
            return "java";
        }

        return null;
    }

    private static string AppendCodeSamples(
        string existingContent,
        IReadOnlyList<McpCodeSample> samples,
        int maxSamples,
        int maxCharsPerSample)
    {
        if (maxSamples <= 0 || maxCharsPerSample <= 0)
        {
            return existingContent;
        }

        var selected = samples
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .Take(maxSamples)
            .ToList();

        if (selected.Count == 0)
        {
            return existingContent;
        }

        var sb = new StringBuilder();
        sb.Append(existingContent);
        sb.Append("\n\n---\n\n");
        sb.Append("# Official code samples (Microsoft Learn MCP)\n\n");

        foreach (var sample in selected)
        {
            var title = string.IsNullOrWhiteSpace(sample.Title) ? "Code sample" : sample.Title;
            var language = string.IsNullOrWhiteSpace(sample.Language) ? string.Empty : sample.Language;
            var code = sample.Code ?? string.Empty;
            if (code.Length > maxCharsPerSample)
            {
                code = code[..maxCharsPerSample];
            }

            sb.Append("## ");
            sb.Append(title);
            sb.Append("\n\n");

            sb.Append("```");
            sb.Append(language);
            sb.Append("\n");
            sb.Append(code);
            sb.Append("\n```\n\n");

            if (!string.IsNullOrWhiteSpace(sample.Url))
            {
                sb.Append("Source: ");
                sb.Append(sample.Url);
                sb.Append("\n\n");
            }
        }

        return sb.ToString();
    }
}
