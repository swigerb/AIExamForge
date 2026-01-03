using System.Collections.Concurrent;
using System.Text.Json;
using AIExamForge.Application.Generation.DTOs;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Api.Streaming;

/// <summary>
/// Handles Server-Sent Events (SSE) for generation progress streaming.
/// Per openapi.yaml: Streams progress, question, complete, and error events.
/// </summary>
public sealed class GenerationProgressHandler
{
    private readonly ConcurrentDictionary<Guid, List<StreamWriter>> _connections = new();
    private readonly ILogger<GenerationProgressHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GenerationProgressHandler(ILogger<GenerationProgressHandler> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Registers a new SSE connection for a generation.
    /// </summary>
    public void AddConnection(Guid generationId, StreamWriter writer)
    {
        var writers = _connections.GetOrAdd(generationId, _ => []);
        lock (writers)
        {
            writers.Add(writer);
        }

        _logger.LogDebug("Added SSE connection for generation {GenerationId}. Total connections: {Count}",
            generationId, writers.Count);
    }

    /// <summary>
    /// Removes an SSE connection.
    /// </summary>
    public void RemoveConnection(Guid generationId, StreamWriter writer)
    {
        if (_connections.TryGetValue(generationId, out var writers))
        {
            lock (writers)
            {
                writers.Remove(writer);
                if (writers.Count == 0)
                {
                    _connections.TryRemove(generationId, out _);
                }
            }

            _logger.LogDebug("Removed SSE connection for generation {GenerationId}", generationId);
        }
    }

    /// <summary>
    /// Sends a progress update to all connected clients.
    /// </summary>
    public async Task SendProgressAsync(Guid generationId, int progress, string message)
    {
        var eventData = GenerationProgressEvent.ProgressUpdate(generationId, progress, message);
        await BroadcastEventAsync(generationId, "progress", eventData);
    }

    /// <summary>
    /// Sends a question generated event to all connected clients.
    /// </summary>
    public async Task SendQuestionAsync(Guid generationId, QuestionDto question, int progress)
    {
        var eventData = GenerationProgressEvent.QuestionGenerated(generationId, question, progress);
        await BroadcastEventAsync(generationId, "question", eventData);
    }

    /// <summary>
    /// Sends a complete event to all connected clients.
    /// </summary>
    public async Task SendCompleteAsync(Guid generationId)
    {
        var eventData = GenerationProgressEvent.Complete(generationId);
        await BroadcastEventAsync(generationId, "complete", eventData);
    }

    /// <summary>
    /// Sends an error event to all connected clients.
    /// </summary>
    public async Task SendErrorAsync(Guid generationId, string error)
    {
        var eventData = GenerationProgressEvent.Failed(generationId, error);
        await BroadcastEventAsync(generationId, "error", eventData);
    }

    /// <summary>
    /// Checks if there are any active connections for a generation.
    /// </summary>
    public bool HasConnections(Guid generationId) =>
        _connections.TryGetValue(generationId, out var writers) && writers.Count > 0;

    private async Task BroadcastEventAsync(Guid generationId, string eventType, GenerationProgressEvent eventData)
    {
        if (!_connections.TryGetValue(generationId, out var writers))
        {
            return;
        }

        var json = JsonSerializer.Serialize(eventData, _jsonOptions);
        var sseMessage = $"event: {eventType}\ndata: {json}\n\n";

        List<StreamWriter> failedWriters = [];

        lock (writers)
        {
            foreach (var writer in writers.ToArray()) // Copy to avoid collection modified during iteration
            {
                try
                {
                    // Write synchronously to maintain order, but use async flush
                    writer.Write(sseMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write to SSE connection for generation {GenerationId}", generationId);
                    failedWriters.Add(writer);
                }
            }
        }

        // Flush all writers asynchronously
        var flushTasks = new List<Task>();
        lock (writers)
        {
            foreach (var writer in writers.Where(w => !failedWriters.Contains(w)))
            {
                flushTasks.Add(FlushWriterAsync(writer, generationId, failedWriters));
            }
        }

        await Task.WhenAll(flushTasks);

        // Remove failed connections
        foreach (var failedWriter in failedWriters)
        {
            RemoveConnection(generationId, failedWriter);
        }
    }

    private async Task FlushWriterAsync(StreamWriter writer, Guid generationId, List<StreamWriter> failedWriters)
    {
        try
        {
            await writer.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flush SSE connection for generation {GenerationId}", generationId);
            lock (failedWriters)
            {
                failedWriters.Add(writer);
            }
        }
    }

    /// <summary>
    /// Cleans up all connections for a generation.
    /// </summary>
    public void CleanupGeneration(Guid generationId)
    {
        if (_connections.TryRemove(generationId, out var writers))
        {
            lock (writers)
            {
                foreach (var writer in writers)
                {
                    try
                    {
                        writer.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }

                writers.Clear();
            }

            _logger.LogDebug("Cleaned up all SSE connections for generation {GenerationId}", generationId);
        }
    }
}
