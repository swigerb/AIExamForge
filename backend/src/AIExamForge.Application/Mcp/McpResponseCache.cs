using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AIExamForge.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Application.Mcp;

/// <summary>
/// T175: MCP response caching per request per NFR-004.
/// Caches MCP retrieval results within a request to avoid duplicate calls for same topics.
/// </summary>
public interface IMcpResponseCache
{
    /// <summary>
    /// Gets a cached MCP response for the given topic.
    /// </summary>
    Task<McpContent?> GetAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an MCP response in the cache.
    /// </summary>
    Task SetAsync(string topic, McpContent content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    McpCacheStats GetStats();

    /// <summary>
    /// Clears the cache.
    /// </summary>
    void Clear();
}

/// <summary>
/// Statistics for MCP cache usage.
/// </summary>
public sealed record McpCacheStats(
    int TotalRequests,
    int CacheHits,
    int CacheMisses,
    int ItemCount)
{
    public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
}

/// <summary>
/// Request-scoped in-memory MCP cache implementation.
/// Per NFR-004: Cache MCP responses per request to optimize performance.
/// </summary>
public sealed class RequestScopedMcpCache : IMcpResponseCache, IDisposable
{
    private readonly ConcurrentDictionary<string, CachedMcpContent> _cache = new();
    private readonly TimeSpan _itemExpiration;
    private int _hits;
    private int _misses;
    private bool _disposed;

    /// <summary>
    /// Creates a new request-scoped MCP cache.
    /// </summary>
    /// <param name="itemExpiration">How long items stay valid. Default: 5 minutes.</param>
    public RequestScopedMcpCache(TimeSpan? itemExpiration = null)
    {
        _itemExpiration = itemExpiration ?? TimeSpan.FromMinutes(5);
    }

    public Task<McpContent?> GetAsync(string topic, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = NormalizeKey(topic);

        if (_cache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            Interlocked.Increment(ref _hits);
            return Task.FromResult<McpContent?>(cached.Content);
        }

        Interlocked.Increment(ref _misses);
        return Task.FromResult<McpContent?>(null);
    }

    public Task SetAsync(string topic, McpContent content, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = NormalizeKey(topic);
        var cached = new CachedMcpContent(content, DateTime.UtcNow.Add(_itemExpiration));

        _cache.AddOrUpdate(key, cached, (_, _) => cached);

        return Task.CompletedTask;
    }

    public McpCacheStats GetStats()
    {
        var hits = Volatile.Read(ref _hits);
        var misses = Volatile.Read(ref _misses);

        return new McpCacheStats(
            TotalRequests: hits + misses,
            CacheHits: hits,
            CacheMisses: misses,
            ItemCount: _cache.Count);
    }

    public void Clear()
    {
        _cache.Clear();
        _hits = 0;
        _misses = 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cache.Clear();
            _disposed = true;
        }
    }

    private static string NormalizeKey(string topic)
    {
        // Normalize topic for consistent cache keys
        var normalized = topic.Trim().ToLowerInvariant();

        // Use hash for very long topics
        if (normalized.Length > 256)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(hash);
        }

        return normalized;
    }

    private sealed record CachedMcpContent(McpContent Content, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}

/// <summary>
/// Decorator for MCP retrieval service that adds caching.
/// </summary>
public sealed class CachedMcpRetrievalService
{
    private readonly McpRetrievalService _innerService;
    private readonly IMcpResponseCache _cache;
    private readonly ILogger<CachedMcpRetrievalService> _logger;

    public CachedMcpRetrievalService(
        McpRetrievalService innerService,
        IMcpResponseCache cache,
        ILogger<CachedMcpRetrievalService> logger)
    {
        _innerService = innerService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves MCP content for a topic, using cache when available.
    /// </summary>
    public async Task<McpContent?> RetrieveForTopicAsync(
        string topic,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cached = await _cache.GetAsync(topic, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("MCP cache hit for topic: {Topic}", topic);
            return cached;
        }

        // Retrieve from MCP
        _logger.LogDebug("MCP cache miss for topic: {Topic}", topic);
        var content = await _innerService.RetrieveForTopicAsync(topic, cancellationToken);

        // Cache the result if content was retrieved
        if (content != null)
        {
            await _cache.SetAsync(topic, content, cancellationToken);
        }

        return content;
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public McpCacheStats GetCacheStats() => _cache.GetStats();
}
