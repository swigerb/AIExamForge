using AIExamForge.Application.Documents.Interfaces;
using AIExamForge.Application.Parsing;
using AIExamForge.Application.Parsing.DTOs;
using AIExamForge.Application.Parsing.Interfaces;
using AIExamForge.Domain.Entities;
using AIExamForge.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Application.Documents;

/// <summary>
/// Service for handling file uploads, extraction, and outline parsing.
/// </summary>
public sealed class FileUploadService
{
    private readonly IEnumerable<IDocumentExtractor> _extractors;
    private readonly IOutlineParser _parser;
    private readonly IOutlineRepository _outlineRepository;
    private readonly IFileUploadRepository _fileUploadRepository;
    private readonly IBlobStorageClient? _blobStorageClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FileUploadService> _logger;

    // Maximum file size: 10 MB per FR-024
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    
    // Allowed extensions and content types
    private static readonly Dictionary<string, string> ExtensionToContentType = new()
    {
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".pdf"] = "application/pdf"
    };

    public FileUploadService(
        IEnumerable<IDocumentExtractor> extractors,
        IOutlineParser parser,
        IOutlineRepository outlineRepository,
        IFileUploadRepository fileUploadRepository,
        IUnitOfWork unitOfWork,
        ILogger<FileUploadService> logger,
        IBlobStorageClient? blobStorageClient = null)
    {
        _extractors = extractors;
        _parser = parser;
        _outlineRepository = outlineRepository;
        _fileUploadRepository = fileUploadRepository;
        _blobStorageClient = blobStorageClient;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Processes an uploaded file, extracts text, and creates an outline.
    /// </summary>
    public async Task<FileUploadResult> ProcessUploadAsync(
        string userId,
        Stream fileStream,
        string fileName,
        long fileSize,
        string? examName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));
        ArgumentNullException.ThrowIfNull(fileStream);

        _logger.LogInformation("Processing file upload: {FileName}, size: {Size} bytes", fileName, fileSize);

        // Validate file size
        if (fileSize > MaxFileSizeBytes)
        {
            return FileUploadResult.Failed($"File size exceeds maximum of {MaxFileSizeBytes / (1024 * 1024)} MB");
        }

        // Validate extension and get content type
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!ExtensionToContentType.TryGetValue(extension, out var contentType))
        {
            return FileUploadResult.Failed($"File type '{extension}' not supported. Allowed types: {string.Join(", ", ExtensionToContentType.Keys)}");
        }

        // Find appropriate extractor
        var extractor = _extractors.FirstOrDefault(e => e.CanExtract(fileName));
        if (extractor == null)
        {
            return FileUploadResult.Failed($"No extractor available for file type: {extension}");
        }

        // Upload to blob storage if client available, otherwise use a placeholder URI
        string blobStorageUri;
        if (_blobStorageClient != null)
        {
            var uploadResult = await _blobStorageClient.UploadAsync(fileStream, fileName, contentType, cancellationToken);
            if (!uploadResult.Success)
            {
                return FileUploadResult.Failed(uploadResult.ErrorMessage ?? "Failed to upload file to storage");
            }
            blobStorageUri = uploadResult.Uri!;
            
            // Reset stream position for extraction
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }
        }
        else
        {
            // Use local processing path when blob storage not configured
            blobStorageUri = $"local://uploads/{Guid.NewGuid()}/{fileName}";
        }

        // Create file upload record
        var fileUpload = FileUpload.Create(userId, fileName, contentType, fileSize, blobStorageUri);
        await _fileUploadRepository.AddAsync(fileUpload, cancellationToken);

        try
        {
            // Mark as processing
            fileUpload.MarkProcessing();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Extract text from document
            var extractionResult = await extractor.ExtractTextAsync(fileStream, fileName, cancellationToken);
            
            if (!extractionResult.Success)
            {
                fileUpload.MarkFailed(extractionResult.ErrorMessage ?? "Extraction failed");
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return FileUploadResult.Failed(extractionResult.ErrorMessage ?? "Failed to extract text from document");
            }

            var extractedText = extractionResult.Text!;
            
            // Validate extracted text
            if (!_parser.CanParse(extractedText))
            {
                fileUpload.MarkFailed("No parseable content found");
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return FileUploadResult.Failed("No outline structure could be detected in the document");
            }

            // Determine source type
            var sourceType = extension switch
            {
                ".docx" => "DocxUpload",
                ".pdf" => "PdfUpload",
                _ => "FileUpload"
            };

            // Create outline
            var outline = Outline.Create(userId, extractedText, examName, fileUpload.Id);
            
            // Parse and set nodes
            var nodes = _parser.Parse(extractedText, outline.Id);
            outline.SetParsedNodes(nodes);

            // Mark file upload as completed
            fileUpload.MarkCompleted(extractedText, outline.Id);

            // Persist
            await _outlineRepository.AddAsync(outline, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully processed upload {FileUploadId}, created outline {OutlineId}",
                fileUpload.Id, outline.Id);

            return FileUploadResult.Successful(
                fileUpload.Id,
                outline.Id,
                MapToResponse(outline, sourceType, fileName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing upload: {FileName}", fileName);
            fileUpload.MarkFailed(ex.Message);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return FileUploadResult.Failed($"Error processing document: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets supported file extensions.
    /// </summary>
    public IReadOnlyList<string> GetSupportedExtensions() => ExtensionToContentType.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Gets maximum file size in bytes.
    /// </summary>
    public long GetMaxFileSize() => MaxFileSizeBytes;

    private static OutlineResponse MapToResponse(Outline outline, string sourceType, string fileName)
    {
        return new OutlineResponse
        {
            Id = outline.Id,
            ExamName = outline.Title,
            SourceType = sourceType,
            SourceFileName = fileName,
            Nodes = outline.RootNodes.Select(MapNodeToDto).ToList().AsReadOnly(),
            CreatedAt = outline.CreatedAt
        };
    }

    private static OutlineNodeDto MapNodeToDto(OutlineNode node)
    {
        return new OutlineNodeDto
        {
            Id = node.Id,
            Title = node.Title,
            Content = node.Content,
            Level = node.Level,
            Children = node.Children.Select(MapNodeToDto).ToList().AsReadOnly()
        };
    }
}

/// <summary>
/// Result of a file upload operation.
/// </summary>
public sealed record FileUploadResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? FileUploadId { get; init; }
    public Guid? OutlineId { get; init; }
    public OutlineResponse? Outline { get; init; }

    public static FileUploadResult Successful(Guid fileUploadId, Guid outlineId, OutlineResponse outline)
        => new()
        {
            Success = true,
            FileUploadId = fileUploadId,
            OutlineId = outlineId,
            Outline = outline
        };

    public static FileUploadResult Failed(string errorMessage)
        => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
