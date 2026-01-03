using System.Text;
using AIExamForge.Application.Documents.Interfaces;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIExamForge.Infrastructure.Documents;

/// <summary>
/// Configuration options for Azure AI Document Intelligence.
/// </summary>
public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "AzureDocumentIntelligence";

    /// <summary>
    /// Azure AI Document Intelligence endpoint URL.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Azure AI Document Intelligence API key.
    /// </summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Extracts text content from PDF files using Azure AI Document Intelligence.
/// </summary>
public sealed class PdfExtractor : IDocumentExtractor
{
    private readonly ILogger<PdfExtractor> _logger;
    private readonly DocumentIntelligenceOptions _options;
    private DocumentAnalysisClient? _client;

    public PdfExtractor(
        ILogger<PdfExtractor> logger,
        IOptions<DocumentIntelligenceOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public IReadOnlyList<string> SupportedExtensions => [".pdf"];

    public bool CanExtract(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<DocumentExtractionResult> ExtractTextAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!CanExtract(fileName))
        {
            return DocumentExtractionResult.Failed($"Unsupported file type: {Path.GetExtension(fileName)}");
        }

        // Check if Document Intelligence is configured
        if (string.IsNullOrEmpty(_options.Endpoint) || string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("Azure Document Intelligence not configured. PDF extraction unavailable.");
            return DocumentExtractionResult.Failed("PDF extraction is not configured. Please configure Azure Document Intelligence.");
        }

        try
        {
            var client = GetClient();
            
            // Reset stream position if possible
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            _logger.LogDebug("Starting PDF extraction for file: {FileName}", fileName);

            // Use prebuilt-read model for text extraction
            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-read",
                stream,
                cancellationToken: cancellationToken);

            var result = operation.Value;
            
            if (result.Pages.Count == 0)
            {
                return DocumentExtractionResult.Failed("No pages found in PDF document");
            }

            var textBuilder = new StringBuilder();
            var pageCount = result.Pages.Count;

            // Extract text preserving structure
            foreach (var page in result.Pages)
            {
                foreach (var line in page.Lines)
                {
                    textBuilder.AppendLine(line.Content);
                }
                textBuilder.AppendLine(); // Page break
            }

            var text = textBuilder.ToString().Trim();
            
            if (string.IsNullOrWhiteSpace(text))
            {
                return DocumentExtractionResult.Failed("No text content found in PDF");
            }

            _logger.LogInformation("Extracted {CharCount} characters from {PageCount} pages in PDF",
                text.Length, pageCount);

            return DocumentExtractionResult.Successful(text, pageCount);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Document Intelligence request failed for file: {FileName}", fileName);
            return DocumentExtractionResult.Failed($"Document analysis failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF file: {FileName}", fileName);
            return DocumentExtractionResult.Failed($"Failed to extract text: {ex.Message}");
        }
    }

    private DocumentAnalysisClient GetClient()
    {
        if (_client != null)
        {
            return _client;
        }

        var endpoint = new Uri(_options.Endpoint!);
        var credential = new AzureKeyCredential(_options.ApiKey!);
        _client = new DocumentAnalysisClient(endpoint, credential);
        
        return _client;
    }
}
