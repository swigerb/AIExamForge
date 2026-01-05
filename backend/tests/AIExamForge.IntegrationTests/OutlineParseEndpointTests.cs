using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIExamForge.Application.Parsing.DTOs;

namespace AIExamForge.IntegrationTests;

public sealed class OutlineParseEndpointTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task ParseOutline_WithExamName_ReturnsParsedNodesAndEchoesExamName()
    {
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });

        var outlineText = """
# AZ-204: Developing Solutions for Microsoft Azure

## 1. Develop Azure compute solutions (25-30%)

### 1.1 Implement containerized solutions
- Create and manage container images
- Publish an image to Azure Container Registry
- Run containers by using Azure Container Instance

### 1.2 Implement Azure App Service Web Apps
- Create an Azure App Service Web App
- Configure and monitor App Service apps
""";

        var request = new ParseOutlineRequest
        {
            Text = outlineText,
            ExamName = "AZ-204"
        };

        using var response = await client.PostAsJsonAsync("/api/outlines/parse", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        var parsed = await response.Content.ReadFromJsonAsync<OutlineResponse>(options);

        Assert.NotNull(parsed);
        Assert.Equal("AZ-204", parsed!.ExamName);
        Assert.NotEqual(Guid.Empty, parsed.Id);
        Assert.Equal("TextPaste", parsed.SourceType);
        Assert.NotEqual(default, parsed.CreatedAt);

        Assert.NotNull(parsed.Nodes);
        Assert.Single(parsed.Nodes);

        var root = parsed.Nodes[0];
        Assert.Contains("AZ-204", root.Title);
        Assert.Equal(1, root.Level);

        Assert.Single(root.Children);

        var compute = root.Children.Single(n => n.Title.Contains("Develop Azure compute solutions", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, compute.Level);

        Assert.Equal(2, compute.Children.Count);

        var containers = compute.Children.Single(n => n.Title.Contains("Implement containerized solutions", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, containers.Level);

        // Unindented bullets should attach as children of the last heading.
        Assert.Equal(3, containers.Children.Count);
        Assert.Contains(containers.Children, n => n.Title.Contains("Create and manage container images", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(containers.Children, n => n.Title.Contains("Publish an image to Azure Container Registry", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(containers.Children, n => n.Title.Contains("Run containers by using Azure Container Instance", StringComparison.OrdinalIgnoreCase));
        Assert.All(containers.Children, n => Assert.Equal(4, n.Level));

        var appService = compute.Children.Single(n => n.Title.Contains("Implement Azure App Service Web Apps", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, appService.Level);
        Assert.Equal(2, appService.Children.Count);
        Assert.Contains(appService.Children, n => n.Title.Contains("Create an Azure App Service Web App", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(appService.Children, n => n.Title.Contains("Configure and monitor App Service apps", StringComparison.OrdinalIgnoreCase));
    }
}
