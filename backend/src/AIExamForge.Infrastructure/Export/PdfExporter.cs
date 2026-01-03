using System.Text.Json;
using AIExamForge.Application.Export;
using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Domain.Models;
using AIExamForge.Domain.ValueObjects;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AIExamForge.Infrastructure.Export;

/// <summary>
/// T141: PDF exporter for question sets using QuestPDF.
/// Per FR-020a: PDF must include title page (matches DOCX layout).
/// Per FR-020c: PDF must include visible disclaimer on first page.
/// Per FR-050: Exam Mode omits explanations.
/// Per FR-051: Study Mode includes explanations in separate section.
/// </summary>
public sealed class PdfExporter : IQuestionExporter
{
    private static readonly bool _licenseConfigured = false;
    private static readonly object _licenseLock = new();

    public ExportFormat Format => ExportFormat.Pdf;
    public string ContentType => "application/pdf";
    public string FileExtension => ".pdf";

    public Task<ExportResult> ExportAsync(
        ExportContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        EnsureLicenseConfigured();

        try
        {
            var filteredQuestions = RenderModeFilter.FilterQuestions(context);
            var rationalesSection = RenderModeFilter.GetRationalesSection(context);

            var document = CreateDocument(context, filteredQuestions, rationalesSection);
            var pdfBytes = document.GeneratePdf();

            var fileName = GenerateFileName(context);

            return Task.FromResult(ExportResult.Succeeded(pdfBytes, ContentType, fileName));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExportResult.Failed($"PDF export failed: {ex.Message}"));
        }
    }

    private static void EnsureLicenseConfigured()
    {
        if (!_licenseConfigured)
        {
            lock (_licenseLock)
            {
                // QuestPDF Community license for open source / non-commercial use
                QuestPDF.Settings.License = LicenseType.Community;
            }
        }
    }

    private static IDocument CreateDocument(
        ExportContext context,
        IReadOnlyList<FilteredQuestion> questions,
        IReadOnlyList<QuestionRationale> rationales)
    {
        return Document.Create(container =>
        {
            // Title page
            container.Page(page =>
            {
                ConfigurePage(page);
                page.Content().Column(column =>
                {
                    column.Spacing(15);
                    AddTitlePage(column, context);
                    AddDisclaimer(column);
                });
                AddFooter(page, context);
            });

            // Questions pages
            container.Page(page =>
            {
                ConfigurePage(page);
                page.Header().Text("Questions").FontSize(24).Bold().FontColor(Colors.Blue.Darken2);
                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(20);
                    int questionNumber = 0;
                    foreach (var question in questions)
                    {
                        questionNumber++;
                        AddQuestion(column, question, questionNumber);
                    }
                });
                AddFooter(page, context);
            });

            // Answer key page (Study Mode only)
            if (rationales.Count > 0)
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Header().Text("Answer Key & Explanations").FontSize(24).Bold().FontColor(Colors.Blue.Darken2);
                    page.Content().PaddingVertical(20).Column(column =>
                    {
                        column.Spacing(15);
                        foreach (var rationale in rationales)
                        {
                            var question = questions.FirstOrDefault(q => q.Id == rationale.QuestionId);
                            AddAnswerEntry(column, question, rationale);
                        }
                    });
                    AddFooter(page, context);
                });
            }
        });
    }

    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(50);
        page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));
    }

    private static void AddTitlePage(ColumnDescriptor column, ExportContext context)
    {
        // Main title
        column.Item().Text(context.Title)
            .FontSize(32)
            .Bold()
            .FontColor(Colors.Blue.Darken2);

        column.Item().PaddingTop(20).Column(meta =>
        {
            meta.Spacing(5);

            meta.Item().Row(row =>
            {
                row.RelativeItem().Text("Exam:").Bold();
                row.RelativeItem(3).Text(context.ExamName);
            });

            meta.Item().Row(row =>
            {
                row.RelativeItem().Text("Questions:").Bold();
                row.RelativeItem(3).Text(context.QuestionCount.ToString());
            });

            meta.Item().Row(row =>
            {
                row.RelativeItem().Text("Mode:").Bold();
                row.RelativeItem(3).Text(context.RenderMode == RenderMode.Exam ? "Exam Mode" : "Study Mode");
            });

            meta.Item().Row(row =>
            {
                row.RelativeItem().Text("Generated:").Bold();
                row.RelativeItem(3).Text($"{context.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
            });
        });
    }

    private static void AddDisclaimer(ColumnDescriptor column)
    {
        column.Item().PaddingTop(30).Column(disclaimer =>
        {
            disclaimer.Item().Text("Disclaimer")
                .FontSize(14)
                .Bold()
                .FontColor(Colors.Grey.Darken2);

            disclaimer.Item().PaddingTop(10).Text(ExportContext.DisclaimerText)
                .FontSize(9)
                .FontColor(Colors.Grey.Darken1);
        });
    }

    private static void AddQuestion(ColumnDescriptor column, FilteredQuestion question, int number)
    {
        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(15).Column(qCol =>
        {
            qCol.Spacing(8);

            // Question header
            qCol.Item().Row(row =>
            {
                row.RelativeItem().Text($"Question {number}")
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken2);
            });

            // Metadata
            qCol.Item().Text($"Type: {question.ItemTypeCode} | Difficulty: {question.Difficulty}")
                .FontSize(9)
                .Italic()
                .FontColor(Colors.Grey.Darken1);

            // Instruction if present
            if (!string.IsNullOrWhiteSpace(question.Instruction))
            {
                qCol.Item().PaddingTop(5).Row(row =>
                {
                    row.ConstantItem(80).Text("Instructions:").Bold();
                    row.RelativeItem().Text(question.Instruction);
                });
            }

            // Stem
            qCol.Item().PaddingTop(5).Text(question.Stem);

            // Content (choices, etc.)
            qCol.Item().PaddingTop(10).Column(contentCol =>
            {
                AddQuestionContent(contentCol, question.Content, question.ItemTypeCode);
            });
        });
    }

    private static void AddQuestionContent(ColumnDescriptor column, string contentJson, string itemTypeCode)
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
                        column.Item().Row(row =>
                        {
                            row.ConstantItem(25).Text($"{optionLetter})").Bold();
                            row.RelativeItem().Text(text.GetString() ?? "");
                        });
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
                        column.Item().Row(row =>
                        {
                            row.ConstantItem(25).Text($"{optionLetter})").Bold();
                            row.RelativeItem().Text(text.GetString() ?? "");
                        });
                    }
                    optionLetter++;
                }
            }
            // Handle drag-and-drop
            else if (root.TryGetProperty("sources", out var sources) &&
                     root.TryGetProperty("targets", out var targets))
            {
                column.Item().Text("Drag items:").Bold();
                if (sources.ValueKind == JsonValueKind.Array)
                {
                    foreach (var source in sources.EnumerateArray())
                    {
                        if (source.TryGetProperty("text", out var text))
                        {
                            column.Item().PaddingLeft(15).Text($"• {text.GetString()}");
                        }
                    }
                }

                column.Item().PaddingTop(8).Text("Drop targets:").Bold();
                if (targets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var target in targets.EnumerateArray())
                    {
                        if (target.TryGetProperty("label", out var label))
                        {
                            column.Item().PaddingLeft(15).Text($"• {label.GetString()}");
                        }
                    }
                }
            }
            // Handle build list
            else if (root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
            {
                column.Item().Text("Available steps (arrange in correct order):").Bold();
                foreach (var step in steps.EnumerateArray())
                {
                    if (step.TryGetProperty("text", out var text))
                    {
                        column.Item().PaddingLeft(15).Text($"• {text.GetString()}");
                    }
                }
            }
        }
        catch
        {
            column.Item().Text("See content for this question type").Italic();
        }
    }

    private static void AddAnswerEntry(
        ColumnDescriptor column,
        FilteredQuestion? question,
        QuestionRationale rationale)
    {
        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(answerCol =>
        {
            answerCol.Spacing(5);

            // Question number
            answerCol.Item().Text($"Question {rationale.QuestionNumber}")
                .FontSize(12)
                .Bold()
                .FontColor(Colors.Blue.Darken2);

            // Correct answer if available
            if (question != null)
            {
                var answers = ExtractCorrectAnswers(question.Content);
                if (!string.IsNullOrEmpty(answers))
                {
                    answerCol.Item().Row(row =>
                    {
                        row.ConstantItem(100).Text("Correct Answer:").Bold();
                        row.RelativeItem().Text(answers);
                    });
                }
            }

            // Explanation
            answerCol.Item().Row(row =>
            {
                row.ConstantItem(100).Text("Explanation:").Bold();
                row.RelativeItem().Text(rationale.Rationale);
            });
        });
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

    private static void AddFooter(PageDescriptor page, ExportContext context)
    {
        page.Footer().AlignCenter().Column(col =>
        {
            col.Item().Text($"Generated by AIExamForge on {context.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC")
                .FontSize(8)
                .Italic()
                .FontColor(Colors.Grey.Darken1);

            col.Item().Text(ExportContext.ShortDisclaimer)
                .FontSize(7)
                .Italic()
                .FontColor(Colors.Grey.Darken1);

            col.Item().AlignCenter().Text(text =>
            {
                text.CurrentPageNumber().FontSize(9);
                text.Span(" / ").FontSize(9);
                text.TotalPages().FontSize(9);
            });
        });
    }

    private static string GenerateFileName(ExportContext context)
    {
        var sanitizedName = SanitizeFileName(context.ExamName);
        var modeIndicator = context.RenderMode == RenderMode.Exam ? "exam" : "study";
        var timestamp = context.GeneratedAt.ToString("yyyyMMdd");
        return $"{sanitizedName}_{modeIndicator}_{timestamp}.pdf";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Where(c => !invalid.Contains(c)));
        return sanitized.Replace(" ", "_").ToLowerInvariant();
    }
}
