using Microsoft.Extensions.Logging;

namespace AIExamForge.Api.Middleware;

/// <summary>
/// Middleware that ensures every request has a correlation ID for distributed tracing.
/// - Reads existing X-Correlation-ID header if present
/// - Generates new GUID if not present
/// - Adds correlation ID to response headers
/// - Makes correlation ID available via HttpContext.Items
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string CorrelationIdKey = "CorrelationId";
    
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get correlation ID from request header
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        
        // Generate new one if not provided
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("D");
        }

        // Store in HttpContext.Items for access by other middleware and endpoints
        context.Items[CorrelationIdKey] = correlationId;
        
        // Add to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers[CorrelationIdHeader] = correlationId;
            }
            return Task.CompletedTask;
        });

        // Add correlation ID to log scope
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdKey] = correlationId
        }))
        {
            _logger.LogDebug("Processing request with CorrelationId: {CorrelationId}", correlationId);
            await _next(context);
        }
    }
}

/// <summary>
/// Extension methods for CorrelationIdMiddleware registration.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds correlation ID middleware to the pipeline.
    /// Should be registered early in the pipeline before other middleware that may need the correlation ID.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Gets the correlation ID from the current HTTP context.
    /// </summary>
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items.TryGetValue("CorrelationId", out var id) ? id?.ToString() : null;
    }
}
