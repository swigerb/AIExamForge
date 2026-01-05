using System.Text.Json;
using System.Text.Json.Serialization;
using AIExamForge.Application.Generation.DTOs;
using FluentAssertions;

namespace AIExamForge.UnitTests.Application.Generation;

public sealed class GenerationRequestDtoJsonTests
{
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new RenderModeJsonConverter());
        options.Converters.Add(new ToneProfileJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [Theory]
    [InlineData("Study")]
    [InlineData("study")]
    [InlineData("study-mode")]
    [InlineData("Study Mode")]
    [InlineData("StudyMode")]
    [InlineData("Exam")]
    [InlineData("exam")]
    [InlineData("exam-mode")]
    [InlineData("Exam Mode")]
    [InlineData("ExamMode")]
    public void RenderMode_Deserializes_FromCommonStringVariants(string value)
    {
        var json = JsonSerializer.Serialize(new
        {
            outlineId = "00000000-0000-0000-0000-000000000001",
            selectedNodeIds = new[] { "00000000-0000-0000-0000-000000000002" },
            questionCount = 1,
            questionTypes = new[] { "M1" },
            renderMode = value
        });

        var dto = JsonSerializer.Deserialize<GenerationRequestDto>(json, CreateOptions());

        dto.Should().NotBeNull();
        dto!.RenderMode.Should().BeOneOf(RenderMode.Exam, RenderMode.Study);
    }

    [Theory]
    [InlineData("concise", ToneProfile.Concise)]
    [InlineData("professional", ToneProfile.Professional)]
    [InlineData("exam-like", ToneProfile.ExamLike)]
    [InlineData("ExamLike", ToneProfile.ExamLike)]
    public void ToneProfile_Deserializes_FromCommonStringVariants(string value, ToneProfile expected)
    {
        var json = JsonSerializer.Serialize(new
        {
            outlineId = "00000000-0000-0000-0000-000000000001",
            selectedNodeIds = new[] { "00000000-0000-0000-0000-000000000002" },
            questionCount = 1,
            questionTypes = new[] { "M1" },
            toneProfile = value
        });

        var dto = JsonSerializer.Deserialize<GenerationRequestDto>(json, CreateOptions());

        dto.Should().NotBeNull();
        dto!.ToneProfile.Should().Be(expected);
    }
}
