using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AIExamForge.Application.Parsing.DTOs;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.IntegrationTests;

public sealed class ExportEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>, IDisposable
{
    private readonly ApiWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Get_ExportFormats_ReturnsOk()
    {
        var resp = await _client.GetAsync("/api/questions/export/formats");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Export_InvalidFormat_ReturnsBadRequest()
    {
        var resp = await _client.GetAsync($"/api/questions/{Guid.NewGuid()}/export?format=nope&mode=exam");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Export_Seeded_ReturnsFile()
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

        var resp = await _client.GetAsync($"/api/questions/{generationId}/export?format=json&mode=exam");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotNull(resp.Content.Headers.ContentType);
    }

    [Fact]
    public async Task Get_Export_GenerationNotFound_ReturnsNotFound()
    {
        var resp = await _client.GetAsync($"/api/questions/{Guid.NewGuid()}/export?format=json&mode=study");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Export_NoQuestionSet_ReturnsBadRequest()
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

        var resp = await _client.GetAsync($"/api/questions/{generationId}/export?format=json&mode=study");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
