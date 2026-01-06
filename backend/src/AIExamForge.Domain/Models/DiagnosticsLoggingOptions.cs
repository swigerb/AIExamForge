namespace AIExamForge.Domain.Models;

public sealed class DiagnosticsLoggingOptions
{
    public const string SectionName = "Diagnostics:Logging";

    /// <summary>
    /// Enables verbose diagnostic logging of prompts, MCP calls, and LLM responses.
    /// Intended for Development only.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Maximum characters to emit for any large payload (prompt/body/response).
    /// </summary>
    public int MaxChars { get; set; } = 8000;

    public bool LogPromptBodies { get; set; } = true;

    public bool LogMcpRequestBodies { get; set; } = true;
    public bool LogMcpResponseBodies { get; set; } = false;

    public bool LogLlmRequestBodies { get; set; } = true;
    public bool LogLlmResponseBodies { get; set; } = true;
}
