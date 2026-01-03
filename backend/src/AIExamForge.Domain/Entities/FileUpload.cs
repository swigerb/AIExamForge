using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Entities;

/// <summary>
/// Represents an uploaded file (DOCX or PDF) per FR-001a through FR-001g.
/// Files are stored temporarily and deleted after processing.
/// </summary>
public sealed class FileUpload
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSizeBytes { get; private set; }
    public string BlobStorageUri { get; private set; } = null!;
    public FileUploadStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>
    /// Extracted text content after processing.
    /// </summary>
    public string? ExtractedText { get; private set; }

    /// <summary>
    /// Associated outline created from this file.
    /// </summary>
    public Guid? OutlineId { get; private set; }

    private FileUpload() { } // EF Core constructor

    public static FileUpload Create(
        string userId,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        string blobStorageUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName, nameof(originalFileName));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType, nameof(contentType));
        ArgumentException.ThrowIfNullOrWhiteSpace(blobStorageUri, nameof(blobStorageUri));

        const long maxFileSizeBytes = 10 * 1024 * 1024; // 10 MB per FR-001b
        if (fileSizeBytes > maxFileSizeBytes)
        {
            throw new ArgumentException($"File size exceeds maximum of {maxFileSizeBytes / (1024 * 1024)} MB.");
        }

        if (!IsValidContentType(contentType))
        {
            throw new ArgumentException(
                $"Invalid content type: '{contentType}'. Only .docx and .pdf files are allowed.");
        }

        return new FileUpload
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            BlobStorageUri = blobStorageUri,
            Status = FileUploadStatus.Uploaded,
            UploadedAt = DateTimeOffset.UtcNow
        };
    }

    private static bool IsValidContentType(string contentType)
    {
        return contentType is
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or // .docx
            "application/pdf"; // .pdf
    }

    public void MarkProcessing()
    {
        if (Status != FileUploadStatus.Uploaded)
        {
            throw new InvalidOperationException($"Cannot start processing. Current status: {Status}");
        }

        Status = FileUploadStatus.Processing;
    }

    public void MarkCompleted(string extractedText, Guid outlineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extractedText, nameof(extractedText));

        Status = FileUploadStatus.Completed;
        ExtractedText = extractedText;
        OutlineId = outlineId;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage, nameof(errorMessage));

        Status = FileUploadStatus.Failed;
        ErrorMessage = errorMessage;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDeleted()
    {
        Status = FileUploadStatus.Deleted;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Returns true if the file should be cleaned up (older than 1 hour per FR-001f).
    /// </summary>
    public bool ShouldBeCleanedUp(DateTimeOffset now)
    {
        if (Status == FileUploadStatus.Deleted)
        {
            return false;
        }

        var maxRetentionHours = 1; // Per FR-001f
        return now - UploadedAt > TimeSpan.FromHours(maxRetentionHours);
    }
}
