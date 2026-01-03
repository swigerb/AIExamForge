using AIExamForge.Application.Parsing.DTOs;
using AIExamForge.Application.Parsing.Interfaces;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Application.Parsing;

/// <summary>
/// Service for parsing and persisting exam outlines.
/// Orchestrates the parsing and database operations.
/// </summary>
public sealed class OutlineService
{
    private readonly IOutlineParser _parser;
    private readonly IOutlineRepository _outlineRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OutlineService> _logger;

    public OutlineService(
        IOutlineParser parser,
        IOutlineRepository outlineRepository,
        IUnitOfWork unitOfWork,
        ILogger<OutlineService> logger)
    {
        _parser = parser;
        _outlineRepository = outlineRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Parses text input and creates a persisted outline.
    /// </summary>
    public async Task<OutlineResponse> ParseAndSaveAsync(
        string userId,
        ParseOutlineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("Parsing outline for user {UserId}, text length: {Length}", 
            userId, request.Text.Length);

        // Create the outline entity
        var outline = Outline.Create(
            userId,
            request.Text,
            request.ExamName);

        // Parse the text into nodes
        var nodes = _parser.Parse(request.Text, outline.Id);
        outline.SetParsedNodes(nodes);

        // Persist
        await _outlineRepository.AddAsync(outline, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created outline {OutlineId} with {NodeCount} root nodes", 
            outline.Id, nodes.Count);

        return MapToResponse(outline);
    }

    /// <summary>
    /// Gets an outline by ID.
    /// </summary>
    public async Task<OutlineResponse?> GetByIdAsync(
        Guid outlineId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var outline = await _outlineRepository.GetByIdAsync(outlineId, cancellationToken);
        
        if (outline == null)
        {
            return null;
        }

        // Verify user owns the outline
        if (outline.UserId != userId)
        {
            _logger.LogWarning("User {UserId} attempted to access outline {OutlineId} owned by {OwnerId}",
                userId, outlineId, outline.UserId);
            return null;
        }

        return MapToResponse(outline);
    }

    /// <summary>
    /// Gets all outlines for a user.
    /// </summary>
    public async Task<IReadOnlyList<OutlineResponse>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var outlines = await _outlineRepository.GetByUserIdAsync(userId, cancellationToken);
        return outlines.Select(MapToResponse).ToList().AsReadOnly();
    }

    private static OutlineResponse MapToResponse(Outline outline)
    {
        return new OutlineResponse
        {
            Id = outline.Id,
            ExamName = outline.Title,
            SourceType = "TextPaste", // TODO: Track source type in entity
            SourceFileName = null,
            Nodes = outline.RootNodes.Select(MapNodeToDto).ToList().AsReadOnly(),
            CreatedAt = outline.CreatedAt
        };
    }

    private static OutlineNodeDto MapNodeToDto(OutlineNode node)
    {
        return new OutlineNodeDto
        {
            Id = node.Id,
            Title = node.Title,
            Content = node.Content,
            Level = node.Level,
            Children = node.Children.Select(MapNodeToDto).ToList().AsReadOnly()
        };
    }
}
