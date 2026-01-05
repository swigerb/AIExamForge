using System.Net;
using System.Net.Http.Json;
using AIExamForge.Application.Documents;
using AIExamForge.Application.Parsing.DTOs;

namespace AIExamForge.IntegrationTests;

public sealed class OutlineEndpointsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_Parse_EmptyText_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = " ",
            ExamName = "AZ-204"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_Parse_Valid_ReturnsOutline()
    {
        var resp = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure App Service\n- Deployment slots",
            ExamName = "AZ-204"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var outline = await resp.Content.ReadFromJsonAsync<OutlineResponse>();
        Assert.NotNull(outline);
        Assert.NotEqual(Guid.Empty, outline!.Id);
        Assert.Equal("AZ-204", outline.ExamName);
        Assert.NotEmpty(outline.Nodes);
    }

    [Fact]
    public async Task Get_ById_Unknown_ReturnsNotFound()
    {
        var resp = await _client.GetAsync($"/api/outlines/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_UserOutlines_ReturnsOk()
    {
        // Ensure at least one outline exists for the test user
        _ = await _client.PostAsJsonAsync("/api/outlines/parse", new ParseOutlineRequest
        {
            Text = "1. Compute\n1.1 Azure Functions",
            ExamName = "AZ-204"
        });

        var resp = await _client.GetAsync("/api/outlines");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var outlines = await resp.Content.ReadFromJsonAsync<List<OutlineResponse>>();
        Assert.NotNull(outlines);
        Assert.NotEmpty(outlines!);
    }

    [Fact]
    public async Task Post_Upload_PdfWithoutExtractor_ReturnsBadRequest()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }), "file", "outline.pdf");

        var resp = await _client.PostAsync("/api/outlines/upload?examName=AZ-204", form);

        // Application currently has no IDocumentExtractor implementations, so this is the expected behavior.
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
