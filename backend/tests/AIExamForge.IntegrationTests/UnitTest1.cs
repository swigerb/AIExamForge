using System.Net;
using System.Text.Json;

namespace AIExamForge.IntegrationTests;

public sealed class VersionEndpointTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task GetVersion_ReturnsOkAndExpectedPayload()
    {
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        static JsonElement GetPropertyCaseInsensitive(JsonElement element, string camelCase, string pascalCase)
        {
            if (element.TryGetProperty(camelCase, out var camel))
            {
                return camel;
            }

            if (element.TryGetProperty(pascalCase, out var pascal))
            {
                return pascal;
            }

            throw new KeyNotFoundException($"Neither '{camelCase}' nor '{pascalCase}' was present in the JSON payload.");
        }

        Assert.Equal("AIExamForge API", GetPropertyCaseInsensitive(doc.RootElement, "name", "Name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(GetPropertyCaseInsensitive(doc.RootElement, "version", "Version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(GetPropertyCaseInsensitive(doc.RootElement, "environment", "Environment").GetString()));
    }
}
