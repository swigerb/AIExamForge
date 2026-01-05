using AIExamForge.Application.Mcp.Interfaces;
using AIExamForge.Infrastructure.MCP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AIExamForge.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for diagnosing Microsoft Learn MCP connectivity.
/// Intended for development and troubleshooting.
/// </summary>
public static class McpDiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapMcpDiagnosticsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/mcp")
            .WithTags("MCP")
            .RequireAuthorization();

        group.MapGet("/health", Health)
            .WithName("mcpHealth")
            .WithSummary("Check Microsoft Learn MCP availability")
            .WithDescription("Initializes an MCP session and lists tools to verify connectivity")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/test", Test)
            .WithName("mcpTest")
            .WithSummary("Run a test MCP docs query")
            .WithDescription("Runs microsoft_docs_search (and optionally fetch) via MCP and returns a short summary")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status502BadGateway);

        return routes;
    }

    private static async Task<IResult> Health(
        IMcpClient mcpClient,
        IOptions<McpClientOptions> options,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var available = await mcpClient.IsAvailableAsync(cancellationToken);

        return Results.Ok(new
        {
            Available = available,
            BaseUrl = environment.IsDevelopment() ? options.Value.BaseUrl : null
        });
    }

    private static async Task<IResult> Test(
        IMcpClient mcpClient,
        string? query,
        CancellationToken cancellationToken)
    {
        query = string.IsNullOrWhiteSpace(query)
            ? "Implement Azure App Service web apps"
            : query;

        var result = await mcpClient.QueryAsync(query, cancellationToken);

        if (!result.Success)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "MCP query failed",
                detail: result.ErrorMessage ?? "Unknown error");
        }

        var urls = result.GetSourceUrls().Take(5).ToArray();

        return Results.Ok(new
        {
            Query = query,
            result.Success,
            result.QueryId,
            result.QueryTimeMs,
            ContentChunkCount = result.ContentChunks.Count,
            SourceUrlCount = result.GetSourceUrls().Count,
            SampleUrls = urls
        });
    }
}
