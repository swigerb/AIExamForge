namespace AIExamForge.Domain.ValueObjects;

/// <summary>
/// Render mode for question exports per FR-049 through FR-055.
/// </summary>
public enum RenderMode
{
    /// <summary>
    /// Exam Mode: No rationales/explanations in exports.
    /// Use for simulating real exam conditions.
    /// </summary>
    Exam = 0,

    /// <summary>
    /// Study Mode: Rationales included in a separate section (never inline).
    /// Use for learning and review. This is the default mode.
    /// </summary>
    Study = 1
}
