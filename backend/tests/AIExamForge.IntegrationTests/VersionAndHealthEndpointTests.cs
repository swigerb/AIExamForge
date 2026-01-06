using System.Net;
using System.Net.Http.Json;

namespace AIExamForge.IntegrationTests;

public sealed class VersionAndHealthEndpointTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client = factory.CreateClient();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Get_Version_ReturnsOk()
    {
        var resp = await _client.GetAsync("/api/version");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(json);
        Assert.True(json!.ContainsKey("version"));
        Assert.True(json.ContainsKey("name"));
        Assert.True(json.ContainsKey("environment"));
    }

    [Fact]
    public async Task Get_Health_ReturnsOk()
    {
        var resp = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
