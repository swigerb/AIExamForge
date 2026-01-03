namespace AIExamForge.Domain.Models;

/// <summary>
/// Represents content retrieved from Microsoft Learn via MCP.
/// Per Constitution Principle VI: All question content must be sourced from MCP.
/// </summary>
public sealed record McpContent
{
    /// <summary>
    /// Unique identifier for this content retrieval.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The query that was used to retrieve this content.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// The raw content text from Microsoft Learn.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Source URLs from Microsoft Learn documentation.
    /// </summary>
    public required IReadOnlyList<string> SourceUrls { get; init; }

    /// <summary>
    /// When the content was retrieved.
    /// </summary>
    public required DateTimeOffset RetrievedAt { get; init; }

    /// <summary>
    /// How long the MCP query took in milliseconds.
    /// </summary>
    public long QueryTimeMs { get; init; }

    /// <summary>
    /// The outline node ID this content is associated with.
    /// </summary>
    public Guid? OutlineNodeId { get; init; }

    /// <summary>
    /// Creates a new McpContent instance.
    /// </summary>
    public static McpContent Create(
        string query,
        string content,
        IReadOnlyList<string> sourceUrls,
        long queryTimeMs,
        Guid? outlineNodeId = null)
    {
        return new McpContent
        {
            Id = Guid.NewGuid(),
            Query = query,
            Content = content,
            SourceUrls = sourceUrls,
            RetrievedAt = DateTimeOffset.UtcNow,
            QueryTimeMs = queryTimeMs,
            OutlineNodeId = outlineNodeId
        };
    }

    /// <summary>
    /// Gets a summary of the sources for citation purposes.
    /// </summary>
    public string GetSourceSummary()
    {
        if (SourceUrls.Count == 0)
        {
            return "No sources available";
        }

        return string.Join(", ", SourceUrls.Take(3).Select(GetShortUrl));
    }

    private static string GetShortUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.TrimEnd('/');
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[^1] : uri.Host;
        }
        catch
        {
            return url.Length > 50 ? url[..50] + "..." : url;
        }
    }
}

/// <summary>
/// Aggregated content from multiple MCP queries for a generation request.
/// </summary>
public sealed record McpContentCollection
{
    /// <summary>
    /// All content items retrieved for this generation.
    /// </summary>
    public required IReadOnlyList<McpContent> Items { get; init; }

    /// <summary>
    /// Total query time across all items.
    /// </summary>
    public long TotalQueryTimeMs => Items.Sum(i => i.QueryTimeMs);

    /// <summary>
    /// All unique source URLs across all items.
    /// </summary>
    public IReadOnlyList<string> AllSourceUrls => Items
        .SelectMany(i => i.SourceUrls)
        .Distinct()
        .ToList()
        .AsReadOnly();

    /// <summary>
    /// Gets the combined content for prompt construction.
    /// </summary>
    public string GetCombinedContent() => string.Join("\n\n---\n\n", 
        Items.Select(i => $"# {i.Query}\n\n{i.Content}"));

    /// <summary>
    /// Whether any content was retrieved.
    /// </summary>
    public bool HasContent => Items.Any(i => !string.IsNullOrWhiteSpace(i.Content));

    /// <summary>
    /// Creates an empty collection.
    /// </summary>
    public static McpContentCollection Empty => new() { Items = [] };
}
