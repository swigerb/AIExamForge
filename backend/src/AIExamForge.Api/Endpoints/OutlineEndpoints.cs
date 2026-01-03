using System.Security.Claims;
using AIExamForge.Application.Documents;
using AIExamForge.Application.Parsing;
using AIExamForge.Application.Parsing.DTOs;

namespace AIExamForge.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for outline parsing and management.
/// Per openapi.yaml: /outlines/* endpoints
/// </summary>
public static class OutlineEndpoints
{
    public static IEndpointRouteBuilder MapOutlineEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/outlines")
            .WithTags("Outlines")
            .RequireAuthorization();

        group.MapPost("/parse", ParseOutline)
            .WithName("parseOutline")
            .WithSummary("Parse text outline")
            .WithDescription("Parse plain text exam outline into hierarchical structure")
            .Produces<OutlineResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{outlineId:guid}", GetOutline)
            .WithName("getOutline")
            .WithSummary("Get outline by ID")
            .WithDescription("Retrieve a previously parsed outline")
            .Produces<OutlineResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", GetUserOutlines)
            .WithName("getUserOutlines")
            .WithSummary("Get user outlines")
            .WithDescription("Get all outlines for the authenticated user")
            .Produces<IReadOnlyList<OutlineResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/upload", UploadOutline)
            .WithName("uploadOutline")
            .WithSummary("Upload outline file")
            .WithDescription("Upload a DOCX or PDF file and parse its outline structure")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<FileUploadResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .DisableAntiforgery();

        return routes;
    }

    private static async Task<IResult> ParseOutline(
        ParseOutlineRequest request,
        OutlineService outlineService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new { message = "Text is required" });
        }

        var response = await outlineService.ParseAndSaveAsync(userId, request, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetOutline(
        Guid outlineId,
        OutlineService outlineService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var response = await outlineService.GetByIdAsync(outlineId, userId, cancellationToken);
        
        if (response == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> GetUserOutlines(
        OutlineService outlineService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var outlines = await outlineService.GetByUserIdAsync(userId, cancellationToken);
        return Results.Ok(outlines);
    }

    private static async Task<IResult> UploadOutline(
        IFormFile file,
        FileUploadService uploadService,
        ClaimsPrincipal user,
        string? examName = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "No file uploaded" });
        }

        // Check file size (10 MB limit per FR-024)
        const long maxSize = 10 * 1024 * 1024;
        if (file.Length > maxSize)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "File Too Large",
                detail: $"File size exceeds maximum of {maxSize / (1024 * 1024)} MB");
        }

        using var stream = file.OpenReadStream();
        var result = await uploadService.ProcessUploadAsync(
            userId,
            stream,
            file.FileName,
            file.Length,
            examName,
            cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { message = result.ErrorMessage });
        }

        return Results.Ok(new FileUploadResponse
        {
            FileUploadId = result.FileUploadId!.Value,
            OutlineId = result.OutlineId!.Value,
            Outline = result.Outline!
        });
    }

    private static string? GetUserId(ClaimsPrincipal user)
    {
        // Try standard claims in order of preference
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("oid")  // Azure AD object ID
            ?? user.FindFirstValue("sub"); // Subject claim
    }
}
