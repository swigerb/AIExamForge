using System.Text.Json;
using AIExamForge.Application.Generation;
using Xunit;

namespace AIExamForge.UnitTests.Generation;

public sealed class M1ContentNormalizationTests
{
    [Fact]
    public void NormalizeItemTypeContent_M1_UnwrapsContentWrapper_AndDefaultsRandomizeChoices()
    {
        // Simulate the common model bug:
        // content = { "content": { "randomizeChoices": true }, "choices": [...], "correctChoiceKey": "A" }
        // M1 schema requires randomizeChoices at root and disallows additional properties like "content".
        const string raw = """
        {
          "choices": [
            { "key": "A", "text": "Choice A" },
            { "key": "B", "text": "Choice B" },
            { "key": "C", "text": "Choice C" }
          ],
          "correctChoiceKey": "A",
          "content": {
            "randomizeChoices": true
          }
        }
        """;

        using var doc = JsonDocument.Parse(raw);
        var normalized = QuestionGenerationService.NormalizeItemTypeContentForTests("M1", doc.RootElement);

        Assert.NotNull(normalized);
        Assert.Equal(JsonValueKind.Object, normalized!.Value.ValueKind);

        Assert.False(normalized.Value.TryGetProperty("content", out _));

        Assert.True(normalized.Value.TryGetProperty("randomizeChoices", out var randomize));
        Assert.Equal(JsonValueKind.True, randomize.ValueKind);
    }

    [Fact]
    public void NormalizeItemTypeContent_M1_DefaultsRandomizeChoices_WhenMissing()
    {
        const string raw = """
        {
          "choices": [
            { "key": "A", "text": "Choice A" },
            { "key": "B", "text": "Choice B" },
            { "key": "C", "text": "Choice C" }
          ],
          "correctChoiceKey": "A",
          "content": {
            "note": "model forgot randomizeChoices"
          }
        }
        """;

        using var doc = JsonDocument.Parse(raw);
        var normalized = QuestionGenerationService.NormalizeItemTypeContentForTests("M1", doc.RootElement);

        Assert.NotNull(normalized);
        Assert.True(normalized!.Value.TryGetProperty("randomizeChoices", out var randomize));
        Assert.Equal(JsonValueKind.True, randomize.ValueKind);
    }
}
