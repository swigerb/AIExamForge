using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Api.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Maps exceptions to appropriate HTTP responses with problem details.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title, detail) = exception switch
        {
            ArgumentNullException argNull => (HttpStatusCode.BadRequest, "Bad Request", $"Required argument '{argNull.ParamName}' was null."),
            ArgumentException arg => (HttpStatusCode.BadRequest, "Bad Request", arg.Message),
            InvalidOperationException invalid => (HttpStatusCode.BadRequest, "Invalid Operation", invalid.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", "Access denied."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found", "The requested resource was not found."),
            NotSupportedException notSupported => (HttpStatusCode.BadRequest, "Not Supported", notSupported.Message),
            OperationCanceledException => (HttpStatusCode.RequestTimeout, "Request Timeout", "The operation was cancelled."),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;

        var correlationId = context.Items.TryGetValue("CorrelationId", out var id) ? id?.ToString() : context.TraceIdentifier;

        // RFC 7807 Problem Details format
        var problemDetails = new
        {
            type = $"https://httpstatuses.com/{(int)statusCode}",
            title,
            status = (int)statusCode,
            detail = statusCode == HttpStatusCode.InternalServerError 
                ? "An unexpected error occurred. Please try again later." 
                : detail,
            instance = context.Request.Path.Value,
            traceId = correlationId
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
    }
}

/// <summary>
/// Extension methods for ErrorHandlingMiddleware registration.
/// </summary>
public static class ErrorHandlingMiddlewareExtensions
{
    /// <summary>
    /// Adds global error handling middleware to the pipeline.
    /// Should be registered early in the pipeline to catch all exceptions.
    /// </summary>
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
