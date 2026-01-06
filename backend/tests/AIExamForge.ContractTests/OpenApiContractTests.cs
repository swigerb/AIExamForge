using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIExamForge.ContractTests;

public sealed class OpenApiSpecFileContractTests
{
    [Fact]
    public void OpenApiYaml_Exists_AndContainsExpectedMarkers()
    {
        var specPath = ContractTestPaths.FindFileUpTree(
            startDirectory: AppContext.BaseDirectory,
            relativePath: Path.Combine("specs", "001-generate-questions", "contracts", "openapi.yaml"));

        Assert.True(File.Exists(specPath), $"OpenAPI spec not found at: {specPath}");

        var text = File.ReadAllText(specPath);
        Assert.Contains("openapi: 3.0.3", text);
        Assert.Contains("title: AIExamForge API", text);
        Assert.Contains("paths:", text);
        Assert.Contains("/generations:", text);
        Assert.Contains("/outlines/parse:", text);
        Assert.Contains("/questions/{generationId}/export:", text);
    }

    [Fact]
    public void OpenApiYaml_DefinesExpectedTopLevelPaths()
    {
        var yamlPaths = ReadSpecPaths();

        Assert.Contains("/outlines/parse", yamlPaths);
        Assert.Contains("/generations", yamlPaths);
        Assert.Contains("/health", yamlPaths);
    }

    private static HashSet<string> ReadSpecPaths()
    {
        var specPath = ContractTestPaths.FindFileUpTree(
            startDirectory: AppContext.BaseDirectory,
            relativePath: Path.Combine("specs", "001-generate-questions", "contracts", "openapi.yaml"));

        var text = File.ReadAllText(specPath);

        // Extract YAML path keys with indentation two spaces: "  /foo/bar:".
        var matches = Regex.Matches(text, @"^\s{2}(/[^:]+):\s*$", RegexOptions.Multiline);
        var paths = matches.Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        return paths;
    }
}

public sealed class OpenApiRuntimeContractTests : IClassFixture<ContractApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiRuntimeContractTests(ContractApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiEndpoint_ReturnsDocument_WithExpectedPaths()
    {
        using var resp = await _client.GetAsync("/openapi/v1.json");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("paths", out var pathsElement));
        var paths = pathsElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("/api/outlines/parse", paths);
        Assert.Contains("/api/outlines/upload", paths);
        Assert.Contains("/api/outlines/{outlineId}", paths);
        Assert.Contains("/api/generations/{generationId}/stream", paths);
        Assert.Contains("/api/questions/{generationId}/export", paths);

        // Some OpenAPI generators normalize the trailing slash differently for group roots.
        Assert.True(
            paths.Contains("/api/generations") || paths.Contains("/api/generations/"),
            "Expected OpenAPI to contain /api/generations (or /api/generations/) path.");
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsSuccessOrUnhealthy()
    {
        using var resp = await _client.GetAsync("/health");

        // Per openapi.yaml, health can be 200 (healthy) or 503 (unhealthy).
        Assert.True(
            resp.StatusCode == System.Net.HttpStatusCode.OK || resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable,
            $"Unexpected /health status code: {(int)resp.StatusCode} {resp.StatusCode}");
    }

    [Fact]
    public async Task OpenApiRuntime_Covers_AllSpecPaths_WithApiPrefix()
    {
        var specPath = ContractTestPaths.FindFileUpTree(
            startDirectory: AppContext.BaseDirectory,
            relativePath: Path.Combine("specs", "001-generate-questions", "contracts", "openapi.yaml"));

        var specText = File.ReadAllText(specPath);
        var specMatches = Regex.Matches(specText, @"^\s{2}(/[^:]+):\s*$", RegexOptions.Multiline);
        var specPaths = specMatches.Select(m => m.Groups[1].Value).ToList();

        using var resp = await _client.GetAsync("/openapi/v1.json");
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var runtimePaths = doc.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var specPathValue in specPaths)
        {
            // Spec servers are rooted at /api, except /health which is intentionally at the root.
            if (specPathValue.Equals("/health", StringComparison.Ordinal))
            {
                using var healthResp = await _client.GetAsync("/health");
                Assert.True(
                    healthResp.StatusCode == System.Net.HttpStatusCode.OK ||
                    healthResp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable,
                    $"Unexpected /health status code: {(int)healthResp.StatusCode} {healthResp.StatusCode}");
                continue;
            }

            var expectedRuntimePath = $"/api{specPathValue}";

            if (expectedRuntimePath.Equals("/api/generations", StringComparison.Ordinal))
            {
                Assert.True(
                    runtimePaths.Contains("/api/generations") || runtimePaths.Contains("/api/generations/"),
                    "Runtime OpenAPI missing generations root path.");
                continue;
            }

            Assert.Contains(expectedRuntimePath, runtimePaths);
        }
    }
}

internal static class ContractTestPaths
{
    public static string FindFileUpTree(string startDirectory, string relativePath)
    {
        var current = new DirectoryInfo(startDirectory);

        for (var i = 0; i < 12 && current != null; i++)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        // Fall back to best-effort path for error messaging
        return Path.Combine(new DirectoryInfo(startDirectory).FullName, relativePath);
    }
}
