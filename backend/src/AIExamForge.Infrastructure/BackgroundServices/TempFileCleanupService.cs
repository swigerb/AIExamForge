using AIExamForge.Application.Documents.Interfaces;
using AIExamForge.Domain.Repositories;
using AIExamForge.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIExamForge.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that cleans up temporary files after their retention period.
/// Per FR-001f: Files are deleted after 1 hour.
/// </summary>
public sealed class TempFileCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TempFileCleanupService> _logger;
    private readonly TempFileCleanupOptions _options;

    public TempFileCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<TempFileCleanupOptions> options,
        ILogger<TempFileCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TempFileCleanupService started. Cleanup interval: {Interval} minutes, Retention: {Retention} minutes",
            _options.CleanupIntervalMinutes,
            _options.RetentionPeriodMinutes);

        // Wait for application startup before starting cleanup
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredFilesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file cleanup");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.CleanupIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("TempFileCleanupService stopped");
    }

    private async Task CleanupExpiredFilesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting file cleanup check");

        using var scope = _scopeFactory.CreateScope();
        var fileUploadRepository = scope.ServiceProvider.GetRequiredService<IFileUploadRepository>();
        var blobStorageClient = scope.ServiceProvider.GetService<IBlobStorageClient>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTimeOffset.UtcNow;
        var cutoffTime = now.AddMinutes(-_options.RetentionPeriodMinutes);

        // Get files that need cleanup
        var expiredFiles = await fileUploadRepository.GetFilesForCleanupAsync(cutoffTime, cancellationToken);
        
        if (!expiredFiles.Any())
        {
            _logger.LogDebug("No expired files found for cleanup");
            return;
        }

        _logger.LogInformation("Found {Count} expired files for cleanup", expiredFiles.Count());

        var deletedCount = 0;
        var errorCount = 0;

        foreach (var file in expiredFiles)
        {
            try
            {
                // Delete from blob storage if client available and URI is not local
                if (blobStorageClient != null && !file.BlobStorageUri.StartsWith("local://"))
                {
                    var deleted = await blobStorageClient.DeleteAsync(file.BlobStorageUri, cancellationToken);
                    if (!deleted)
                    {
                        _logger.LogWarning("Failed to delete blob for file {FileId}: {Uri}", file.Id, file.BlobStorageUri);
                    }
                }

                // Mark as deleted in database
                file.MarkDeleted();
                deletedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up file {FileId}", file.Id);
                errorCount++;
            }
        }

        // Save all changes
        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("File cleanup completed. Deleted: {DeletedCount}, Errors: {ErrorCount}",
            deletedCount, errorCount);
    }
}

/// <summary>
/// Configuration options for temporary file cleanup.
/// </summary>
public sealed class TempFileCleanupOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TempFileCleanup";

    /// <summary>
    /// Interval between cleanup runs in minutes. Default: 15 minutes.
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// File retention period in minutes. Default: 60 minutes (1 hour per FR-001f).
    /// </summary>
    public int RetentionPeriodMinutes { get; set; } = 60;
}
