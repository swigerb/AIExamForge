namespace AIExamForge.Domain.Entities;

/// <summary>
/// Represents a section or subsection in the outline hierarchy.
/// Per data-model.md: Hierarchical structure with max depth of 10 levels.
/// </summary>
public sealed class OutlineNode
{
    public const int MaxNestingDepth = 10;

    public Guid Id { get; private set; }
    public Guid OutlineId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Content { get; private set; }
    public int Level { get; private set; }
    public int SortOrder { get; private set; }

    /// <summary>
    /// Child nodes of this node.
    /// </summary>
    public IReadOnlyList<OutlineNode> Children => _children.AsReadOnly();
    private readonly List<OutlineNode> _children = [];

    private OutlineNode() { } // EF Core constructor

    public static OutlineNode Create(
        Guid outlineId,
        string title,
        int level,
        int sortOrder,
        Guid? parentId = null,
        string? content = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));

        if (level < 1 || level > MaxNestingDepth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                $"Level must be between 1 and {MaxNestingDepth}. Got: {level}");
        }

        return new OutlineNode
        {
            Id = Guid.NewGuid(),
            OutlineId = outlineId,
            ParentId = parentId,
            Title = title,
            Content = content,
            Level = level,
            SortOrder = sortOrder
        };
    }

    public void AddChild(OutlineNode child)
    {
        if (child.Level != Level + 1)
        {
            throw new InvalidOperationException(
                $"Child level must be {Level + 1} for parent at level {Level}. Got: {child.Level}");
        }

        _children.Add(child);
    }

    /// <summary>
    /// Gets the full path of titles from root to this node.
    /// </summary>
    public string GetPath(Outline outline)
    {
        var path = new Stack<string>();
        var current = this;

        while (current != null)
        {
            path.Push(current.Title);
            current = current.ParentId.HasValue ? outline.FindNode(current.ParentId.Value) : null;
        }

        return string.Join(" > ", path);
    }

    /// <summary>
    /// Gets all descendant node IDs including this node.
    /// </summary>
    public IEnumerable<Guid> GetDescendantIds()
    {
        yield return Id;
        foreach (var child in _children)
        {
            foreach (var descendantId in child.GetDescendantIds())
            {
                yield return descendantId;
            }
        }
    }
}
