using System.Text;
using AIExamForge.Application.Documents.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace AIExamForge.Infrastructure.Documents;

/// <summary>
/// Extracts text content from .docx files using Open XML SDK.
/// </summary>
public sealed class DocxExtractor : IDocumentExtractor
{
    private readonly ILogger<DocxExtractor> _logger;

    public DocxExtractor(ILogger<DocxExtractor> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> SupportedExtensions => [".docx"];

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

        try
        {
            // Open XML SDK works synchronously, wrap in Task.Run for async context
            return await Task.Run(() => ExtractTextSync(stream, fileName), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from DOCX file: {FileName}", fileName);
            return DocumentExtractionResult.Failed($"Failed to extract text: {ex.Message}");
        }
    }

    private DocumentExtractionResult ExtractTextSync(Stream stream, string fileName)
    {
        _logger.LogDebug("Extracting text from DOCX file: {FileName}", fileName);

        // Ensure stream can be read
        if (!stream.CanRead)
        {
            return DocumentExtractionResult.Failed("Stream is not readable");
        }

        // Reset position if possible
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var document = WordprocessingDocument.Open(stream, false);
        
        var body = document.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            return DocumentExtractionResult.Failed("Document body not found");
        }

        var textBuilder = new StringBuilder();
        var paragraphCount = 0;

        // Extract text from paragraphs
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var paragraphText = GetParagraphText(paragraph);
            
            if (!string.IsNullOrWhiteSpace(paragraphText))
            {
                // Check for heading style to preserve outline structure
                var style = GetParagraphStyle(paragraph, document);
                var prefix = GetHeadingPrefix(style);
                
                if (!string.IsNullOrEmpty(prefix))
                {
                    textBuilder.AppendLine($"{prefix} {paragraphText}");
                }
                else
                {
                    textBuilder.AppendLine(paragraphText);
                }
                
                paragraphCount++;
            }
        }

        var text = textBuilder.ToString().Trim();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return DocumentExtractionResult.Failed("No text content found in document");
        }

        _logger.LogInformation("Extracted {CharCount} characters from {ParagraphCount} paragraphs in DOCX", 
            text.Length, paragraphCount);

        return DocumentExtractionResult.Successful(text, paragraphCount);
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        var builder = new StringBuilder();
        
        foreach (var run in paragraph.Descendants<Run>())
        {
            foreach (var text in run.Descendants<Text>())
            {
                builder.Append(text.Text);
            }
        }

        return builder.ToString();
    }

    private static string? GetParagraphStyle(Paragraph paragraph, WordprocessingDocument document)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (string.IsNullOrEmpty(styleId))
        {
            return null;
        }

        // Try to get the style definition
        var styles = document.MainDocumentPart?.StyleDefinitionsPart?.Styles;
        var style = styles?.Elements<Style>()
            .FirstOrDefault(s => s.StyleId?.Value == styleId);

        return style?.StyleName?.Val?.Value ?? styleId;
    }

    private static string GetHeadingPrefix(string? style)
    {
        if (string.IsNullOrEmpty(style))
        {
            return string.Empty;
        }

        // Map Word heading styles to markdown headings
        return style.ToLowerInvariant() switch
        {
            "heading1" or "heading 1" => "#",
            "heading2" or "heading 2" => "##",
            "heading3" or "heading 3" => "###",
            "heading4" or "heading 4" => "####",
            "heading5" or "heading 5" => "#####",
            "heading6" or "heading 6" => "######",
            "title" => "#",
            "subtitle" => "##",
            _ when style.StartsWith("heading", StringComparison.OrdinalIgnoreCase) => "#",
            _ => string.Empty
        };
    }
}
