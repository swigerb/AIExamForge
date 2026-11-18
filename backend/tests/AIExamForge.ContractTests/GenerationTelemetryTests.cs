using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Globalization;
using AIExamForge.Api.Services;
using AIExamForge.Domain.ValueObjects;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIExamForge.ContractTests;

public sealed class GenerationTelemetryTests
{
    [Theory]
    [InlineData("GenerationCompleted", "en-US")]
    [InlineData("GenerationCompleted", "fr-FR")]
    [InlineData("McpRetrieval", "en-US")]
    [InlineData("McpRetrieval", "fr-FR")]
    [InlineData("QuestionSetExported", "en-US")]
    [InlineData("QuestionSetExported", "fr-FR")]
    public void TrackDurationEvent_PreservesGenerationAndInvariantDurationAlongsideAggregateMetrics(
        string eventName,
        string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            var generationId = Guid.NewGuid();
            var duration = TimeSpan.FromTicks(12345678);
            var events = new EventCaptureProvider();
            var measurements = new ConcurrentQueue<(string Name, double Value)>();
            using var listener = new MeterListener
            {
                InstrumentPublished = (instrument, meterListener) =>
                {
                    if (instrument.Meter.Name == "Microsoft.ApplicationInsights")
                    {
                        meterListener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
                measurements.Enqueue((instrument.Name, value)));
            listener.Start();

            // SDK 3's DI client emits ILogger events and Meter measurements without starting a host/exporter.
            var services = new ServiceCollection();
            services.AddApplicationInsightsTelemetry(options =>
                options.ConnectionString = $"InstrumentationKey={Guid.NewGuid()}");
            services.AddLogging(logging => logging.ClearProviders().AddProvider(events));
            using var provider = services.BuildServiceProvider();
            var telemetry = new ApplicationInsightsGenerationTelemetry(
                provider.GetRequiredService<TelemetryClient>(),
                NullLogger<ApplicationInsightsGenerationTelemetry>.Instance);

            Dictionary<string, double> expectedMetrics;
            switch (eventName)
            {
                case "GenerationCompleted":
                    telemetry.TrackGenerationCompleted(generationId, 7, 6, duration);
                    expectedMetrics = new()
                    {
                        ["GenerationDurationMs"] = duration.TotalMilliseconds,
                        ["QuestionsGenerated"] = 7,
                        ["QuestionsValidated"] = 6
                    };
                    break;
                case "McpRetrieval":
                    telemetry.TrackMcpRetrieval(generationId, "topic", true, duration, true, false);
                    expectedMetrics = new()
                    {
                        ["McpRetrievalDurationMs"] = duration.TotalMilliseconds,
                        ["McpCacheHitRate"] = 1,
                        ["McpEmptyResultRate"] = 0
                    };
                    break;
                case "QuestionSetExported":
                    telemetry.TrackExport(generationId, ExportFormat.Pdf, RenderMode.Study, true, duration);
                    expectedMetrics = new()
                    {
                        ["ExportDurationMs_Pdf"] = duration.TotalMilliseconds
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventName));
            }

            var capturedEvent = Assert.Single(events.Events);
            Assert.Equal(eventName, capturedEvent["microsoft.custom_event.name"]);
            Assert.Equal(generationId.ToString(), capturedEvent["GenerationId"]);
            var durationText = Assert.IsType<string>(capturedEvent["DurationMs"]);
            Assert.Equal("1234.5678", durationText);
            Assert.Equal(duration.TotalMilliseconds, double.Parse(durationText, CultureInfo.InvariantCulture));

            var actualMetrics = measurements.ToDictionary(measurement => measurement.Name, measurement => measurement.Value);
            Assert.Equal(expectedMetrics.Count, actualMetrics.Count);
            foreach (var (name, value) in expectedMetrics)
            {
                Assert.Equal(value, actualMetrics[name]);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private sealed class EventCaptureProvider : ILoggerProvider
    {
        public ConcurrentQueue<Dictionary<string, object?>> Events { get; } = new();

        public ILogger CreateLogger(string categoryName) => new EventCaptureLogger(Events);

        public void Dispose() { }
    }

    private sealed class EventCaptureLogger(
        ConcurrentQueue<Dictionary<string, object?>> events) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> attributes)
            {
                var properties = attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
                if (properties.ContainsKey("microsoft.custom_event.name"))
                {
                    events.Enqueue(properties);
                }
            }
        }
    }
}
