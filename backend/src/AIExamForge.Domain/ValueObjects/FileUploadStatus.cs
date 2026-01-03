namespace AIExamForge.Domain.ValueObjects;

/// <summary>
/// File upload status per data-model.md.
/// </summary>
public enum FileUploadStatus
{
    /// <summary>File uploaded, awaiting processing</summary>
    Uploaded = 0,

    /// <summary>Text extraction in progress</summary>
    Processing = 1,

    /// <summary>Text extraction completed successfully</summary>
    Completed = 2,

    /// <summary>Text extraction failed</summary>
    Failed = 3,

    /// <summary>File deleted (cleanup or user action)</summary>
    Deleted = 4
}
