using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;
using FluentAssertions;

namespace AIExamForge.UnitTests.Domain;

public sealed class GenerationRequestTests
{
    [Fact]
    public void Create_WithValidInputs_SetsDefaults()
    {
        var outlineId = Guid.NewGuid();
        var nodeIds = new[] { Guid.NewGuid() };
        var itemTypes = new[] { ItemTypeCode.M1, ItemTypeCode.BA };

        var request = GenerationRequest.Create(
            userId: "user1",
            outlineId: outlineId,
            selectedNodeIds: nodeIds,
            questionCount: 5,
            itemTypes: itemTypes);

        request.UserId.Should().Be("user1");
        request.OutlineId.Should().Be(outlineId);
        request.QuestionCount.Should().Be(5);
        request.SelectedNodeIds.Should().BeEquivalentTo(nodeIds);
        request.ItemTypes.Should().BeEquivalentTo(itemTypes);
        request.DifficultyDistribution.Should().Be(DifficultyDistribution.Default);
        request.Status.Should().Be(GenerationStatus.Pending);
        request.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNoSelectedNodes_Throws()
    {
        var act = () => GenerationRequest.Create(
            userId: "user1",
            outlineId: Guid.NewGuid(),
            selectedNodeIds: Array.Empty<Guid>(),
            questionCount: 5,
            itemTypes: new[] { ItemTypeCode.M1 });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one outline node must be selected*")
            .And.ParamName.Should().Be("selectedNodeIds");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Create_WhenQuestionCountOutOfRange_Throws(int questionCount)
    {
        var act = () => GenerationRequest.Create(
            userId: "user1",
            outlineId: Guid.NewGuid(),
            selectedNodeIds: new[] { Guid.NewGuid() },
            questionCount: questionCount,
            itemTypes: new[] { ItemTypeCode.M1 });

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*between 1 and*")
            .And.ParamName.Should().Be("questionCount");
    }

    [Fact]
    public void StateTransitions_FollowExpectedFlow()
    {
        var request = GenerationRequest.Create(
            userId: "user1",
            outlineId: Guid.NewGuid(),
            selectedNodeIds: new[] { Guid.NewGuid() },
            questionCount: 3,
            itemTypes: new[] { ItemTypeCode.M1 });

        request.StartContentRetrieval();
        request.Status.Should().Be(GenerationStatus.RetrievingContent);

        request.StartGeneration();
        request.Status.Should().Be(GenerationStatus.Generating);

        request.StartValidation(questionsGenerated: 3);
        request.Status.Should().Be(GenerationStatus.Validating);
        request.QuestionsGenerated.Should().Be(3);

        var setId = Guid.NewGuid();
        request.Complete(setId, questionsValidated: 3);
        request.Status.Should().Be(GenerationStatus.Completed);
        request.QuestionSetId.Should().Be(setId);
        request.QuestionsValidated.Should().Be(3);
    }
}
