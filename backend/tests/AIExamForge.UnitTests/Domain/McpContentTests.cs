using AIExamForge.Domain.Models;
using FluentAssertions;

namespace AIExamForge.UnitTests.Domain;

public sealed class McpContentTests
{
    [Fact]
    public void GetSourceSummary_WhenNoSources_ReturnsMessage()
    {
        var content = McpContent.Create("q", "c", Array.Empty<string>(), queryTimeMs: 1);

        content.GetSourceSummary().Should().Be("No sources available");
    }

    [Fact]
    public void McpContentCollection_AllSourceUrls_DedupesAcrossItems()
    {
        var a = McpContent.Create("q1", "c1", new[] { "https://learn.microsoft.com/a", "https://learn.microsoft.com/b" }, 10);
        var b = McpContent.Create("q2", "c2", new[] { "https://learn.microsoft.com/b", "https://learn.microsoft.com/c" }, 20);

        var collection = new McpContentCollection { Items = new[] { a, b } };

        collection.AllSourceUrls.Should().BeEquivalentTo(
            new[] { "https://learn.microsoft.com/a", "https://learn.microsoft.com/b", "https://learn.microsoft.com/c" },
            options => options.WithoutStrictOrdering());
    }

    [Fact]
    public void GetCombinedContent_FormatsWithHeadersAndSeparators()
    {
        var a = McpContent.Create("Topic A", "Alpha", new[] { "https://learn.microsoft.com/a" }, 10);
        var b = McpContent.Create("Topic B", "Beta", new[] { "https://learn.microsoft.com/b" }, 20);

        var collection = new McpContentCollection { Items = new[] { a, b } };
        var combined = collection.GetCombinedContent();

        combined.Should().Contain("# Topic A");
        combined.Should().Contain("Alpha");
        combined.Should().Contain("---");
        combined.Should().Contain("# Topic B");
        combined.Should().Contain("Beta");
    }
}
