using System.Text.RegularExpressions;
using AIExamForge.Application.Parsing.Interfaces;
using AIExamForge.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Application.Parsing;

/// <summary>
/// Parses plain text into hierarchical outline structure.
/// Supports markdown headings, indentation, and numbered outlines.
/// </summary>
public sealed partial class OutlineParser : IOutlineParser
{
    private readonly ILogger<OutlineParser> _logger;

    public OutlineParser(ILogger<OutlineParser> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<OutlineNode> Parse(string text, Guid outlineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));

        _logger.LogDebug("Parsing outline text of length {Length}", text.Length);

        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var rootNodes = new List<OutlineNode>();
        var nodeStack = new Stack<(OutlineNode Node, int ParsedLevel)>();
        var sortOrderAtLevel = new Dictionary<int, int>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var (parsedLevel, title, content) = ParseLine(line);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            // Find parent based on parsed level
            while (nodeStack.Count > 0 && nodeStack.Peek().ParsedLevel >= parsedLevel)
            {
                nodeStack.Pop();
            }

            // Determine the actual level based on parent (ensuring it's parent level + 1)
            int actualLevel;
            Guid? parentId;
            
            if (nodeStack.Count == 0)
            {
                // Root level node
                actualLevel = 1;
                parentId = null;
            }
            else
            {
                // Child of current parent - level must be parent level + 1
                var parent = nodeStack.Peek().Node;
                actualLevel = parent.Level + 1;
                parentId = parent.Id;
            }

            // Get sort order for this actual level
            if (!sortOrderAtLevel.TryGetValue(actualLevel, out var sortOrder))
            {
                sortOrder = 0;
            }
            sortOrderAtLevel[actualLevel] = sortOrder + 1;
            
            // Reset sort order for deeper levels
            foreach (var key in sortOrderAtLevel.Keys.Where(k => k > actualLevel).ToList())
            {
                sortOrderAtLevel[key] = 0;
            }

            var node = OutlineNode.Create(
                outlineId,
                title.Trim(),
                actualLevel,
                sortOrder,
                parentId,
                content?.Trim());

            if (parentId == null)
            {
                rootNodes.Add(node);
            }
            else
            {
                var parent = nodeStack.Peek().Node;
                parent.AddChild(node);
            }

            // Push with parsed level (for hierarchy detection) not actual level
            nodeStack.Push((node, parsedLevel));
        }

        _logger.LogInformation("Parsed {NodeCount} root nodes from outline text", rootNodes.Count);

        return rootNodes.AsReadOnly();
    }

    public bool CanParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
        
        // Check if at least one line can be parsed as a heading/section
        return lines.Any(line =>
        {
            var (_, title, _) = ParseLine(line);
            return !string.IsNullOrWhiteSpace(title);
        });
    }

    private static (int Level, string Title, string? Content) ParseLine(string line)
    {
        // Try markdown heading (# ## ### etc.)
        var markdownMatch = MarkdownHeadingRegex().Match(line);
        if (markdownMatch.Success)
        {
            var level = markdownMatch.Groups[1].Value.Length;
            var title = markdownMatch.Groups[2].Value;
            return (level, title, null);
        }

        // Try numbered outline (1. 1.1 1.1.1 etc.)
        var numberedMatch = NumberedOutlineRegex().Match(line);
        if (numberedMatch.Success)
        {
            var numberPart = numberedMatch.Groups[1].Value;
            var level = numberPart.Count(c => c == '.') + 1;
            var title = numberedMatch.Groups[2].Value;
            return (level, title, null);
        }

        // Try lettered outline (a. b. c. or A. B. C.)
        var letteredMatch = LetteredOutlineRegex().Match(line);
        if (letteredMatch.Success)
        {
            var title = letteredMatch.Groups[2].Value;
            return (2, title, null); // Letters are typically second level
        }

        // Try roman numerals (i. ii. iii. iv. or I. II. III. IV.)
        var romanMatch = RomanNumeralRegex().Match(line);
        if (romanMatch.Success)
        {
            var title = romanMatch.Groups[2].Value;
            return (1, title, null); // Roman numerals are typically top level
        }

        // Try indentation-based (tabs or spaces)
        var indentMatch = IndentationRegex().Match(line);
        if (indentMatch.Success)
        {
            var indent = indentMatch.Groups[1].Value;
            var content = indentMatch.Groups[2].Value;
            
            // Calculate level based on indentation
            // 1 tab = 1 level, 4 spaces = 1 level, 2 spaces = 1 level
            var tabCount = indent.Count(c => c == '\t');
            var spaceCount = indent.Count(c => c == ' ');
            var indentLevel = tabCount + (spaceCount / 4) + (spaceCount % 4 / 2);
            
            var level = Math.Max(1, indentLevel + 1);
            
            // Strip any bullet points
            var bulletMatch = BulletPointRegex().Match(content);
            if (bulletMatch.Success)
            {
                content = bulletMatch.Groups[1].Value;
            }
            
            return (level, content, null);
        }

        // No pattern matched - treat as level 1 text
        // Strip any bullet points
        var bulletMatch2 = BulletPointRegex().Match(line);
        if (bulletMatch2.Success)
        {
            return (1, bulletMatch2.Groups[1].Value, null);
        }

        return (1, line, null);
    }

    // Regex patterns as partial methods for source generation
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex MarkdownHeadingRegex();

    [GeneratedRegex(@"^(\d+(?:\.\d+)*\.?)\s+(.+)$")]
    private static partial Regex NumberedOutlineRegex();

    [GeneratedRegex(@"^([a-zA-Z])[\.\)]\s+(.+)$")]
    private static partial Regex LetteredOutlineRegex();

    [GeneratedRegex(@"^([ivxlcdmIVXLCDM]+)[\.\)]\s+(.+)$")]
    private static partial Regex RomanNumeralRegex();

    [GeneratedRegex(@"^([\t ]+)(.+)$")]
    private static partial Regex IndentationRegex();

    [GeneratedRegex(@"^[-*•]\s*(.+)$")]
    private static partial Regex BulletPointRegex();
}
