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
    private readonly ConcurrentDictionary<Guid, string> _lastEventByGeneration = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _sendGates = new();
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
    /// If this generation has already produced an event, replay it for a newly-joined connection.
    /// This prevents clients from getting stuck if they subscribe after an early failure/completion.
    /// </summary>
    public async Task<bool> TryReplayLastEventAsync(Guid generationId, StreamWriter writer, CancellationToken cancellationToken = default)
    {
        if (!_lastEventByGeneration.TryGetValue(generationId, out var lastEvent))
        {
            return false;
        }

        var gate = _sendGates.GetOrAdd(generationId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            await writer.WriteAsync(lastEvent);
            await writer.FlushAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to replay last SSE event for generation {GenerationId}", generationId);
            return false;
        }
        finally
        {
            gate.Release();
        }
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
        var json = JsonSerializer.Serialize(eventData, _jsonOptions);
        var sseMessage = $"event: {eventType}\ndata: {json}\n\n";

        // Always remember the last event, even if no one is connected yet.
        _lastEventByGeneration[generationId] = sseMessage;

        if (!_connections.TryGetValue(generationId, out var writers))
        {
            return;
        }

        var gate = _sendGates.GetOrAdd(generationId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();

        List<StreamWriter> failedWriters = [];

        try
        {
            StreamWriter[] snapshot;
            lock (writers)
            {
                snapshot = writers.ToArray();
            }

            foreach (var writer in snapshot)
            {
                try
                {
                    await writer.WriteAsync(sseMessage);
                    await writer.FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write to SSE connection for generation {GenerationId}", generationId);
                    failedWriters.Add(writer);
                }
            }
        }
        finally
        {
            gate.Release();
        }

        // Remove failed connections
        foreach (var failedWriter in failedWriters)
        {
            RemoveConnection(generationId, failedWriter);
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

        _lastEventByGeneration.TryRemove(generationId, out _);

        if (_sendGates.TryRemove(generationId, out var gate))
        {
            gate.Dispose();
        }
    }
}
