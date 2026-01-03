namespace AIExamForge.Application.Documents.Interfaces;

/// <summary>
/// Interface for blob storage operations.
/// Per FR-001e: Files are stored temporarily in Azure Blob Storage.
/// </summary>
public interface IBlobStorageClient
{
    /// <summary>
    /// Uploads a file to blob storage.
    /// </summary>
    /// <param name="stream">The file content stream.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upload result with blob URI or error message.</returns>
    Task<BlobUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from blob storage.
    /// </summary>
    /// <param name="blobUri">The blob URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The download result with file stream or error message.</returns>
    Task<BlobContentDownloadResult> DownloadAsync(
        string blobUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from blob storage.
    /// </summary>
    /// <param name="blobUri">The blob URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully.</returns>
    Task<bool> DeleteAsync(
        string blobUri,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a blob upload operation.
/// </summary>
public sealed record BlobUploadResult
{
    public bool Success { get; init; }
    public string? Uri { get; init; }
    public string? ErrorMessage { get; init; }

    public static BlobUploadResult Successful(string uri)
        => new() { Success = true, Uri = uri };

    public static BlobUploadResult Failed(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Result of a blob download operation.
/// </summary>
public sealed record BlobContentDownloadResult
{
    public bool Success { get; init; }
    public Stream? Content { get; init; }
    public string? ContentType { get; init; }
    public string? ErrorMessage { get; init; }

    public static BlobContentDownloadResult Successful(Stream content, string contentType)
        => new() { Success = true, Content = content, ContentType = contentType };

    public static BlobContentDownloadResult Failed(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };
}
