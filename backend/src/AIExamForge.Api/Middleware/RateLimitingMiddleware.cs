using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Api.Middleware;

/// <summary>
/// Rate limiting middleware enforcing:
/// - 10 requests per minute per user
/// - 100 questions generated per day per user
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    
    // In-memory rate limiting (production should use Redis/distributed cache)
    private static readonly ConcurrentDictionary<string, RateLimitState> _requestLimits = new();
    private static readonly ConcurrentDictionary<string, DailyQuestionState> _questionLimits = new();
    
    private const int MaxRequestsPerMinute = 10;
    private const int MaxQuestionsPerDay = 100;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = GetUserId(context);
        
        // Check request rate limit
        if (!await CheckRequestRateLimitAsync(context, userId))
        {
            return;
        }

        // For question generation endpoints, check daily limit
        if (IsQuestionGenerationEndpoint(context))
        {
            if (!await CheckDailyQuestionLimitAsync(context, userId))
            {
                return;
            }
        }

        await _next(context);

        // If question generation succeeded, increment counter
        if (IsQuestionGenerationEndpoint(context) && context.Response.StatusCode is >= 200 and < 300)
        {
            IncrementQuestionCount(userId, GetGeneratedQuestionCount(context));
        }
    }

    private static string GetUserId(HttpContext context)
    {
        // Use authenticated user ID, or fall back to IP for anonymous
        var userId = context.User?.Identity?.Name;
        if (string.IsNullOrEmpty(userId))
        {
            userId = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        }
        return userId;
    }

    private async Task<bool> CheckRequestRateLimitAsync(HttpContext context, string userId)
    {
        var now = DateTime.UtcNow;
        var state = _requestLimits.GetOrAdd(userId, _ => new RateLimitState());

        lock (state)
        {
            // Reset if window has passed
            if (now - state.WindowStart > TimeSpan.FromMinutes(1))
            {
                state.WindowStart = now;
                state.RequestCount = 0;
            }

            if (state.RequestCount >= MaxRequestsPerMinute)
            {
                var retryAfter = (int)Math.Ceiling((state.WindowStart.AddMinutes(1) - now).TotalSeconds);
                _logger.LogWarning("Rate limit exceeded for user {UserId}. Requests: {Count}/{Max}", 
                    userId, state.RequestCount, MaxRequestsPerMinute);
                
                context.Response.Headers["Retry-After"] = retryAfter.ToString();
                context.Response.Headers["X-RateLimit-Limit"] = MaxRequestsPerMinute.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = "0";
                context.Response.Headers["X-RateLimit-Reset"] = state.WindowStart.AddMinutes(1).ToString("o");
                
                return ReturnTooManyRequests(context, "Request rate limit exceeded", retryAfter).GetAwaiter().GetResult();
            }

            state.RequestCount++;
            
            context.Response.Headers["X-RateLimit-Limit"] = MaxRequestsPerMinute.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = (MaxRequestsPerMinute - state.RequestCount).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = state.WindowStart.AddMinutes(1).ToString("o");
        }

        return true;
    }

    private async Task<bool> CheckDailyQuestionLimitAsync(HttpContext context, string userId)
    {
        var today = DateTime.UtcNow.Date;
        var state = _questionLimits.GetOrAdd(userId, _ => new DailyQuestionState());

        bool limitExceeded;
        int retryAfter = 0;

        lock (state)
        {
            // Reset if day has changed
            if (state.Date != today)
            {
                state.Date = today;
                state.QuestionCount = 0;
            }

            limitExceeded = state.QuestionCount >= MaxQuestionsPerDay;

            if (limitExceeded)
            {
                _logger.LogWarning("Daily question limit exceeded for user {UserId}. Count: {Count}/{Max}", 
                    userId, state.QuestionCount, MaxQuestionsPerDay);
                
                var resetTime = today.AddDays(1);
                retryAfter = (int)(resetTime - DateTime.UtcNow).TotalSeconds;
                
                context.Response.Headers["X-DailyQuota-Limit"] = MaxQuestionsPerDay.ToString();
                context.Response.Headers["X-DailyQuota-Remaining"] = "0";
                context.Response.Headers["X-DailyQuota-Reset"] = resetTime.ToString("o");
            }
            else
            {
                context.Response.Headers["X-DailyQuota-Limit"] = MaxQuestionsPerDay.ToString();
                context.Response.Headers["X-DailyQuota-Remaining"] = (MaxQuestionsPerDay - state.QuestionCount).ToString();
                context.Response.Headers["X-DailyQuota-Reset"] = today.AddDays(1).ToString("o");
            }
        }

        if (limitExceeded)
        {
            return await ReturnTooManyRequests(context, "Daily question generation limit exceeded", retryAfter);
        }

        return true;
    }

    private static void IncrementQuestionCount(string userId, int count)
    {
        if (count <= 0)
        {
            return;
        }
        
        var today = DateTime.UtcNow.Date;
        var state = _questionLimits.GetOrAdd(userId, _ => new DailyQuestionState());

        lock (state)
        {
            if (state.Date != today)
            {
                state.Date = today;
                state.QuestionCount = 0;
            }
            state.QuestionCount += count;
        }
    }

    private static bool IsQuestionGenerationEndpoint(HttpContext context)
    {
        // Match POST /api/generation/* endpoints
        return context.Request.Method == HttpMethods.Post &&
               context.Request.Path.StartsWithSegments("/api/generation");
    }

    private static int GetGeneratedQuestionCount(HttpContext context)
    {
        // Try to get the count from context items (set by generation endpoint)
        if (context.Items.TryGetValue("GeneratedQuestionCount", out var count) && count is int questionCount)
        {
            return questionCount;
        }
        return 0;
    }

    private static async Task<bool> ReturnTooManyRequests(HttpContext context, string message, int retryAfterSeconds)
    {
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new
        {
            type = "https://httpstatuses.com/429",
            title = "Too Many Requests",
            status = 429,
            detail = message,
            instance = context.Request.Path.Value,
            retryAfterSeconds
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
        return false;
    }

    private sealed class RateLimitState
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public int RequestCount { get; set; }
    }

    private sealed class DailyQuestionState
    {
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;
        public int QuestionCount { get; set; }
    }
}

/// <summary>
/// Extension methods for RateLimitingMiddleware registration.
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Adds rate limiting middleware to the pipeline.
    /// Enforces 10 requests/minute and 100 questions/day per user.
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
