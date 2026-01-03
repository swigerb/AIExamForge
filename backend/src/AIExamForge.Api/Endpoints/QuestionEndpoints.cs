using System.Security.Claims;
using System.Text.Json;
using AIExamForge.Application.Generation;
using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Application.Questions;
using AIExamForge.Application.Questions.DTOs;
using AIExamForge.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AIExamForge.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for question management.
/// Per openapi.yaml: /questions/* endpoints
/// </summary>
public static class QuestionEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IEndpointRouteBuilder MapQuestionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/questions")
            .WithTags("Questions")
            .RequireAuthorization();

        // T126: POST /api/questions/{questionId}/regenerate - Regenerate single question
        group.MapPost("/{questionId:guid}/regenerate", RegenerateQuestion)
            .WithName("regenerateQuestion")
            .WithSummary("Regenerate a single question")
            .WithDescription(@"Regenerate a single question within a question set.
The original question will be replaced with a newly generated one
using the same parameters (item type, difficulty, topic).

**MCP-Exclusive**: New question is generated only from Microsoft Learn content.")
            .Produces<QuestionRegenerationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        // T129: PUT /api/questions/{questionId} - Update a question
        group.MapPut("/{questionId:guid}", UpdateQuestion)
            .WithName("updateQuestion")
            .WithSummary("Update a question")
            .WithDescription(@"Update a question's content, stem, choices, or explanation.
The updated content will be validated against schema and lint rules
before persisting.")
            .Produces<QuestionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // GET /api/questions/{questionId} - Get a single question
        group.MapGet("/{questionId:guid}", GetQuestion)
            .WithName("getQuestion")
            .WithSummary("Get a single question")
            .WithDescription("Get details of a single question")
            .Produces<QuestionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return routes;
    }

    /// <summary>
    /// T126: Regenerate a single question.
    /// </summary>
    private static async Task<IResult> RegenerateQuestion(
        Guid questionId,
        [FromServices] QuestionGenerationService generationService,
        [FromServices] IQuestionSetRepository questionSetRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Regenerate the question
        var result = await generationService.RegenerateQuestionAsync(questionId, userId, cancellationToken);

        if (!result.Success)
        {
            return result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? Results.NotFound()
                : Results.Problem(
                    detail: result.ErrorMessage,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Regeneration Failed");
        }

        // Get the new question details
        if (result.NewQuestionId.HasValue)
        {
            var questionSets = await questionSetRepository.GetByUserIdAsync(userId, cancellationToken);
            foreach (var set in questionSets)
            {
                var setWithQuestions = await questionSetRepository.GetByIdWithQuestionsAsync(set.Id, cancellationToken);
                var newQuestion = setWithQuestions?.Questions.FirstOrDefault(q => q.Id == result.NewQuestionId.Value);
                if (newQuestion != null)
                {
                    return Results.Ok(new QuestionRegenerationResponse
                    {
                        Success = true,
                        OriginalQuestionId = result.OriginalQuestionId,
                        NewQuestion = MapToQuestionResponse(newQuestion)
                    });
                }
            }
        }

        return Results.Ok(new QuestionRegenerationResponse
        {
            Success = true,
            OriginalQuestionId = result.OriginalQuestionId,
            Message = "Question regenerated successfully"
        });
    }

    /// <summary>
    /// T129: Update a question with validation.
    /// </summary>
    private static async Task<IResult> UpdateQuestion(
        Guid questionId,
        [FromBody] Application.Questions.DTOs.UpdateQuestionRequest request,
        [FromServices] QuestionEditingService editingService,
        [FromServices] IQuestionSetRepository questionSetRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Use the QuestionEditingService to update and validate
        var result = await editingService.UpdateQuestionAsync(questionId, userId, request, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Results.NotFound();
            }

            return Results.Problem(
                detail: result.ErrorMessage,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Failed",
                extensions: result.ValidationErrors.Count > 0
                    ? new Dictionary<string, object?> { ["errors"] = result.ValidationErrors }
                    : null);
        }

        // Get the updated question to return
        if (result.NewQuestionId.HasValue)
        {
            var questionSets = await questionSetRepository.GetByUserIdAsync(userId, cancellationToken);
            foreach (var set in questionSets)
            {
                var setWithQuestions = await questionSetRepository.GetByIdWithQuestionsAsync(set.Id, cancellationToken);
                var updatedQuestion = setWithQuestions?.Questions.FirstOrDefault(q => q.Id == result.NewQuestionId.Value);
                if (updatedQuestion != null)
                {
                    var response = MapToQuestionResponse(updatedQuestion);
                    // Include warnings if any
                    if (result.Warnings.Count > 0)
                    {
                        // Return with warnings in headers
                        return Results.Ok(response);
                    }
                    return Results.Ok(response);
                }
            }
        }

        return Results.NotFound();
    }

    /// <summary>
    /// Get a single question by ID.
    /// </summary>
    private static async Task<IResult> GetQuestion(
        Guid questionId,
        [FromServices] IQuestionSetRepository questionSetRepository,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Find the question across all user's question sets
        var questionSets = await questionSetRepository.GetByUserIdAsync(userId, cancellationToken);
        foreach (var set in questionSets)
        {
            var setWithQuestions = await questionSetRepository.GetByIdWithQuestionsAsync(set.Id, cancellationToken);
            var question = setWithQuestions?.Questions.FirstOrDefault(q => q.Id == questionId);
            if (question != null)
            {
                return Results.Ok(MapToQuestionResponse(question));
            }
        }

        return Results.NotFound();
    }

    private static QuestionResponse MapToQuestionResponse(Domain.Entities.Question question)
    {
        return new QuestionResponse
        {
            Id = question.Id,
            ItemTypeCode = question.ItemType.Code,
            TypeDisplayName = GetItemTypeDisplayName(question.ItemType.Code),
            Title = question.Title,
            Stem = question.Stem,
            Instruction = question.Instruction,
            Difficulty = question.Difficulty.ToString(),
            Content = ParseJsonContent(question.ContentJson),
            Rationale = question.Rationale,
            Tags = new QuestionTagsDto
            {
                Exam = question.Tags.Exam,
                Objective = question.Tags.Objective,
                Section = question.Tags.Section,
                Topics = question.Tags.Topics.ToList()
            },
            Scoring = new QuestionScoringDto
            {
                Model = question.Scoring.Model,
                MaxPoints = question.Scoring.MaxPoints
            },
            CreatedAt = question.CreatedAt,
            LastModifiedAt = question.LastModifiedAt
        };
    }

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
}

