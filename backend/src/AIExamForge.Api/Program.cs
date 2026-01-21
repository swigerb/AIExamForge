using AIExamForge.Api.Endpoints;
using AIExamForge.Api.Extensions;
using AIExamForge.Api.Services;
using AIExamForge.Api.Streaming;
using AIExamForge.Application;
using AIExamForge.Application.Mcp;
using AIExamForge.Application.Mcp.Interfaces;
using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Domain.Models;
using AIExamForge.Infrastructure;
using AIExamForge.Infrastructure.Data;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.Identity.Web;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry (Development only)
// Exports traces/logs/metrics via OTLP so you can view them in the Aspire Dashboard.
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue("OpenTelemetry:Enabled", true))
{
    var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "AIExamForge.Api";

    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"]
        ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
        ?? "http://localhost:18889"; // Aspire Dashboard OTLP gRPC default

    // Aspire dashboard commonly uses OTLP/gRPC over http:// (h2c) in local dev.
    // .NET requires enabling unencrypted HTTP/2 support for this scenario.
    if (otlpEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(serviceName: serviceName, serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        });

    builder.Logging.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(resourceBuilder);
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
        options.ParseStateValues = true;

        options.AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(otlpEndpoint);
            o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        });
    });

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: serviceName, serviceVersion: "1.0.0"))
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(resourceBuilder)
                .SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.RecordException = true;
                })
                .AddHttpClientInstrumentation(o =>
                {
                    o.RecordException = true;
                })
                .AddEntityFrameworkCoreInstrumentation(o =>
                {
                    o.SetDbStatementForText = true;
                    o.SetDbStatementForStoredProcedure = true;
                })
                .AddSource("AIExamForge.Mcp")
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
        })
        .WithMetrics(metrics =>
        {
            metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
        });
}

// Add Azure Key Vault as a configuration source when configured.
// This allows secrets like "AzureOpenAI:ApiKey" to be supplied via Key Vault.
builder.Configuration.AddAzureKeyVault();

// Development diagnostics logging (prompt/MCP/LLM request/response bodies).
builder.Services.Configure<DiagnosticsLoggingOptions>(
    builder.Configuration.GetSection(DiagnosticsLoggingOptions.SectionName));

// Add Microsoft Identity (Entra ID) authentication
builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");

// Add Authorization
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAuthenticatedUser", policy => policy.RequireAuthenticatedUser());

// T177: Configure database settings from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var databaseSettings = builder.Configuration
    .GetSection(DatabaseSettings.SectionName)
    .Get<DatabaseSettings>() ?? new DatabaseSettings();

// Add Infrastructure services (DbContext with connection pooling, Repositories, etc.)
builder.Services.AddInfrastructure(builder.Configuration, connectionString, databaseSettings);

// Add Application services (Services, Validators, etc.)
builder.Services.AddApplication();

// MCP retrieval behavior (e.g., whether to include code samples)
builder.Services.Configure<McpRetrievalOptions>(
    builder.Configuration.GetSection(McpRetrievalOptions.SectionName));

// In-memory caching (used by MCP retrieval/caching services)
builder.Services.AddMemoryCache();

// Add OpenAPI documentation
builder.Services.AddOpenApi();

// Minimal API JSON options
// Frontend sends enums as strings (e.g., "Study"/"Exam"); enable string-enum deserialization.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Prefer tolerant, per-type converters for specific DTO enums.
    options.SerializerOptions.Converters.Add(new RenderModeJsonConverter());
    options.SerializerOptions.Converters.Add(new ToneProfileJsonConverter());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        // In development the SPA may run on varying ports (Vite, Aspire, etc.).
        // Allow loopback origins broadly to keep fetch + SSE working.
        if (builder.Environment.IsDevelopment())
        {
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrWhiteSpace(origin))
                    {
                        return false;
                    }

                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        return false;
                    }

                    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase);
                })
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();

            return;
        }

        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add health checks
builder.Services.AddHealthChecks();

// Add ProblemDetails for error handling
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (!builder.Environment.IsDevelopment())
        {
            return;
        }

        if (context.Exception is not Exception ex)
        {
            return;
        }

        // Put the full exception (including stack trace) into `detail` so the frontend can surface it.
        // This is Development-only to avoid leaking sensitive details.
        context.ProblemDetails.Detail = ex.ToString();
    };
});

// Add SSE handler for streaming
builder.Services.AddSingleton<GenerationProgressHandler>();

// Background queue + worker for generation
builder.Services.AddSingleton<IGenerationBackgroundQueue, GenerationBackgroundQueue>();
builder.Services.AddHostedService<GenerationBackgroundWorker>();

// Development diagnostics: capture MCP query strings per generation.
builder.Services.AddSingleton<IMcpQueryTraceStore, InMemoryMcpQueryTraceStore>();

var app = builder.Build();

// Development diagnostics: log inbound requests immediately so it's obvious when the UI is calling the API.
// This is especially helpful for long-lived SSE requests which may not complete for a while.
if (app.Environment.IsDevelopment())
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RequestTrace");
    app.Use((context, next) =>
    {
        logger.LogInformation("HTTP {Method} {Path}", context.Request.Method, context.Request.Path);
        return next();
    });
}

// Local development convenience: ensure the LocalDB database exists.
// The repo currently does not ship EF Core migrations, so we use EnsureCreated() in Development.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AIExamForgeDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowFrontend");

app.UseAuthentication();

// Local development convenience: allow calling protected endpoints without Entra ID.
// This injects a stable dev user identity when requests are otherwise anonymous.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Auth:InjectLocalDevUser", true))
{
    app.Use((context, next) =>
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "local-dev")],
                authenticationType: "LocalDev");
            context.User = new ClaimsPrincipal(identity);
        }

        return next();
    });
}

app.UseAuthorization();

// Friendly root endpoint for local debugging / discovery
app.MapGet("/", (HttpContext ctx) => Results.Ok(new
{
    Message = "AIExamForge API is running",
    Urls = new
    {
        Version = $"{ctx.Request.Scheme}://{ctx.Request.Host}/api/version",
        Health = $"{ctx.Request.Scheme}://{ctx.Request.Host}/health",
        OpenApi = app.Environment.IsDevelopment() ? $"{ctx.Request.Scheme}://{ctx.Request.Host}/openapi/v1.json" : null
    }
})).AllowAnonymous();

// Health check endpoint
app.MapHealthChecks("/health").WithOpenApi();

// Map API endpoints
app.MapApiEndpoints();

app.Run();

public partial class Program;

/// <summary>
/// Extension methods for mapping API endpoints.
/// </summary>
public static class EndpointExtensions
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        // Version endpoint (public)
        app.MapGet("/api/version", () => new
        {
            Version = "1.0.0",
            Name = "AIExamForge API",
            Environment = app.Environment.EnvironmentName
        }).AllowAnonymous();

        // Map actual endpoints
        app.MapOutlineEndpoints();
        app.MapGenerationEndpoints();
        app.MapQuestionEndpoints();
        app.MapExportEndpoints();
        app.MapMcpDiagnosticsEndpoints();
    }
}
