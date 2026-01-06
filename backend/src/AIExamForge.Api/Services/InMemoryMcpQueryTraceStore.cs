using System.Collections.Concurrent;
using AIExamForge.Application.Mcp.Interfaces;

namespace AIExamForge.Api.Services;

public sealed class InMemoryMcpQueryTraceStore : IMcpQueryTraceStore
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<McpQueryTraceEntry>> _queries = new();

    public void RecordQuery(Guid generationId, Guid? outlineNodeId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var queue = _queries.GetOrAdd(generationId, _ => new ConcurrentQueue<McpQueryTraceEntry>());
        queue.Enqueue(new McpQueryTraceEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            OutlineNodeId = outlineNodeId,
            Query = query
        });

        // Keep the trace bounded to avoid unbounded memory growth.
        while (queue.Count > 200 && queue.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<McpQueryTraceEntry> GetQueries(Guid generationId)
    {
        return _queries.TryGetValue(generationId, out var queue)
            ? queue.ToArray()
            : Array.Empty<McpQueryTraceEntry>();
    }

    public void Clear(Guid generationId)
    {
        _queries.TryRemove(generationId, out _);
    }
}
