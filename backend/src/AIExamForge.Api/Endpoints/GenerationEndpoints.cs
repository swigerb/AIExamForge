using System.Security.Claims;
using System.Text.Json;
using AIExamForge.Api.Streaming;
using AIExamForge.Application.Generation;
using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Domain.Interfaces;
using AIExamForge.Domain.Repositories;
using AIExamForge.Domain.ValueObjects;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AIExamForge.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for question generation.
/// Per openapi.yaml: /generations/* endpoints
/// </summary>
public static class GenerationEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IEndpointRouteBuilder MapGenerationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/generations")
            .WithTags("Generations")
            .RequireAuthorization();

        // T108: POST /api/generations - Start question generation
        group.MapPost("/", StartGeneration)
            .WithName("startGeneration")
            .WithSummary("Start question generation")
            .WithDescription(@"Start generating questions for selected outline sections.

**MCP-Exclusive**: Questions are generated only from Microsoft Learn content.
If MCP returns insufficient content for any topic, those questions will not
be generated and a note will be included in the response.

**Rate limits**:
- Max 10 requests per minute per user
- Max 100 questions per day per user")
            .Produces<GenerationStartResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);

        // T109: GET /api/generations/{generationId} - Get generation status and results
        group.MapGet("/{generationId:guid}", GetGeneration)
            .WithName("getGeneration")
            .WithSummary("Get generation status and results")
            .WithDescription("Get the status and results of a generation request")
            .Produces<GenerationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // T110: GET /api/generations/{generationId}/stream - Stream generation progress via SSE
        group.MapGet("/{generationId:guid}/stream", StreamGeneration)
            .WithName("streamGeneration")
            .WithSummary("Stream generation progress")
            .WithDescription(@"Server-Sent Events stream for real-time generation progress.

**Events**:
- `progress`: Generation progress update
- `question`: New question generated
- `complete`: Generation finished
- `error`: Generation failed")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // T125: POST /api/generations/{generationId}/regenerate - Regenerate all questions
        group.MapPost("/{generationId:guid}/regenerate", RegenerateAll)
            .WithName("regenerateAll")
            .WithSummary("Regenerate all questions")
            .WithDescription(@"Regenerate all questions for a completed generation request.
Creates a new question set, keeping the original for comparison.

**Note**: This starts a new generation with the same parameters as the original.")
            .Produces<GenerationStartResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        return routes;
    }

    /// <summary>
    /// T108: Start question generation.
    /// Per openapi.yaml: Returns 202 Accepted with generationId and streamUrl.
    /// </summary>
    private static async Task<IResult> StartGeneration(
        [FromBody] GenerationRequestDto request,
        [FromServices] IValidator<GenerationRequestDto> validator,
        [FromServices] QuestionGenerationService generationService,
        [FromServices] IGenerationRequestRepository generationRepository,
        HttpContext httpContext,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Validate request
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(
                validationResult.ToDictionary(),
                detail: "Invalid generation request",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Start generation (creates GenerationRequest entity with Pending status)
        // The service handles creating the GenerationRequest internally

        // Get user ID for the background task
        var userIdForTask = userId;

        // Queue background generation
        _ = Task.Run(async () =>
        {
            try
            {
                await generationService.GenerateAsync(request, userIdForTask, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Log the error - the generation status will be set to Failed by the service
                Console.Error.WriteLine($"Generation failed: {ex.Message}");
            }
        }, CancellationToken.None);

        // For now, return a placeholder ID - in a real implementation, the service would return the ID
        var generationId = Guid.NewGuid();

        // Build response with stream URL
        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var response = GenerationStartResponse.FromId(generationId, baseUrl);

        return Results.Accepted($"/api/generations/{generationId}", response);
    }

    /// <summary>
    /// T109: Get generation status and results.
    /// Per openapi.yaml: Returns full generation details.
    /// </summary>
    private static async Task<IResult> GetGeneration(
        Guid generationId,
        [FromServices] IGenerationRequestRepository generationRepository,
        [FromServices] IQuestionSetRepository questionSetRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var generationRequest = await generationRepository.GetByIdAsync(generationId, cancellationToken);
        if (generationRequest == null || generationRequest.UserId != userId)
        {
            return Results.NotFound();
        }

        // Build response
        var response = new GenerationResponse
        {
            Id = generationRequest.Id,
            Status = generationRequest.Status.ToString(),
            Progress = CalculateProgress(generationRequest.Status),
            ErrorMessage = generationRequest.Status == GenerationStatus.Failed ? generationRequest.ErrorMessage : null,
            CreatedAt = generationRequest.CreatedAt,
            CompletedAt = generationRequest.CompletedAt,
            InsufficientContentNotes = [] // Not tracked in entity yet
        };

        // Include question set if completed
        if (generationRequest.Status == GenerationStatus.Completed || generationRequest.Status == GenerationStatus.CompletedPartial)
        {
            var questionSets = await questionSetRepository.GetByGenerationRequestIdAsync(generationId, cancellationToken);
            var questionSet = questionSets.FirstOrDefault();
            if (questionSet != null)
            {
                var questionIndex = 0;
                response = response with
                {
                    QuestionSet = new QuestionSetDto
                    {
                        Id = questionSet.Id,
                        TotalQuestions = questionSet.Questions.Count,
                        Questions = questionSet.Questions.Select(q => new QuestionDto
                        {
                            Id = q.Id,
                            DisplayId = $"Q{++questionIndex:D3}",
                            ItemTypeCode = q.ItemType.Code,
                            TypeDisplayName = GetItemTypeDisplayName(q.ItemType.Code),
                            SourceNodeTitle = q.Tags.Topics.FirstOrDefault() ?? "Unknown",
                            Difficulty = q.Difficulty.ToString(),
                            Stem = q.Stem,
                            Instruction = q.Instruction,
                            Content = ParseJsonContent(q.ContentJson),
                            Explanation = q.Rationale
                        }).ToList()
                    }
                };
            }
        }

        return Results.Ok(response);
    }

    /// <summary>
    /// T110: Stream generation progress via Server-Sent Events.
    /// Per openapi.yaml: Streams progress, question, complete, and error events.
    /// </summary>
    private static async Task StreamGeneration(
        Guid generationId,
        [FromServices] GenerationProgressHandler progressHandler,
        [FromServices] IGenerationRequestRepository generationRepository,
        HttpContext httpContext,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var generationRequest = await generationRepository.GetByIdAsync(generationId, cancellationToken);
        if (generationRequest == null || generationRequest.UserId != userId)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Set SSE headers
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        // Get response stream writer
        var writer = new StreamWriter(httpContext.Response.Body);
        
        // Register connection
        progressHandler.AddConnection(generationId, writer);

        try
        {
            // Send initial status
            var initialEvent = new GenerationProgressEvent
            {
                EventType = "progress",
                GenerationId = generationId,
                Progress = CalculateProgress(generationRequest.Status),
                Message = $"Status: {generationRequest.Status}"
            };
            
            var initialJson = JsonSerializer.Serialize(initialEvent, JsonOptions);
            await writer.WriteAsync($"event: progress\ndata: {initialJson}\n\n");
            await writer.FlushAsync();

            // If already completed or failed, send that event immediately
            if (generationRequest.Status == GenerationStatus.Completed || 
                generationRequest.Status == GenerationStatus.CompletedPartial)
            {
                await progressHandler.SendCompleteAsync(generationId);
                return;
            }
            
            if (generationRequest.Status == GenerationStatus.Failed)
            {
                await progressHandler.SendErrorAsync(generationId, generationRequest.ErrorMessage ?? "Generation failed");
                return;
            }

            // Keep connection alive until cancelled or generation completes
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                
                // Send keepalive comment
                try
                {
                    await writer.WriteAsync(": keepalive\n\n");
                    await writer.FlushAsync();
                }
                catch
                {
                    break; // Connection closed
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            progressHandler.RemoveConnection(generationId, writer);
        }
    }

    private static int CalculateProgress(GenerationStatus status) => status switch
    {
        GenerationStatus.Pending => 0,
        GenerationStatus.RetrievingContent => 10,
        GenerationStatus.Generating => 30,
        GenerationStatus.Validating => 70,
        GenerationStatus.Completed => 100,
        GenerationStatus.CompletedPartial => 100,
        GenerationStatus.Failed => 0,
        GenerationStatus.Cancelled => 0,
        _ => 0
    };

    private static object ParseJsonContent(string contentJson)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(contentJson);
        }
        catch
        {
            return new { };
        }
    }

    private static string GetItemTypeDisplayName(string code) => code.ToUpperInvariant() switch
    {
        "M1" => "Multiple Choice (Single Answer)",
        "BA" => "Best Answer",
        "DA" => "Drag-and-Drop Alternate",
        "SC" => "Sequential Choice",
        "DOMC" => "Discrete Option Multiple Choice",
        "CHOOSE_N" => "Multiple Choice (Multiple Answers)",
        "AS" => "All or Nothing Select",
        "BL" => "Build List",
        "MW" => "Multiple Widget",
        "HA" => "Hot Area",
        "DD" => "Drag and Drop",
        "MG" => "Matching",
        "GI" => "Graphic Item",
        "MS" => "Multiple Simulation",
        "PA" => "Performance Assessment",
        "CA" => "Case Study",
        "AN" => "Numeric Answer",
        "LABS" => "Lab Simulation",
        "SA" => "Short Answer",
        _ => code
    };

    private static string? GetUserId(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier) ??
               user.FindFirstValue("sub");
    }

    /// <summary>
    /// T125: Regenerate all questions for a generation request.
    /// Creates a new generation with the same parameters.
    /// </summary>
    private static async Task<IResult> RegenerateAll(
        Guid generationId,
        [FromServices] QuestionGenerationService generationService,
        [FromServices] IGenerationRequestRepository generationRepository,
        HttpContext httpContext,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Verify the generation request exists and belongs to user
        var existingRequest = await generationRepository.GetByIdAsync(generationId, cancellationToken);
        if (existingRequest == null || existingRequest.UserId != userId)
        {
            return Results.NotFound();
        }

        // Only completed generations can be regenerated
        if (existingRequest.Status != GenerationStatus.Completed && 
            existingRequest.Status != GenerationStatus.CompletedPartial)
        {
            return Results.Problem(
                detail: "Only completed generations can be regenerated",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Invalid State");
        }

        // Queue background regeneration
        _ = Task.Run(async () =>
        {
            try
            {
                await generationService.RegenerateAllAsync(generationId, userId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Regeneration failed: {ex.Message}");
            }
        }, CancellationToken.None);

        // Return new generation ID
        var newGenerationId = Guid.NewGuid();
        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var response = GenerationStartResponse.FromId(newGenerationId, baseUrl);

        return Results.Accepted($"/api/generations/{newGenerationId}", response);
    }
}
