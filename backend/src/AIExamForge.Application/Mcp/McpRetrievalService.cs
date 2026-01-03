using AIExamForge.Application.Mcp.Interfaces;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

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

    // Cache content for 30 minutes (content doesn't change frequently)
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public McpRetrievalService(
        IMcpClient mcpClient,
        IMemoryCache cache,
        ILogger<McpRetrievalService> logger)
    {
        _mcpClient = mcpClient;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves content for a single outline node.
    /// </summary>
    public async Task<McpContent?> RetrieveForNodeAsync(
        OutlineNode node,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"mcp_node_{node.Id}";
        
        if (_cache.TryGetValue(cacheKey, out McpContent? cached))
        {
            _logger.LogDebug("Cache hit for node {NodeId}: {Title}", node.Id, node.Title);
            return cached;
        }

        var query = BuildQueryFromNode(node);
        _logger.LogInformation("Querying MCP for node {NodeId}: {Query}", node.Id, query);

        var result = await _mcpClient.QueryAsync(query, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("MCP query failed for node {NodeId}: {Error}", node.Id, result.ErrorMessage);
            return null;
        }

        if (result.ContentChunks.Count == 0)
        {
            _logger.LogWarning("No content found for node {NodeId}: {Query}", node.Id, query);
            return null;
        }

        var content = McpContent.Create(
            query,
            result.GetCombinedContent(),
            result.GetSourceUrls(),
            result.QueryTimeMs,
            node.Id);

        _cache.Set(cacheKey, content, CacheDuration);

        return content;
    }

    /// <summary>
    /// Retrieves content for multiple outline nodes.
    /// </summary>
    public async Task<McpContentCollection> RetrieveForNodesAsync(
        IEnumerable<OutlineNode> nodes,
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

            var content = await RetrieveForNodeAsync(node, cancellationToken);
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

        var content = McpContent.Create(
            topic,
            result.GetCombinedContent(),
            result.GetSourceUrls(),
            result.QueryTimeMs);

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
}
