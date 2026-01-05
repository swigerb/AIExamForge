namespace AIExamForge.ContractTests;

public sealed class OpenApiContractSmokeTests
{
    [Fact]
    public void OpenApiYaml_Exists_AndContainsExpectedMarkers()
    {
        var specPath = FindFileUpTree(
            startDirectory: AppContext.BaseDirectory,
            relativePath: Path.Combine("specs", "001-generate-questions", "contracts", "openapi.yaml"));

        Assert.True(File.Exists(specPath), $"OpenAPI spec not found at: {specPath}");

        var text = File.ReadAllText(specPath);

        Assert.Contains("openapi: 3.0.3", text);
        Assert.Contains("title: AIExamForge API", text);
        Assert.Contains("paths:", text);
        Assert.Contains("/generations:", text);
        Assert.Contains("/outlines/parse:", text);
    }

    private static string FindFileUpTree(string startDirectory, string relativePath)
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
