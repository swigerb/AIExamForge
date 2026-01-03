using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Entities;

/// <summary>
/// Represents an export record tracking exported question sets per data-model.md.
/// </summary>
public sealed class ExportRecord
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public Guid QuestionSetId { get; private set; }
    public ExportFormat Format { get; private set; }

    /// <summary>
    /// The render mode used for this export.
    /// </summary>
    public RenderMode RenderMode { get; private set; }

    /// <summary>
    /// Blob storage URI for the exported file.
    /// </summary>
    public string BlobStorageUri { get; private set; } = null!;

    /// <summary>
    /// Original file name for download.
    /// </summary>
    public string FileName { get; private set; } = null!;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; private set; }

    /// <summary>
    /// Number of questions included in the export.
    /// </summary>
    public int QuestionCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Number of times this export has been downloaded.
    /// </summary>
    public int DownloadCount { get; private set; }

    /// <summary>
    /// Last download time.
    /// </summary>
    public DateTimeOffset? LastDownloadedAt { get; private set; }

    /// <summary>
    /// Expiration time for the export file (optional cleanup).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// Whether the export file has been deleted.
    /// </summary>
    public bool IsDeleted { get; private set; }

    private ExportRecord() { } // EF Core constructor

    public static ExportRecord Create(
        string userId,
        Guid questionSetId,
        ExportFormat format,
        RenderMode renderMode,
        string blobStorageUri,
        string fileName,
        long fileSizeBytes,
        int questionCount,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(blobStorageUri, nameof(blobStorageUri));
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));

        if (fileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "File size must be positive.");
        }

        if (questionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questionCount), "Question count must be positive.");
        }

        return new ExportRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuestionSetId = questionSetId,
            Format = format,
            RenderMode = renderMode,
            BlobStorageUri = blobStorageUri,
            FileName = fileName,
            FileSizeBytes = fileSizeBytes,
            QuestionCount = questionCount,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    public void RecordDownload()
    {
        DownloadCount++;
        LastDownloadedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsDeleted()
    {
        IsDeleted = true;
    }

    /// <summary>
    /// Checks if the export has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Gets the content type for the export format.
    /// </summary>
    public string ContentType => Format.GetContentType();
}
