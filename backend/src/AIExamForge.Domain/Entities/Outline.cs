namespace AIExamForge.Domain.Entities;

/// <summary>
/// Represents an exam outline provided by the user.
/// Per data-model.md: A structured text document containing sections and subsections.
/// </summary>
public sealed class Outline
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public string? Title { get; private set; }
    public string RawContent { get; private set; } = null!;
    public int ContentSizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ParsedAt { get; private set; }

    /// <summary>
    /// Root nodes of the outline hierarchy.
    /// </summary>
    public IReadOnlyList<OutlineNode> RootNodes => _rootNodes.AsReadOnly();
    private readonly List<OutlineNode> _rootNodes = [];

    /// <summary>
    /// Associated file upload, if outline was provided via file.
    /// </summary>
    public Guid? FileUploadId { get; private set; }

    private Outline() { } // EF Core constructor

    public static Outline Create(string userId, string rawContent, string? title = null, Guid? fileUploadId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(rawContent, nameof(rawContent));

        var contentSize = System.Text.Encoding.UTF8.GetByteCount(rawContent);
        const int maxSizeBytes = 500 * 1024; // 500 KB per FR-024

        if (contentSize > maxSizeBytes)
        {
            throw new ArgumentException($"Outline content exceeds maximum size of {maxSizeBytes / 1024} KB.");
        }

        return new Outline
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            RawContent = rawContent,
            ContentSizeBytes = contentSize,
            CreatedAt = DateTimeOffset.UtcNow,
            FileUploadId = fileUploadId
        };
    }

    public void SetParsedNodes(IEnumerable<OutlineNode> rootNodes)
    {
        _rootNodes.Clear();
        _rootNodes.AddRange(rootNodes);
        ParsedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Finds a node by ID in the entire hierarchy.
    /// </summary>
    public OutlineNode? FindNode(Guid nodeId)
    {
        foreach (var root in _rootNodes)
        {
            var found = FindNodeRecursive(root, nodeId);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static OutlineNode? FindNodeRecursive(OutlineNode node, Guid nodeId)
    {
        if (node.Id == nodeId)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var found = FindNodeRecursive(child, nodeId);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all leaf nodes (nodes without children).
    /// </summary>
    public IEnumerable<OutlineNode> GetLeafNodes()
    {
        return _rootNodes.SelectMany(GetLeafNodesRecursive);
    }

    /// <summary>
    /// Gets all nodes in the outline hierarchy (flattened).
    /// </summary>
    public IEnumerable<OutlineNode> GetAllNodes()
    {
        return _rootNodes.SelectMany(GetAllNodesRecursive);
    }

    private static IEnumerable<OutlineNode> GetAllNodesRecursive(OutlineNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in GetAllNodesRecursive(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<OutlineNode> GetLeafNodesRecursive(OutlineNode node)
    {
        if (!node.Children.Any())
        {
            yield return node;
        }
        else
        {
            foreach (var child in node.Children)
            {
                foreach (var leaf in GetLeafNodesRecursive(child))
                {
                    yield return leaf;
                }
            }
        }
    }
}
