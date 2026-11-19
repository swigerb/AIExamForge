using System.Globalization;
using AIExamForge.Domain.ValueObjects;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace AIExamForge.Api.Services;

/// <summary>
/// T161: Telemetry service for tracking generation events per FR-028.
/// Tracks generation success/failure, MCP metrics, and performance data.
/// </summary>
public interface IGenerationTelemetry
{
    /// <summary>
    /// Tracks the start of a generation operation.
    /// </summary>
    void TrackGenerationStarted(Guid generationId, string examName, int questionCount);

    /// <summary>
    /// Tracks successful completion of generation.
    /// </summary>
    void TrackGenerationCompleted(Guid generationId, int questionsGenerated, int questionsValidated, TimeSpan duration);

    /// <summary>
    /// Tracks generation failure.
    /// </summary>
    void TrackGenerationFailed(Guid generationId, string reason, string? errorMessage = null);

    /// <summary>
    /// Tracks individual question generation.
    /// </summary>
    void TrackQuestionGenerated(Guid generationId, string itemTypeCode, bool passedValidation);

    /// <summary>
    /// Tracks MCP retrieval operation per FR-033.
    /// </summary>
    void TrackMcpRetrieval(Guid generationId, string topic, bool success, TimeSpan duration, bool cacheHit, bool emptyResult);

    /// <summary>
    /// Tracks export operation.
    /// </summary>
    void TrackExport(Guid generationId, ExportFormat format, RenderMode mode, bool success, TimeSpan duration);
}

