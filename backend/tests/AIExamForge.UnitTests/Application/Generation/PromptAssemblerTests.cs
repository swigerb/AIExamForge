using AIExamForge.Application.Generation;
using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Interfaces;
using AIExamForge.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIExamForge.UnitTests.Application.Generation;

public sealed class PromptAssemblerTests
{
    [Fact]
    public async Task AssembleAsync_BuildsUserPromptWithExpectedSections_AndDistinctItemTypes()
    {
        var registry = new Mock<IItemTypeRegistry>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PromptAssembler>>();

        registry.Setup(r => r.GetPrompt("M1")).Returns("Prompt M1");
        registry.Setup(r => r.GetPrompt("SC")).Throws(new InvalidOperationException("missing prompt"));

        var sut = new PromptAssembler(registry.Object, logger.Object);

        var request = new GenerationRequestDto
        {
            OutlineId = Guid.NewGuid(),
            SelectedNodeIds = new[] { Guid.NewGuid() },
            QuestionCount = 3,
            QuestionTypes = new[] { "M1", "M1", "SC" },
            RenderMode = RenderMode.Study,
            Difficulty = null,
            ToneProfile = null
        };

        var outlineId = Guid.NewGuid();
        var outlineNodes = new List<OutlineNode>
        {
            OutlineNode.Create(outlineId, title: "Root", level: 1, sortOrder: 0, content: "Root content")
        };

        var mcpContent = new McpContentCollection
        {
            Items =
            [
                McpContent.Create("q1", "c1", new[] { "https://learn.microsoft.com/a" }, 10),
                McpContent.Create("q2", "c2", new[] { "https://learn.microsoft.com/a", "https://learn.microsoft.com/b" }, 20)
            ]
        };

        var payload = await sut.AssembleAsync(request, outlineNodes, mcpContent);

        payload.QuestionCount.Should().Be(3);
        payload.ItemTypes.Should().BeEquivalentTo(new[] { "M1", "SC" });
        payload.SourceUrls.Should().BeEquivalentTo(new[] { "https://learn.microsoft.com/a", "https://learn.microsoft.com/b" });
        payload.RenderMode.Should().Be("Study");
        payload.Difficulty.Should().BeNull();

        payload.UserPrompt.Should().Contain("# Exam Writing Guidelines");
        payload.UserPrompt.Should().Contain("# Question Types to Generate");
        payload.UserPrompt.Should().Contain("## M1");
        payload.UserPrompt.Should().Contain("Prompt M1");
        payload.UserPrompt.Should().Contain("## SC");
        payload.UserPrompt.Should().Contain("Generate questions of type SC.");
        payload.UserPrompt.Should().Contain("# Source Content (from Microsoft Learn)");
        payload.UserPrompt.Should().Contain("# q1");
        payload.UserPrompt.Should().Contain("c1");
        payload.UserPrompt.Should().Contain("# Outline Context");
        payload.UserPrompt.Should().Contain("- Section: Root");
        payload.UserPrompt.Should().Contain("Content: Root content");
        payload.UserPrompt.Should().Contain("# Generation Parameters");
        payload.UserPrompt.Should().Contain("- Number of questions: 3");
    }

    [Fact]
    public async Task AssembleAsync_WhenDifficultyAndToneProvided_MapsToPayloadAndIncludesInPrompt()
    {
        var registry = new Mock<IItemTypeRegistry>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PromptAssembler>>();

        registry.Setup(r => r.GetPrompt("M1")).Returns("Prompt M1");

        var sut = new PromptAssembler(registry.Object, logger.Object);

        var request = new GenerationRequestDto
        {
            OutlineId = Guid.NewGuid(),
            SelectedNodeIds = new[] { Guid.NewGuid() },
            QuestionCount = 2,
            QuestionTypes = new[] { "M1" },
            RenderMode = RenderMode.Exam,
            Difficulty = new DifficultyDistributionDto { EasyPercent = 50, MediumPercent = 25, HardPercent = 25 },
            ToneProfile = ToneProfile.Professional
        };

        var outlineNodes = new List<OutlineNode>();
        var mcpContent = McpContentCollection.Empty;

        var payload = await sut.AssembleAsync(request, outlineNodes, mcpContent);

        payload.RenderMode.Should().Be("Exam");
        payload.ToneProfile.Should().Be("Professional");
        payload.Difficulty.Should().NotBeNull();
        payload.Difficulty!.EasyPercent.Should().Be(50);
        payload.Difficulty.MediumPercent.Should().Be(25);
        payload.Difficulty.HardPercent.Should().Be(25);

        payload.UserPrompt.Should().Contain("- Difficulty distribution: 50% easy, 25% medium, 25% hard");
        payload.UserPrompt.Should().Contain("- Tone: Professional");
    }
}
