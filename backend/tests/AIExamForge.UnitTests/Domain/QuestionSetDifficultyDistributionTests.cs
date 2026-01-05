using AIExamForge.Domain.Entities;
using AIExamForge.Domain.ValueObjects;
using FluentAssertions;

namespace AIExamForge.UnitTests.Domain;

public sealed class QuestionSetDifficultyDistributionTests
{
    [Fact]
    public void GetActualDifficultyDistribution_WhenEmpty_ReturnsDefault()
    {
        var set = QuestionSet.Create("user1", Guid.NewGuid());

        var dist = set.GetActualDifficultyDistribution();

        dist.Should().Be(DifficultyDistribution.Default);
    }

    [Fact]
    public void GetActualDifficultyDistribution_SumsTo100_WithRemainderInHard()
    {
        var set = QuestionSet.Create("user1", Guid.NewGuid());
        var setId = set.Id;

        set.AddQuestion(CreateQuestion(setId, QuestionDifficulty.Easy, orderIndex: 0));
        set.AddQuestion(CreateQuestion(setId, QuestionDifficulty.Medium, orderIndex: 1));
        set.AddQuestion(CreateQuestion(setId, QuestionDifficulty.Hard, orderIndex: 2));

        var dist = set.GetActualDifficultyDistribution();

        (dist.EasyPercent + dist.MediumPercent + dist.HardPercent).Should().Be(100);
        dist.EasyPercent.Should().Be(33);
        dist.MediumPercent.Should().Be(33);
        dist.HardPercent.Should().Be(34);
    }

    private static Question CreateQuestion(Guid questionSetId, QuestionDifficulty difficulty, int orderIndex)
    {
        var itemType = ItemTypeCode.M1;
        var tags = QuestionTags.Create("AZ", "Obj", "Sec", topics: new[] { "t1" });
        var scoring = QuestionScoring.Dichotomous(maxPoints: 1);

        return Question.Create(
            itemType: itemType,
            title: "A valid title",
            stem: "This is a sufficiently long stem for testing.",
            difficulty: difficulty,
            tags: tags,
            scoring: scoring,
            contentJson: "{\"prompt\":\"x\",\"options\":[\"a\",\"b\",\"c\",\"d\"],\"correctIndex\":0}",
            questionSetId: questionSetId,
            orderIndex: orderIndex);
    }
}
