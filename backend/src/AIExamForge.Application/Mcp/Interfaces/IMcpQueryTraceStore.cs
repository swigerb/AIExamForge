using System.Collections.Concurrent;

namespace AIExamForge.Application.Mcp.Interfaces;

/// <summary>
/// Stores per-generation MCP query traces (primarily for Development diagnostics).
/// </summary>
public interface IMcpQueryTraceStore
{
    void RecordQuery(Guid generationId, Guid? outlineNodeId, string query);

    IReadOnlyList<McpQueryTraceEntry> GetQueries(Guid generationId);

    void Clear(Guid generationId);
}

public sealed record McpQueryTraceEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public Guid? OutlineNodeId { get; init; }
    public required string Query { get; init; }
}
