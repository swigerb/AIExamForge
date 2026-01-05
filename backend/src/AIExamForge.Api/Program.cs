using AIExamForge.Api.Endpoints;
using AIExamForge.Api.Extensions;
using AIExamForge.Api.Streaming;
using AIExamForge.Application;
using AIExamForge.Infrastructure;
using AIExamForge.Infrastructure.Data;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault as a configuration source when configured.
// This allows secrets like "AzureOpenAI:ApiKey" to be supplied via Key Vault.
builder.Configuration.AddAzureKeyVault();

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

// In-memory caching (used by MCP retrieval/caching services)
builder.Services.AddMemoryCache();

// Add OpenAPI documentation
builder.Services.AddOpenApi();

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
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
builder.Services.AddProblemDetails();

// Add SSE handler for streaming
builder.Services.AddSingleton<GenerationProgressHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapHealthChecks("/health");

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
    }
}
