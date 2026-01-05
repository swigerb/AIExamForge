using System.Net;
using System.Net.Http.Json;
using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Application.Parsing.DTOs;

namespace AIExamForge.IntegrationTests;

public sealed class AuthorizationNegativeTests(UnauthenticatedApiWebApplicationFactory factory)
    : IClassFixture<UnauthenticatedApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Outlines_Parse_Unauthenticated_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service",
            ExamName = "AZ-204"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Outlines_List_Unauthenticated_Returns401()
    {
        var resp = await _client.GetAsync("/api/outlines");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Generations_Start_Unauthenticated_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/generations", new GenerationRequestDto
        {
            OutlineId = Guid.NewGuid(),
            SelectedNodeIds = Array.Empty<Guid>(),
            QuestionCount = 1,
            QuestionTypes = new[] { "M1" }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Questions_Get_Unauthenticated_Returns401()
    {
        var resp = await _client.GetAsync($"/api/questions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