/// <summary>
/// Application Insights implementation of generation telemetry.
/// </summary>
public sealed class ApplicationInsightsGenerationTelemetry : IGenerationTelemetry
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<ApplicationInsightsGenerationTelemetry> _logger;

    public ApplicationInsightsGenerationTelemetry(
        TelemetryClient telemetryClient,
        ILogger<ApplicationInsightsGenerationTelemetry> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void TrackGenerationStarted(Guid generationId, string examName, int questionCount)
    {
        var eventTelemetry = new EventTelemetry("GenerationStarted")
        {
            Properties =
            {
                ["GenerationId"] = generationId.ToString(),
                ["ExamName"] = examName,
                ["RequestedQuestionCount"] = questionCount.ToString()
            }
        };

        _telemetryClient.TrackEvent(eventTelemetry);

        _logger.LogInformation(
            "Generation {GenerationId} started for exam {ExamName} with {QuestionCount} questions requested",
            generationId, examName, questionCount);
    }

    public void TrackGenerationCompleted(Guid generationId, int questionsGenerated, int questionsValidated, TimeSpan duration)
    {
        var eventTelemetry = new EventTelemetry("GenerationCompleted")
        {
            Properties =
            {
                ["GenerationId"] = generationId.ToString(),
                ["DurationMs"] = duration.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture),
                ["QuestionsGenerated"] = questionsGenerated.ToString(),
                ["QuestionsValidated"] = questionsValidated.ToString(),
                ["ValidationSuccessRate"] = questionsGenerated > 0
                    ? ((double)questionsValidated / questionsGenerated * 100).ToString("F1")
                    : "0"
            }
        };

        _telemetryClient.TrackEvent(eventTelemetry);

        // Also track as a metric for aggregation
        _telemetryClient.TrackMetric("GenerationDurationMs", duration.TotalMilliseconds);
        _telemetryClient.TrackMetric("QuestionsGenerated", questionsGenerated);
        _telemetryClient.TrackMetric("QuestionsValidated", questionsValidated);

        _logger.LogInformation(
            "Generation {GenerationId} completed: {Generated} generated, {Validated} validated in {Duration}ms",
            generationId, questionsGenerated, questionsValidated, duration.TotalMilliseconds);
    }

    public void TrackGenerationFailed(Guid generationId, string reason, string? errorMessage = null)
    {
        var eventTelemetry = new EventTelemetry("GenerationFailed")
        {
            Properties =
            {
                ["GenerationId"] = generationId.ToString(),
                ["FailureReason"] = reason
            }
        };

        if (!string.IsNullOrEmpty(errorMessage))
        {
            eventTelemetry.Properties["ErrorMessage"] = errorMessage;
        }

        _telemetryClient.TrackEvent(eventTelemetry);

        _logger.LogError(
            "Generation {GenerationId} failed: {Reason}. Error: {ErrorMessage}",
            generationId, reason, errorMessage ?? "N/A");
    }

    public void TrackQuestionGenerated(Guid generationId, string itemTypeCode, bool passedValidation)
    {
        var eventTelemetry = new EventTelemetry("QuestionGenerated")
        {
            Properties =
            {
                ["GenerationId"] = generationId.ToString(),
                ["ItemTypeCode"] = itemTypeCode,
                ["PassedValidation"] = passedValidation.ToString()
            }
        };

        _telemetryClient.TrackEvent(eventTelemetry);

        // Track validation success rate by item type
        _telemetryClient.TrackMetric(
            $"ValidationSuccess_{itemTypeCode}",
            passedValidation ? 1.0 : 0.0);
    }

    public void TrackMcpRetrieval(
        Guid generationId,
        string topic,
        bool success,
        TimeSpan duration,
        bool cacheHit,
        bool emptyResult)
    {
        var eventTelemetry = new EventTelemetry("McpRetrieval")
        {
            Properties =
            {
                ["GenerationId"] = generationId.ToString(),
                ["DurationMs"] = duration.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture),
                ["Topic"] = topic,
                ["Success"] = success.ToString(),
                ["CacheHit"] = cacheHit.ToString(),
                ["EmptyResult"] = emptyResult.ToString()
            }
        };

        _telemetryClient.TrackEvent(eventTelemetry);

        // Track metrics for MCP performance monitoring per FR-033
        _telemetryClient.TrackMetric("McpRetrievalDurationMs", duration.TotalMilliseconds);
        _telemetryClient.TrackMetric("McpCacheHitRate", cacheHit ? 1.0 : 0.0);
        _telemetryClient.TrackMetric("McpEmptyResultRate", emptyResult ? 1.0 : 0.0);

        if (!success)
        {
            _logger.LogWarning(
                "MCP retrieval failed for generation {GenerationId}, topic: {Topic}",
                generationId, topic);
        }
    }

    public void TrackExport(
        Guid generationId,
        ExportFormat format,
        RenderMode mode,
        bool success,
        TimeSpan duration)
    {
        var eventTelemetry = new EventTelemetry("QuestionSetExported")
        {
            Properties =
            {
                ["GenerationId"] = generationId.ToString(),
                ["DurationMs"] = duration.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture),
                ["Format"] = format.ToString(),
                ["RenderMode"] = mode.ToString(),
                ["Success"] = success.ToString()
            }
        };

        _telemetryClient.TrackEvent(eventTelemetry);

        _telemetryClient.TrackMetric($"ExportDurationMs_{format}", duration.TotalMilliseconds);

        _logger.LogInformation(
            "Export for generation {GenerationId}: format={Format}, mode={Mode}, success={Success}, duration={Duration}ms",
            generationId, format, mode, success, duration.TotalMilliseconds);
    }
}

/// <summary>
/// No-op implementation for when Application Insights is not configured.
/// </summary>
public sealed class NullGenerationTelemetry : IGenerationTelemetry
{
    public void TrackGenerationStarted(Guid generationId, string examName, int questionCount) { }
    public void TrackGenerationCompleted(Guid generationId, int questionsGenerated, int questionsValidated, TimeSpan duration) { }
    public void TrackGenerationFailed(Guid generationId, string reason, string? errorMessage = null) { }
    public void TrackQuestionGenerated(Guid generationId, string itemTypeCode, bool passedValidation) { }
    public void TrackMcpRetrieval(Guid generationId, string topic, bool success, TimeSpan duration, bool cacheHit, bool emptyResult) { }
    public void TrackExport(Guid generationId, ExportFormat format, RenderMode mode, bool success, TimeSpan duration) { }
}