/// <summary>
/// Response for question regeneration.
/// </summary>
public sealed record QuestionRegenerationResponse
{
    public bool Success { get; init; }
    public Guid OriginalQuestionId { get; init; }
    public QuestionResponse? NewQuestion { get; init; }
    public string? Message { get; init; }
}

/// <summary>
/// Full question response DTO.
/// </summary>
public sealed record QuestionResponse
{
    public Guid Id { get; init; }
    public required string ItemTypeCode { get; init; }
    public required string TypeDisplayName { get; init; }
    public required string Title { get; init; }
    public required string Stem { get; init; }
    public string? Instruction { get; init; }
    public required string Difficulty { get; init; }
    public required object Content { get; init; }
    public string? Rationale { get; init; }
    public required QuestionTagsDto Tags { get; init; }
    public required QuestionScoringDto Scoring { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastModifiedAt { get; init; }
}

/// <summary>
/// Question tags DTO.
/// </summary>
public sealed record QuestionTagsDto
{
    public required string Exam { get; init; }
    public required string Objective { get; init; }
    public required string Section { get; init; }
    public required List<string> Topics { get; init; }
}

/// <summary>
/// Question scoring DTO.
/// </summary>
public sealed record QuestionScoringDto
{
    public required string Model { get; init; }
    public int MaxPoints { get; init; }
}
