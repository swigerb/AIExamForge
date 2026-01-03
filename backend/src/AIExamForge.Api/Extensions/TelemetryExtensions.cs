using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AIExamForge.Api.Extensions;

/// <summary>
/// T159: Azure Application Insights telemetry configuration.
/// Per FR-025, FR-027, FR-028: Structured logging with correlation IDs and telemetry events.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Adds Application Insights telemetry with custom configuration.
    /// </summary>
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        string? connectionString = null)
    {
        // Add Application Insights with connection string from configuration or environment
        services.AddApplicationInsightsTelemetry(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.ConnectionString = connectionString;
            }

            // Enable adaptive sampling for cost control
            options.EnableAdaptiveSampling = true;

            // Enable dependency tracking for external calls (LLM, MCP, etc.)
            options.EnableDependencyTrackingTelemetryModule = true;

            // Enable request tracking
            options.EnableRequestTrackingTelemetryModule = true;
        });

        // Add custom telemetry initializers
        services.AddSingleton<ITelemetryInitializer, OperationCorrelationInitializer>();
        services.AddSingleton<ITelemetryInitializer, CloudRoleNameInitializer>();

        return services;
    }

    /// <summary>
    /// Configures the telemetry middleware pipeline.
    /// </summary>
    public static IApplicationBuilder UseTelemetry(this IApplicationBuilder app)
    {
        // Add correlation ID middleware early in pipeline
        app.UseMiddleware<CorrelationIdMiddleware>();

        return app;
    }
}

/// <summary>
/// T160: Telemetry initializer that adds operation correlation IDs per FR-025, FR-027.
/// </summary>
public sealed class OperationCorrelationInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OperationCorrelationInitializer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize(Microsoft.ApplicationInsights.Channel.ITelemetry telemetry)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        // Get or create correlation ID
        if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId) &&
            correlationId is string correlationIdStr)
        {
            telemetry.Context.Operation.Id = correlationIdStr;
        }

        // Add user context if available
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            telemetry.Context.User.AuthenticatedUserId =
                httpContext.User.Identity.Name ?? "anonymous";
        }

        // Add generation context if available
        if (httpContext.Items.TryGetValue("GenerationId", out var generationId) &&
            generationId is Guid generationGuid)
        {
            if (telemetry is Microsoft.ApplicationInsights.DataContracts.ISupportProperties supportProperties)
            {
                supportProperties.Properties["GenerationId"] = generationGuid.ToString();
            }
        }
    }
}

/// <summary>
/// Telemetry initializer that sets the cloud role name for distributed tracing.
/// </summary>
public sealed class CloudRoleNameInitializer : ITelemetryInitializer
{
    private const string CloudRoleName = "AIExamForge-Api";

    public void Initialize(Microsoft.ApplicationInsights.Channel.ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = CloudRoleName;
        telemetry.Context.Cloud.RoleInstance = Environment.MachineName;
    }
}

/// <summary>
/// Middleware that ensures correlation IDs are propagated through the request pipeline.
/// Per FR-025: All operations must have correlation IDs for tracing.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get correlation ID from header or generate new one
        var correlationId = GetOrCreateCorrelationId(context);

        // Store in items for later access
        context.Items["CorrelationId"] = correlationId;

        // Add to response headers for client correlation
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);
            return Task.CompletedTask;
        });

        // Add correlation ID to log scope
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingId) &&
            !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}
