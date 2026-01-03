using System.Text.Json;
using AIExamForge.Application.Export;
using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Domain.Models;
using AIExamForge.Domain.ValueObjects;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AIExamForge.Infrastructure.Export;

/// <summary>
/// T140: DOCX exporter for question sets using Open XML SDK.
/// Per FR-020a: DOCX must include title page.
/// Per FR-020c: DOCX must include visible disclaimer on first page.
/// Per FR-050: Exam Mode omits explanations.
/// Per FR-051: Study Mode includes explanations in separate section.
/// </summary>
public sealed class DocxExporter : IQuestionExporter
{
    public ExportFormat Format => ExportFormat.Docx;
    public string ContentType => "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public string FileExtension => ".docx";

    public Task<ExportResult> ExportAsync(
        ExportContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        try
        {
            using var memoryStream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                // Add styles
                AddStyles(mainPart);

                // Title page
                AddTitlePage(body, context);

                // Disclaimer (on first page per FR-020c)
                AddDisclaimer(body);

                // Page break after title
                body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

                // Questions
                var filteredQuestions = RenderModeFilter.FilterQuestions(context);
                AddQuestions(body, filteredQuestions);

                // Answer key (Study Mode only)
                var rationalesSection = RenderModeFilter.GetRationalesSection(context);
                if (rationalesSection.Count > 0)
                {
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                    AddAnswerKey(body, filteredQuestions, rationalesSection);
                }

                // Footer with short disclaimer
                AddFooter(body, context);

                mainPart.Document.Save();
            }

            memoryStream.Position = 0;
            var content = memoryStream.ToArray();
            var fileName = GenerateFileName(context);

            return Task.FromResult(ExportResult.Succeeded(content, ContentType, fileName));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExportResult.Failed($"DOCX export failed: {ex.Message}"));
        }
    }

    private static void AddStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        // Title style
        styles.Append(CreateStyle("Title", "Title", 32, true, "2E74B5"));

        // Heading1 style
        styles.Append(CreateStyle("Heading1", "Heading 1", 24, true, "2E74B5"));

        // Heading2 style
        styles.Append(CreateStyle("Heading2", "Heading 2", 18, true, "404040"));

        // Normal style
        styles.Append(CreateStyle("Normal", "Normal", 11, false, "000000"));

        // Disclaimer style
        styles.Append(CreateStyle("Disclaimer", "Disclaimer", 9, false, "666666"));

        stylesPart.Styles = styles;
    }

    private static Style CreateStyle(string styleId, string styleName, int fontSize, bool bold, string colorHex)
    {
        return new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = styleId,
            StyleName = new StyleName { Val = styleName },
            StyleRunProperties = new StyleRunProperties
            {
                FontSize = new FontSize { Val = (fontSize * 2).ToString() },
                Bold = bold ? new Bold() : null,
                Color = new Color { Val = colorHex }
            }
        };
    }

    private static void AddTitlePage(Body body, ExportContext context)
    {
        // Main title
        var titlePara = new Paragraph(
            new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = "Title" } },
            new Run(new Text(context.Title)));
        body.AppendChild(titlePara);

        // Spacer
        body.AppendChild(new Paragraph());

        // Metadata
        AddMetadataLine(body, "Exam", context.ExamName);
        AddMetadataLine(body, "Questions", context.QuestionCount.ToString());
        AddMetadataLine(body, "Mode", context.RenderMode == RenderMode.Exam ? "Exam Mode" : "Study Mode");
        AddMetadataLine(body, "Generated", context.GeneratedAt.ToString("yyyy-MM-dd HH:mm") + " UTC");

        body.AppendChild(new Paragraph());
    }

    private static void AddMetadataLine(Body body, string label, string value)
    {
        var para = new Paragraph(
            new Run(new Text($"{label}: ") { Space = SpaceProcessingModeValues.Preserve },
                    new RunProperties { Bold = new Bold() }),
            new Run(new Text(value)));
        body.AppendChild(para);
    }

    private static void AddDisclaimer(Body body)
    {
        // Disclaimer heading
        var headingPara = new Paragraph(
            new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = "Heading2" } },
            new Run(new Text("Disclaimer")));
        body.AppendChild(headingPara);

        // Disclaimer text
        foreach (var line in ExportContext.DisclaimerText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var para = new Paragraph(
                new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = "Disclaimer" } },
                new Run(new Text(line.Trim())));
            body.AppendChild(para);
        }
    }

    private static void AddQuestions(Body body, IReadOnlyList<FilteredQuestion> questions)
    {
        // Section heading
        var headingPara = new Paragraph(
            new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = "Heading1" } },
            new Run(new Text("Questions")));
        body.AppendChild(headingPara);

        body.AppendChild(new Paragraph());

        int questionNumber = 0;
        foreach (var question in questions)
        {
            questionNumber++;
            AddQuestion(body, question, questionNumber);
        }
    }

    private static void AddQuestion(Body body, FilteredQuestion question, int number)
    {
        // Question heading
        var headingPara = new Paragraph(
            new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = "Heading2" } },
            new Run(new Text($"Question {number}")));
        body.AppendChild(headingPara);

        // Metadata line
        var metaPara = new Paragraph(
            new Run(
                new RunProperties { Italic = new Italic(), FontSize = new FontSize { Val = "18" } },
                new Text($"Type: {question.ItemTypeCode} | Difficulty: {question.Difficulty}")));
        body.AppendChild(metaPara);

        // Instruction if present
        if (!string.IsNullOrWhiteSpace(question.Instruction))
        {
            var instructionPara = new Paragraph(
                new Run(new Text("Instructions: ") { Space = SpaceProcessingModeValues.Preserve },
                        new RunProperties { Bold = new Bold() }),
                new Run(new Text(question.Instruction)));
            body.AppendChild(instructionPara);
            body.AppendChild(new Paragraph());
        }

        // Stem
        var stemPara = new Paragraph(new Run(new Text(question.Stem)));
        body.AppendChild(stemPara);
        body.AppendChild(new Paragraph());

        // Content (choices, etc.)
        AddQuestionContent(body, question.Content, question.ItemTypeCode);

        // Separator
        body.AppendChild(new Paragraph());
        body.AppendChild(new Paragraph(new Run(
            new Text("────────────────────────────────────────"))));
        body.AppendChild(new Paragraph());
    }

    private static void AddQuestionContent(Body body, string contentJson, string itemTypeCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;

            // Handle choices-based questions
            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                char optionLetter = 'A';
                foreach (var choice in choices.EnumerateArray())
                {
                    if (choice.TryGetProperty("text", out var text))
                    {
                        var para = new Paragraph(
                            new Run(new Text($"{optionLetter}) ") { Space = SpaceProcessingModeValues.Preserve },
                                    new RunProperties { Bold = new Bold() }),
                            new Run(new Text(text.GetString() ?? "")));
                        body.AppendChild(para);
                    }
                    optionLetter++;
                }
            }
            // Handle options-based questions
            else if (root.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array)
            {
                char optionLetter = 'A';
                foreach (var option in options.EnumerateArray())
                {
                    if (option.TryGetProperty("text", out var text))
                    {
                        var para = new Paragraph(
                            new Run(new Text($"{optionLetter}) ") { Space = SpaceProcessingModeValues.Preserve },
                                    new RunProperties { Bold = new Bold() }),
                            new Run(new Text(text.GetString() ?? "")));
                        body.AppendChild(para);
                    }
                    optionLetter++;
                }
            }
            // Handle drag-and-drop
            else if (root.TryGetProperty("sources", out var sources) &&
                     root.TryGetProperty("targets", out var targets))
            {
                var dragLabel = new Paragraph(
                    new Run(new Text("Drag items:") { Space = SpaceProcessingModeValues.Preserve },
                            new RunProperties { Bold = new Bold() }));
                body.AppendChild(dragLabel);

                if (sources.ValueKind == JsonValueKind.Array)
                {
                    foreach (var source in sources.EnumerateArray())
                    {
                        if (source.TryGetProperty("text", out var text))
                        {
                            body.AppendChild(new Paragraph(new Run(new Text($"  • {text.GetString()}"))));
                        }
                    }
                }

                body.AppendChild(new Paragraph());
                var dropLabel = new Paragraph(
                    new Run(new Text("Drop targets:") { Space = SpaceProcessingModeValues.Preserve },
                            new RunProperties { Bold = new Bold() }));
                body.AppendChild(dropLabel);

                if (targets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var target in targets.EnumerateArray())
                    {
                        if (target.TryGetProperty("label", out var label))
                        {
                            body.AppendChild(new Paragraph(new Run(new Text($"  • {label.GetString()}"))));
                        }
                    }
                }
            }
            // Handle build list
            else if (root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
            {
                var stepsLabel = new Paragraph(
                    new Run(new Text("Available steps (arrange in correct order):") { Space = SpaceProcessingModeValues.Preserve },
                            new RunProperties { Bold = new Bold() }));
                body.AppendChild(stepsLabel);

                foreach (var step in steps.EnumerateArray())
                {
                    if (step.TryGetProperty("text", out var text))
                    {
                        body.AppendChild(new Paragraph(new Run(new Text($"  • {text.GetString()}"))));
                    }
                }
            }
        }
        catch
        {
            body.AppendChild(new Paragraph(
                new Run(new Text("See content for this question type"),
                        new RunProperties { Italic = new Italic() })));
        }
    }

    private static void AddAnswerKey(
        Body body,
        IReadOnlyList<FilteredQuestion> questions,
        IReadOnlyList<QuestionRationale> rationales)
    {
        // Section heading
        var headingPara = new Paragraph(
            new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = "Heading1" } },
            new Run(new Text("Answer Key & Explanations")));
        body.AppendChild(headingPara);

        body.AppendChild(new Paragraph());

        foreach (var rationale in rationales)
        {
            var question = questions.FirstOrDefault(q => q.Id == rationale.QuestionId);

            // Question number heading
            var qHeading = new Paragraph(
                new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = "Heading2" } },
                new Run(new Text($"Question {rationale.QuestionNumber}")));
            body.AppendChild(qHeading);

            // Try to extract correct answer(s) from content
            if (question != null)
            {
                var answers = ExtractCorrectAnswers(question.Content);
                if (!string.IsNullOrEmpty(answers))
                {
                    var answerPara = new Paragraph(
                        new Run(new Text("Correct Answer: ") { Space = SpaceProcessingModeValues.Preserve },
                                new RunProperties { Bold = new Bold() }),
                        new Run(new Text(answers)));
                    body.AppendChild(answerPara);
                }
            }

            // Explanation
            var explanationPara = new Paragraph(
                new Run(new Text("Explanation: ") { Space = SpaceProcessingModeValues.Preserve },
                        new RunProperties { Bold = new Bold() }),
                new Run(new Text(rationale.Rationale)));
            body.AppendChild(explanationPara);

            body.AppendChild(new Paragraph());
        }
    }

    private static string? ExtractCorrectAnswers(string contentJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;

            // Check for explicit correctAnswer(s)
            if (root.TryGetProperty("correctAnswer", out var correct))
            {
                return correct.ToString();
            }

            if (root.TryGetProperty("correctAnswers", out var correctAnswers))
            {
                if (correctAnswers.ValueKind == JsonValueKind.Array)
                {
                    return string.Join(", ", correctAnswers.EnumerateArray().Select(a => a.ToString()));
                }
            }

            // Look for isCorrect in choices
            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                var correctChoices = new List<string>();
                char optionLetter = 'A';
                foreach (var choice in choices.EnumerateArray())
                {
                    if (choice.TryGetProperty("isCorrect", out var isCorrect) && isCorrect.GetBoolean())
                    {
                        correctChoices.Add(optionLetter.ToString());
                    }
                    optionLetter++;
                }
                if (correctChoices.Count > 0)
                {
                    return string.Join(", ", correctChoices);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void AddFooter(Body body, ExportContext context)
    {
        body.AppendChild(new Paragraph());
        body.AppendChild(new Paragraph(new Run(
            new Text("────────────────────────────────────────"))));

        var footerPara = new Paragraph(
            new ParagraphProperties
            {
                Justification = new Justification { Val = JustificationValues.Center }
            },
            new Run(
                new RunProperties { Italic = new Italic(), FontSize = new FontSize { Val = "18" } },
                new Text($"Generated by AIExamForge on {context.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC")));
        body.AppendChild(footerPara);

        var disclaimerPara = new Paragraph(
            new ParagraphProperties
            {
                Justification = new Justification { Val = JustificationValues.Center }
            },
            new Run(
                new RunProperties { Italic = new Italic(), FontSize = new FontSize { Val = "16" } },
                new Text(ExportContext.ShortDisclaimer)));
        body.AppendChild(disclaimerPara);
    }

    private static string GenerateFileName(ExportContext context)
    {
        var sanitizedName = SanitizeFileName(context.ExamName);
        var modeIndicator = context.RenderMode == RenderMode.Exam ? "exam" : "study";
        var timestamp = context.GeneratedAt.ToString("yyyyMMdd");
        return $"{sanitizedName}_{modeIndicator}_{timestamp}.docx";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Where(c => !invalid.Contains(c)));
        return sanitized.Replace(" ", "_").ToLowerInvariant();
    }
}
