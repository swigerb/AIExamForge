using AIExamForge.Domain.ValueObjects;
using FluentAssertions;

namespace AIExamForge.UnitTests.Domain;

public sealed class DifficultyDistributionTests
{
    [Fact]
    public void Create_WhenAnyNegative_Throws()
    {
        var act = () => DifficultyDistribution.Create(-1, 50, 51);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void Create_WhenDoesNotSumTo100_Throws()
    {
        var act = () => DifficultyDistribution.Create(30, 50, 10);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must sum to 100*");
    }

    [Fact]
    public void Default_IsValidAndSumsTo100()
    {
        var d = DifficultyDistribution.Default;

        (d.EasyPercent + d.MediumPercent + d.HardPercent).Should().Be(100);
    }

    [Fact]
    public void CalculateCounts_DistributesRemainderToMedium()
    {
        var d = DifficultyDistribution.Create(30, 50, 20);

        var (easy, medium, hard) = d.CalculateCounts(10);

        easy.Should().Be(3);
        medium.Should().Be(5);
        hard.Should().Be(2);
    }
}
