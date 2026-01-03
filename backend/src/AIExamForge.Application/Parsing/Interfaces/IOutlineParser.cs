using AIExamForge.Domain.Entities;

namespace AIExamForge.Application.Parsing.Interfaces;

/// <summary>
/// Parses plain text into hierarchical outline structure.
/// Detects headings, indentation, and numbering patterns per spec.
/// </summary>
public interface IOutlineParser
{
    /// <summary>
    /// Parses plain text into a list of root OutlineNode objects with hierarchical children.
    /// Detects structure based on:
    /// - Markdown headings (# ## ###)
    /// - Indentation (tabs, spaces)
    /// - Numbering patterns (1., 1.1, a., i.)
    /// </summary>
    /// <param name="text">The plain text outline content.</param>
    /// <param name="outlineId">The ID of the parent Outline entity.</param>
    /// <returns>A list of root-level OutlineNode entities with children populated.</returns>
    IReadOnlyList<OutlineNode> Parse(string text, Guid outlineId);

    /// <summary>
    /// Validates that the text can be parsed into a valid structure.
    /// Returns true if the text has at least one detectable section.
    /// </summary>
    bool CanParse(string text);
}
