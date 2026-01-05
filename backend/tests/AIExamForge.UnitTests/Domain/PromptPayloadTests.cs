using AIExamForge.Domain.Models;
using FluentAssertions;

namespace AIExamForge.UnitTests.Domain;

public sealed class PromptPayloadTests
{
    [Fact]
    public void GetJsonSchemaInstruction_ContainsHardConstraints()
    {
        var payload = new PromptPayload
        {
            SystemPrompt = "sys",
            UserPrompt = "user",
            QuestionCount = 1,
            ItemTypes = new[] { "M1" },
            SourceUrls = new[] { "https://learn.microsoft.com/" },
            RenderMode = "Study"
        };

        var instruction = payload.GetJsonSchemaInstruction();

        instruction.Should().Contain("Respond ONLY with a valid JSON object");
        instruction.Should().Contain("Do NOT include any text before or after the JSON");
        instruction.Should().Contain("\"questions\"");
    }

    [Fact]
    public void EstimatedTokenCount_IsRoughlyCharsDiv4()
    {
        var payload = new PromptPayload
        {
            SystemPrompt = new string('a', 40),
            UserPrompt = new string('b', 40),
            QuestionCount = 1,
            ItemTypes = new[] { "M1" },
            SourceUrls = Array.Empty<string>(),
            RenderMode = "Study"
        };

        payload.EstimatedTokenCount.Should().Be((40 + 40) / 4);
    }
}
