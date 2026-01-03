using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace AIExamForge.Api.Middleware;

/// <summary>
/// T163: Input validation middleware for all endpoints per FR-023.
/// Validates request payloads and query parameters before processing.
/// </summary>
public sealed class InputValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputValidationMiddleware> _logger;
    private readonly InputValidationOptions _options;

    public InputValidationMiddleware(
        RequestDelegate next,
        ILogger<InputValidationMiddleware> logger,
        InputValidationOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new InputValidationOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation for certain paths
        if (ShouldSkipValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Validate content length
        if (!ValidateContentLength(context, out var error))
        {
            await WriteValidationError(context, error);
            return;
        }

        // Validate content type for POST/PUT/PATCH
        if (HasRequestBody(context.Request.Method) &&
            !ValidateContentType(context, out error))
        {
            await WriteValidationError(context, error);
            return;
        }

        // Validate query parameters
        if (!ValidateQueryParameters(context, out error))
        {
            await WriteValidationError(context, error);
            return;
        }

        // Validate path parameters for injection attempts
        if (!ValidatePathParameters(context, out error))
        {
            await WriteValidationError(context, error);
            return;
        }

        await _next(context);
    }

    private bool ShouldSkipValidation(PathString path)
    {
        // Skip health check and OpenAPI endpoints
        return path.StartsWithSegments("/health") ||
               path.StartsWithSegments("/openapi") ||
               path.StartsWithSegments("/swagger");
    }

    private static bool HasRequestBody(string method)
    {
        return method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);
    }

    private bool ValidateContentLength(HttpContext context, out string? error)
    {
        error = null;

        var contentLength = context.Request.ContentLength;
        if (contentLength.HasValue && contentLength.Value > _options.MaxRequestBodySize)
        {
            _logger.LogWarning(
                "Request body too large: {ContentLength} bytes exceeds max {MaxSize} bytes",
                contentLength.Value, _options.MaxRequestBodySize);

            error = $"Request body too large. Maximum allowed size is {_options.MaxRequestBodySize / (1024 * 1024)}MB.";
            return false;
        }

        return true;
    }

    private bool ValidateContentType(HttpContext context, out string? error)
    {
        error = null;

        var contentType = context.Request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            // No content type with empty body is OK
            if (context.Request.ContentLength == 0)
            {
                return true;
            }

            error = "Content-Type header is required for requests with body.";
            return false;
        }

        // Allow JSON and multipart/form-data (for file uploads)
        var isValidContentType = contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
                                  contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
                                  contentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase);

        if (!isValidContentType)
        {
            _logger.LogWarning("Invalid content type: {ContentType}", contentType);
            error = $"Unsupported Content-Type: {contentType}. Use application/json or multipart/form-data.";
            return false;
        }

        return true;
    }

    private bool ValidateQueryParameters(HttpContext context, out string? error)
    {
        error = null;

        foreach (var (key, values) in context.Request.Query)
        {
            // Check parameter name length
            if (key.Length > _options.MaxQueryParameterNameLength)
            {
                error = $"Query parameter name '{key}' is too long.";
                return false;
            }

            // Check parameter value length
            foreach (var value in values)
            {
                if (value?.Length > _options.MaxQueryParameterValueLength)
                {
                    error = $"Query parameter value for '{key}' is too long.";
                    return false;
                }

                // Check for potential injection patterns
                if (ContainsDangerousPatterns(value))
                {
                    _logger.LogWarning(
                        "Potential injection attempt in query parameter {Parameter}: {Value}",
                        key, value);
                    error = $"Invalid characters in query parameter '{key}'.";
                    return false;
                }
            }
        }

        return true;
    }

    private bool ValidatePathParameters(HttpContext context, out string? error)
    {
        error = null;

        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return true;
        }

        // Check for path traversal attempts
        if (path.Contains("..") || path.Contains("//"))
        {
            _logger.LogWarning("Potential path traversal attempt: {Path}", path);
            error = "Invalid request path.";
            return false;
        }

        return true;
    }

    private static bool ContainsDangerousPatterns(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Check for common injection patterns
        var dangerousPatterns = new[]
        {
            "<script", "javascript:", "onerror=", "onclick=",  // XSS
            "'; DROP", "1=1", "OR 1=1", "UNION SELECT",        // SQL injection
            "{{", "}}", "${", "$(", "#{",                       // Template injection
        };

        var lowerValue = value.ToLowerInvariant();
        return dangerousPatterns.Any(pattern => lowerValue.Contains(pattern.ToLowerInvariant()));
    }

    private static async Task WriteValidationError(HttpContext context, string? errorMessage)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            title = "Bad Request",
            status = 400,
            detail = errorMessage ?? "Request validation failed.",
            instance = context.Request.Path.Value
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}

/// <summary>
/// Options for input validation middleware.
/// </summary>
public sealed class InputValidationOptions
{
    /// <summary>
    /// Maximum request body size in bytes. Default: 10MB.
    /// </summary>
    public long MaxRequestBodySize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum query parameter name length. Default: 100.
    /// </summary>
    public int MaxQueryParameterNameLength { get; set; } = 100;

    /// <summary>
    /// Maximum query parameter value length. Default: 2000.
    /// </summary>
    public int MaxQueryParameterValueLength { get; set; } = 2000;
}

/// <summary>
/// Extension methods for registering input validation middleware.
/// </summary>
public static class InputValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseInputValidation(
        this IApplicationBuilder app,
        InputValidationOptions? options = null)
    {
        return app.UseMiddleware<InputValidationMiddleware>(options ?? new InputValidationOptions());
    }

    public static IApplicationBuilder UseInputValidation(
        this IApplicationBuilder app,
        Action<InputValidationOptions> configure)
    {
        var options = new InputValidationOptions();
        configure(options);
        return app.UseMiddleware<InputValidationMiddleware>(options);
    }
}
