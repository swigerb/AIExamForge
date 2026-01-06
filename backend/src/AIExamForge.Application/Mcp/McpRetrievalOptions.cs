namespace AIExamForge.Application.Mcp;

/// <summary>
/// Configuration options for MCP retrieval behavior.
/// </summary>
public sealed class McpRetrievalOptions
{
    public const string SectionName = "McpRetrieval";

    /// <summary>
    /// Controls when code samples are retrieved via microsoft_code_sample_search.
    /// Allowed values: Auto, Always, Never.
    /// </summary>
    public string CodeSampleMode { get; set; } = "Auto";

    /// <summary>
    /// Maximum number of code samples to append to retrieved content.
    /// </summary>
    public int MaxCodeSamples { get; set; } = 3;

    /// <summary>
    /// Maximum number of characters to keep per code sample.
    /// </summary>
    public int MaxCharsPerCodeSample { get; set; } = 2000;
}
