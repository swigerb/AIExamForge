namespace AIExamForge.Domain.ValueObjects;

/// <summary>
/// Generation request status per data-model.md.
/// </summary>
public enum GenerationStatus
{
    /// <summary>Request submitted, waiting to start</summary>
    Pending = 0,

    /// <summary>MCP content retrieval in progress</summary>
    RetrievingContent = 1,

    /// <summary>Question generation in progress</summary>
    Generating = 2,

    /// <summary>Validating generated questions</summary>
    Validating = 3,

    /// <summary>Generation completed successfully</summary>
    Completed = 4,

    /// <summary>Generation completed with some questions failed validation</summary>
    CompletedPartial = 5,

    /// <summary>Generation failed</summary>
    Failed = 6,

    /// <summary>Generation cancelled by user</summary>
    Cancelled = 7
}
