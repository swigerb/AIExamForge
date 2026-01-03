namespace AIExamForge.Application.Mcp.Interfaces;

/// <summary>
/// Interface for Model Context Protocol client operations.
/// Used to query Microsoft Learn content for exam question generation.
/// Per Constitution Principle VI: MCP is the exclusive content source.
/// </summary>
public interface IMcpClient
{
    /// <summary>
    /// Queries the Microsoft Learn MCP server for content related to the topic.
    /// </summary>
    /// <param name="topic">The topic to search for (e.g., "Azure Virtual Networks").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query result containing content and source URLs.</returns>
    Task<McpQueryResult> QueryAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the Microsoft Learn MCP server with multiple keywords.
    /// </summary>
    /// <param name="topics">The topics to search for.</param>
    /// <param name="keywords">Additional keywords to refine the search.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query result containing content and source URLs.</returns>
    Task<McpQueryResult> QueryAsync(
        IEnumerable<string> topics,
        IEnumerable<string>? keywords = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the MCP server is available and responding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the server is available.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an MCP query containing content from Microsoft Learn.
/// </summary>
public sealed record McpQueryResult
{
    /// <summary>
    /// Whether the query was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The content chunks retrieved from Microsoft Learn.
    /// </summary>
    public IReadOnlyList<McpContentChunk> ContentChunks { get; init; } = [];

    /// <summary>
    /// Unique identifier for this query (for tracing/logging).
    /// </summary>
    public string QueryId { get; init; } = string.Empty;

    /// <summary>
    /// Error message if the query failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Total time taken for the query in milliseconds.
    /// </summary>
    public long QueryTimeMs { get; init; }

    /// <summary>
    /// Gets the combined content from all chunks.
    /// </summary>
    public string GetCombinedContent() => string.Join("\n\n", ContentChunks.Select(c => c.Content));

    /// <summary>
    /// Gets all unique source URLs from the chunks.
    /// </summary>
    public IReadOnlyList<string> GetSourceUrls() => ContentChunks
        .Select(c => c.SourceUrl)
        .Where(u => !string.IsNullOrEmpty(u))
        .Distinct()
        .ToList()
        .AsReadOnly();

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static McpQueryResult Successful(
        IReadOnlyList<McpContentChunk> chunks,
        string queryId,
        long queryTimeMs) => new()
    {
        Success = true,
        ContentChunks = chunks,
        QueryId = queryId,
        QueryTimeMs = queryTimeMs
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static McpQueryResult Failed(string errorMessage, string queryId) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        QueryId = queryId
    };

    /// <summary>
    /// Creates an empty result (no content found).
    /// </summary>
    public static McpQueryResult Empty(string queryId, long queryTimeMs) => new()
    {
        Success = true,
        ContentChunks = [],
        QueryId = queryId,
        QueryTimeMs = queryTimeMs
    };
}

/// <summary>
/// A chunk of content retrieved from Microsoft Learn via MCP.
/// </summary>
public sealed record McpContentChunk
{
    /// <summary>
    /// The text content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The source URL from Microsoft Learn.
    /// </summary>
    public required string SourceUrl { get; init; }

    /// <summary>
    /// The title of the source document.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Relevance score (0.0-1.0) if available.
    /// </summary>
    public double? RelevanceScore { get; init; }
}
