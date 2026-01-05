using AIExamForge.Domain.Entities;
using FluentAssertions;

namespace AIExamForge.UnitTests.Domain;

public sealed class OutlineNodeTests
{
    [Fact]
    public void Create_EnforcesLevelRange()
    {
        var actLow = () => OutlineNode.Create(Guid.NewGuid(), "Title", level: 0, sortOrder: 0);
        var actHigh = () => OutlineNode.Create(Guid.NewGuid(), "Title", level: OutlineNode.MaxNestingDepth + 1, sortOrder: 0);

        actLow.Should().Throw<ArgumentOutOfRangeException>().And.ParamName.Should().Be("level");
        actHigh.Should().Throw<ArgumentOutOfRangeException>().And.ParamName.Should().Be("level");
    }

    [Fact]
    public void AddChild_RequiresChildLevelToBeParentPlusOne()
    {
        var outlineId = Guid.NewGuid();
        var parent = OutlineNode.Create(outlineId, "Parent", level: 1, sortOrder: 0);
        var invalidChild = OutlineNode.Create(outlineId, "Child", level: 3, sortOrder: 0, parentId: parent.Id);

        var act = () => parent.AddChild(invalidChild);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Child level must be 2*");
    }

    [Fact]
    public void GetDescendantIds_IncludesSelfAndChildren()
    {
        var outlineId = Guid.NewGuid();
        var parent = OutlineNode.Create(outlineId, "Parent", level: 1, sortOrder: 0);
        var child = OutlineNode.Create(outlineId, "Child", level: 2, sortOrder: 0, parentId: parent.Id);

        parent.AddChild(child);

        var ids = parent.GetDescendantIds().ToList();

        ids.Should().Contain(parent.Id);
        ids.Should().Contain(child.Id);
        ids.Should().HaveCount(2);
    }
}
