using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Models;

namespace AIExamForge.Application.Generation.Interfaces;

/// <summary>
/// Interface for assembling prompts for question generation.
/// Composes system prompt, exam style prompt, and item-type-specific prompts.
/// </summary>
public interface IPromptAssembler
{
    /// <summary>
    /// Assembles a complete prompt payload for question generation.
    /// </summary>
    /// <param name="request">The generation request with parameters.</param>
    /// <param name="outlineNodes">The selected outline nodes.</param>
    /// <param name="mcpContent">The content retrieved from MCP.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assembled prompt payload ready for LLM.</returns>
    Task<PromptPayload> AssembleAsync(
        GenerationRequestDto request,
        IReadOnlyList<OutlineNode> outlineNodes,
        McpContentCollection mcpContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the system prompt that enforces MCP-exclusive sourcing.
    /// </summary>
    /// <returns>The system prompt content.</returns>
    string GetSystemPrompt();

    /// <summary>
    /// Gets the exam writing style prompt.
    /// </summary>
    /// <returns>The exam style prompt content.</returns>
    string GetExamStylePrompt();

    /// <summary>
    /// Gets the prompt fragment for a specific item type.
    /// </summary>
    /// <param name="itemTypeCode">The item type code (e.g., "M1", "BA").</param>
    /// <returns>The item type prompt content.</returns>
    string GetItemTypePrompt(string itemTypeCode);
}
