using AIExamForge.Application.Documents.Interfaces;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIExamForge.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage client for file uploads.
/// Per FR-001e: Files are stored temporarily in Azure Blob Storage.
/// </summary>
public sealed class BlobStorageClient : IBlobStorageClient
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<BlobStorageClient> _logger;
    private readonly BlobStorageOptions _options;

    public BlobStorageClient(
        IOptions<BlobStorageOptions> options,
        ILogger<BlobStorageClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
    }

    /// <inheritdoc />
    public async Task<BlobUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType, nameof(contentType));

        try
        {
            // Ensure container exists
            await _containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);

            // Generate unique blob name with path
            var blobName = GenerateBlobName(fileName);
            var blobClient = _containerClient.GetBlobClient(blobName);

            _logger.LogInformation("Uploading blob: {BlobName}, Content-Type: {ContentType}", blobName, contentType);

            // Upload with headers
            var headers = new BlobHttpHeaders { ContentType = contentType };
            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions { HttpHeaders = headers },
                cancellationToken);

            _logger.LogInformation("Successfully uploaded blob: {BlobName}", blobName);

            return BlobUploadResult.Successful(blobClient.Uri.ToString());
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to upload blob: {FileName}, Status: {Status}", fileName, ex.Status);
            return BlobUploadResult.Failed($"Storage error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading blob: {FileName}", fileName);
            return BlobUploadResult.Failed($"Upload failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<BlobContentDownloadResult> DownloadAsync(
        string blobUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri, nameof(blobUri));

        try
        {
            var blobName = ExtractBlobNameFromUri(blobUri);
            var blobClient = _containerClient.GetBlobClient(blobName);

            _logger.LogInformation("Downloading blob: {BlobName}", blobName);

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            var contentType = response.Value.Details.ContentType ?? "application/octet-stream";

            return BlobContentDownloadResult.Successful(response.Value.Content, contentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Blob not found: {BlobUri}", blobUri);
            return BlobContentDownloadResult.Failed("File not found");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to download blob: {BlobUri}, Status: {Status}", blobUri, ex.Status);
            return BlobContentDownloadResult.Failed($"Storage error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading blob: {BlobUri}", blobUri);
            return BlobContentDownloadResult.Failed($"Download failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        string blobUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri, nameof(blobUri));

        try
        {
            var blobName = ExtractBlobNameFromUri(blobUri);
            var blobClient = _containerClient.GetBlobClient(blobName);

            _logger.LogInformation("Deleting blob: {BlobName}", blobName);

            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            
            if (response.Value)
            {
                _logger.LogInformation("Successfully deleted blob: {BlobName}", blobName);
            }
            else
            {
                _logger.LogWarning("Blob did not exist: {BlobName}", blobName);
            }

            return response.Value;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to delete blob: {BlobUri}, Status: {Status}", blobUri, ex.Status);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting blob: {BlobUri}", blobUri);
            return false;
        }
    }

    /// <summary>
    /// Generates a unique blob name with date-based path.
    /// </summary>
    private static string GenerateBlobName(string fileName)
    {
        var now = DateTimeOffset.UtcNow;
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var sanitizedFileName = SanitizeFileName(fileName);
        
        return $"uploads/{now:yyyy}/{now:MM}/{now:dd}/{uniqueId}-{sanitizedFileName}";
    }

    /// <summary>
    /// Sanitizes a file name for blob storage.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 100 ? sanitized[..100] : sanitized;
    }

    /// <summary>
    /// Extracts blob name from a full blob URI.
    /// </summary>
    private string ExtractBlobNameFromUri(string blobUri)
    {
        // Handle local:// URIs (used when blob storage not configured)
        if (blobUri.StartsWith("local://", StringComparison.OrdinalIgnoreCase))
        {
            return blobUri["local://".Length..];
        }

        // Handle Azure blob URIs
        var uri = new Uri(blobUri);
        var pathSegments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
        
        // Path is: /{container}/{blobName}
        return pathSegments.Length > 1 ? pathSegments[1] : pathSegments[0];
    }
}

/// <summary>
/// Configuration options for Azure Blob Storage.
/// </summary>
public sealed class BlobStorageOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "BlobStorage";

    /// <summary>
    /// Azure Storage connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container name for file uploads.
    /// </summary>
    public string ContainerName { get; set; } = "exam-uploads";
}
