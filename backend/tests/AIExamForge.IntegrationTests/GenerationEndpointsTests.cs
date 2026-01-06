using System.Net;
using System.Net.Http.Json;
using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Application.Parsing.DTOs;
using AIExamForge.Domain.ValueObjects;
using DtoRenderMode = AIExamForge.Application.Generation.DTOs.RenderMode;

namespace AIExamForge.IntegrationTests;

public sealed class GenerationEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_Generation_Unknown_ReturnsNotFound()
    {
        var resp = await _client.GetAsync($"/api/generations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Post_StartGeneration_Invalid_ReturnsBadRequest()
    {
        var req = new GenerationRequestDto
        {
            OutlineId = Guid.Empty,
            SelectedNodeIds = Array.Empty<Guid>(),
            QuestionCount = 0,
            QuestionTypes = Array.Empty<string>()
        };

        var resp = await _client.PostAsJsonAsync("/api/generations", req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_StartGeneration_Valid_ReturnsAccepted()
    {
        // Create an outline first to have a real outlineId/nodeId
        var outlineResp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service",
            ExamName = "AZ-204"
        });
        var outline = await outlineResp.Content.ReadFromJsonAsync<AIExamForge.Application.Parsing.DTOs.OutlineResponse>();
        Assert.NotNull(outline);

        var nodeId = outline!.Nodes.First().Id;
        var req = new GenerationRequestDto
        {
            OutlineId = outline.Id,
            SelectedNodeIds = new[] { nodeId },
            QuestionCount = 1,
            QuestionTypes = new[] { "M1" },
            RenderMode = DtoRenderMode.Study
        };

        var resp = await _client.PostAsJsonAsync("/api/generations", req);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task Post_StartGeneration_Valid_ImmediatelyQueryable_DoesNot404()
    {
        // Create an outline first to have a real outlineId/nodeId
        var outlineResp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service",
            ExamName = "AZ-204"
        });
        var outline = await outlineResp.Content.ReadFromJsonAsync<AIExamForge.Application.Parsing.DTOs.OutlineResponse>();
        Assert.NotNull(outline);

        var nodeId = outline!.Nodes.First().Id;
        var req = new GenerationRequestDto
        {
            OutlineId = outline.Id,
            SelectedNodeIds = new[] { nodeId },
            QuestionCount = 1,
            QuestionTypes = new[] { "M1" },
            RenderMode = DtoRenderMode.Study
        };

        var resp = await _client.PostAsJsonAsync("/api/generations", req);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        var start = await resp.Content.ReadFromJsonAsync<GenerationStartResponse>();
        Assert.NotNull(start);
        Assert.NotEqual(Guid.Empty, start!.GenerationId);

        var get = await _client.GetAsync($"/api/generations/{start.GenerationId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Post_StartGeneration_AllowsStringEnum_RenderMode()
    {
        var outlineResp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service",
            ExamName = "AZ-204"
        });
        var outline = await outlineResp.Content.ReadFromJsonAsync<AIExamForge.Application.Parsing.DTOs.OutlineResponse>();
        Assert.NotNull(outline);

        var nodeId = outline!.Nodes.First().Id;

        var json = $$"""
        {
                    "outlineId": "{{outline.Id}}",
                    "selectedNodeIds": ["{{nodeId}}"],
                    "questionCount": 1,
                    "questionTypes": ["M1"],
                    "renderMode": "Study"
        }
        """;

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var resp = await _client.PostAsync("/api/generations", content);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Generation_SeededCompleted_ReturnsOkWithQuestions()
    {
        var outlineResp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service",
            ExamName = "AZ-204"
        });
        var outline = await outlineResp.Content.ReadFromJsonAsync<AIExamForge.Application.Parsing.DTOs.OutlineResponse>();
        Assert.NotNull(outline);
        var nodeId = outline!.Nodes.First().Id;

        var (generationId, _, _) = await TestDataSeeder.SeedCompletedGenerationWithOneQuestionAsync(_factory, outline.Id, nodeId);

        var resp = await _client.GetAsync($"/api/generations/{generationId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("status"));
        Assert.True(body.ContainsKey("questionSet"));
    }

    [Fact]
    public async Task Get_Generation_Stream_Completed_EmitsCompleteEvent()
    {
        var outlineResp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service",
            ExamName = "AZ-204"
        });
        var outline = await outlineResp.Content.ReadFromJsonAsync<AIExamForge.Application.Parsing.DTOs.OutlineResponse>();
        Assert.NotNull(outline);
        var nodeId = outline!.Nodes.First().Id;

        var (generationId, _, _) = await TestDataSeeder.SeedCompletedGenerationWithOneQuestionAsync(_factory, outline.Id, nodeId);

        using var resp = await _client.GetAsync($"/api/generations/{generationId}/stream", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/event-stream", resp.Content.Headers.ContentType?.MediaType);

        var text = await resp.Content.ReadAsStringAsync();
        Assert.Contains("event: progress", text);
        Assert.Contains("event: complete", text);
    }

    [Fact]
    public async Task Post_RegenerateAll_Pending_ReturnsUnprocessableEntity()
    {
        var outlineResp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service",
            ExamName = "AZ-204"
        });
        var outline = await outlineResp.Content.ReadFromJsonAsync<AIExamForge.Application.Parsing.DTOs.OutlineResponse>();
        Assert.NotNull(outline);
        var nodeId = outline!.Nodes.First().Id;

        var generationId = await TestDataSeeder.SeedGenerationAsync(_factory, GenerationStatus.Pending, outline.Id, nodeId);

        var resp = await _client.PostAsync($"/api/generations/{generationId}/regenerate", content: null);
        Assert.Equal((HttpStatusCode)422, resp.StatusCode);
    }

    [Fact]
    public async Task Post_RegenerateAll_Unknown_ReturnsNotFound()
    {
        var resp = await _client.PostAsync($"/api/generations/{Guid.NewGuid()}/regenerate", content: null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
