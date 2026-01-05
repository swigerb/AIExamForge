using System.Net;
using System.Net.Http.Json;
using AIExamForge.Application.Questions.DTOs;
using AIExamForge.Application.Parsing.DTOs;

namespace AIExamForge.IntegrationTests;

public sealed class QuestionEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_Question_Unknown_ReturnsNotFound()
    {
        var resp = await _client.GetAsync($"/api/questions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Question_Seeded_ReturnsOk()
    {
        var outlineResp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service",
            ExamName = "AZ-204"
        });
        var outline = await outlineResp.Content.ReadFromJsonAsync<AIExamForge.Application.Parsing.DTOs.OutlineResponse>();
        Assert.NotNull(outline);
        var nodeId = outline!.Nodes.First().Id;

        var (_, _, questionId) = await TestDataSeeder.SeedCompletedGenerationWithOneQuestionAsync(_factory, outline.Id, nodeId);

        var resp = await _client.GetAsync($"/api/questions/{questionId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Put_UpdateQuestion_ReturnsOkAndNewId()
    {
        var outlineResp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service",
            ExamName = "AZ-204"
        });
        var outline = await outlineResp.Content.ReadFromJsonAsync<AIExamForge.Application.Parsing.DTOs.OutlineResponse>();
        Assert.NotNull(outline);
        var nodeId = outline!.Nodes.First().Id;

        var (_, _, questionId) = await TestDataSeeder.SeedCompletedGenerationWithOneQuestionAsync(_factory, outline.Id, nodeId);

        var update = new UpdateQuestionRequest
        {
            Stem = "You need to swap slots in Azure App Service. Which action should you take?"
        };

        var resp = await _client.PutAsJsonAsync($"/api/questions/{questionId}", update);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(json);
        Assert.True(json!.TryGetValue("id", out var newIdObj));
        Assert.NotNull(newIdObj);
    }

    [Fact]
    public async Task Post_RegenerateQuestion_ReturnsOk()
    {
        var resp = await _client.PostAsync($"/api/questions/{Guid.NewGuid()}/regenerate", content: null);
        // Fake generation service returns OK for non-empty GUIDs.
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Put_UpdateQuestion_Unknown_ReturnsNotFound()
    {
        var update = new UpdateQuestionRequest
        {
            Stem = "Updated stem"
        };

        var resp = await _client.PutAsJsonAsync($"/api/questions/{Guid.NewGuid()}", update);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Post_RegenerateQuestion_EmptyGuid_ReturnsNotFound()
    {
        var resp = await _client.PostAsync($"/api/questions/{Guid.Empty}/regenerate", content: null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
