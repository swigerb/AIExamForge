using AIExamForge.Domain.ValueObjects;
using FluentAssertions;

namespace AIExamForge.UnitTests.Domain;

public sealed class ItemTypeCodeTests
{
    [Fact]
    public void Create_WithValidCode_NormalizesToUpper()
    {
        var code = ItemTypeCode.Create("m1");

        code.Code.Should().Be("M1");
        code.ToString().Should().Be("M1");
    }

    [Fact]
    public void Create_WithInvalidCode_ThrowsArgumentException()
    {
        var act = () => ItemTypeCode.Create("NOT_A_REAL_CODE");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid item type code*")
            .And.ParamName.Should().Be("code");
    }

    [Fact]
    public void TryCreate_WithNullOrWhitespace_ReturnsFalse()
    {
        ItemTypeCode.TryCreate(" ", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryCreate_WithValidCode_ReturnsTrueAndResult()
    {
        ItemTypeCode.TryCreate("choose_n", out var result).Should().BeTrue();
        result.Should().NotBeNull();
        result!.Code.Should().Be("CHOOSE_N");
    }

    [Fact]
    public void IsDichotomous_MatchesKnownSet()
    {
        ItemTypeCode.M1.IsDichotomous.Should().BeTrue();
        ItemTypeCode.BA.IsDichotomous.Should().BeTrue();
        ItemTypeCode.DA.IsDichotomous.Should().BeTrue();
        ItemTypeCode.SC.IsDichotomous.Should().BeTrue();
        ItemTypeCode.DOMC.IsDichotomous.Should().BeTrue();

        ItemTypeCode.CA.IsDichotomous.Should().BeFalse();
        ItemTypeCode.CA.IsPolytomous.Should().BeTrue();
    }
}
