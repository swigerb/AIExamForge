using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Repositories;
using AIExamForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AIExamForge.Infrastructure.Repositories;

public class OutlineRepository : IOutlineRepository
{
    private readonly AIExamForgeDbContext _context;

    public OutlineRepository(AIExamForgeDbContext context)
    {
        _context = context;
    }

    public async Task<Outline?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // RootNodes are a backing field; materialize the tree from OutlineNodes.
        var outline = await _context.Outlines
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (outline == null)
        {
            return null;
        }

        var nodes = await _context.OutlineNodes
            .AsNoTracking()
            .Where(n => n.OutlineId == id)
            .OrderBy(n => n.ParentId)
            .ThenBy(n => n.SortOrder)
            .ToListAsync(cancellationToken);

        if (nodes.Count == 0)
        {
            return outline;
        }

        // Rebuild in-memory hierarchy based on ParentId / SortOrder.
        var rootNodes = new List<OutlineNode>();
        var childrenByParentId = new Dictionary<Guid, List<OutlineNode>>();

        foreach (var node in nodes)
        {
            if (node.ParentId is null)
            {
                rootNodes.Add(node);
                continue;
            }

            if (!childrenByParentId.TryGetValue(node.ParentId.Value, out var children))
            {
                children = [];
                childrenByParentId[node.ParentId.Value] = children;
            }

            children.Add(node);
        }

        rootNodes.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        foreach (var children in childrenByParentId.Values)
        {
            children.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        }

        foreach (var parent in nodes)
        {
            if (childrenByParentId.TryGetValue(parent.Id, out var children))
            {
                foreach (var child in children)
                {
                    parent.AddChild(child);
                }
            }
        }

        outline.SetParsedNodes(rootNodes);
        return outline;
    }

    public async Task<IReadOnlyList<Outline>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Outlines
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Outline>> GetByUserIdPagedAsync(string userId, int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _context.Outlines
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Outlines
            .CountAsync(o => o.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Outline outline, CancellationToken cancellationToken = default)
    {
        await _context.Outlines.AddAsync(outline, cancellationToken);

        // RootNodes are ignored by EF configuration, so persist nodes explicitly.
        var nodes = outline.GetAllNodes().ToList();
        if (nodes.Count > 0)
        {
            await _context.OutlineNodes.AddRangeAsync(nodes, cancellationToken);
        }
    }

    public Task UpdateAsync(Outline outline, CancellationToken cancellationToken = default)
    {
        _context.Outlines.Update(outline);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var outline = await _context.Outlines.FindAsync([id], cancellationToken);
        if (outline != null)
        {
            var nodes = await _context.OutlineNodes
                .Where(n => n.OutlineId == id)
                .ToListAsync(cancellationToken);
            if (nodes.Count > 0)
            {
                _context.OutlineNodes.RemoveRange(nodes);
            }
            _context.Outlines.Remove(outline);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Outlines.AnyAsync(o => o.Id == id, cancellationToken);
    }
}
