using System.Text;
using System.Text.Json;
using AIExamForge.Application.Export.Interfaces;
using AIExamForge.Domain.Models;
using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Application.Export;

/// <summary>
/// T139: Markdown exporter for question sets.
/// Per openapi.yaml: format=markdown returns text/markdown
/// Per FR-050: Exam Mode omits explanations.
/// Per FR-051: Study Mode includes explanations in a separate section.
/// </summary>
public sealed class MarkdownExporter : IQuestionExporter
{
    public ExportFormat Format => ExportFormat.Markdown;
    public string ContentType => "text/markdown";
    public string FileExtension => ".md";

    public Task<ExportResult> ExportAsync(
        ExportContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        try
        {
            var sb = new StringBuilder();
            var filteredQuestions = RenderModeFilter.FilterQuestions(context);
            var rationalesSection = RenderModeFilter.GetRationalesSection(context);

            // Title and metadata
            AppendHeader(sb, context);

            // Disclaimer
            AppendDisclaimer(sb);

            // Questions
            AppendQuestions(sb, filteredQuestions);

            // Answer Key (in Study Mode only)
            if (rationalesSection.Count > 0)
            {
                AppendAnswerKey(sb, filteredQuestions, rationalesSection);
            }

            // Footer
            AppendFooter(sb, context);

            var content = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = GenerateFileName(context);

            return Task.FromResult(ExportResult.Succeeded(content, ContentType, fileName));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExportResult.Failed($"Markdown export failed: {ex.Message}"));
        }
    }

    private static void AppendHeader(StringBuilder sb, ExportContext context)
    {
        sb.AppendLine($"# {context.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Exam**: {context.ExamName}");
        sb.AppendLine($"**Questions**: {context.QuestionCount}");
        sb.AppendLine($"**Mode**: {(context.RenderMode == RenderMode.Exam ? "Exam Mode" : "Study Mode")}");
        sb.AppendLine($"**Generated**: {context.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void AppendDisclaimer(StringBuilder sb)
    {
        sb.AppendLine("> **Disclaimer**");
        sb.AppendLine(">");
        foreach (var line in ExportContext.DisclaimerText.Split('\n'))
        {
            sb.AppendLine($"> {line.Trim()}");
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void AppendQuestions(StringBuilder sb, IReadOnlyList<FilteredQuestion> questions)
    {
        sb.AppendLine("## Questions");
        sb.AppendLine();

        int questionNumber = 0;
        foreach (var question in questions)
        {
            questionNumber++;
            AppendQuestion(sb, question, questionNumber);
        }
    }

    private static void AppendQuestion(StringBuilder sb, FilteredQuestion question, int number)
    {
        sb.AppendLine($"### Question {number}");
        sb.AppendLine();

        // Metadata line
        sb.AppendLine($"*Type: {question.ItemTypeCode} | Difficulty: {question.Difficulty}*");
        sb.AppendLine();

        // Instruction if present
        if (!string.IsNullOrWhiteSpace(question.Instruction))
        {
            sb.AppendLine($"**Instructions**: {question.Instruction}");
            sb.AppendLine();
        }

        // Stem
        sb.AppendLine(question.Stem);
        sb.AppendLine();

        // Content (choices, etc.)
        AppendQuestionContent(sb, question.Content, question.ItemTypeCode);

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void AppendQuestionContent(StringBuilder sb, string contentJson, string itemTypeCode)
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
                        sb.AppendLine($"- **{optionLetter})** {text.GetString()}");
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
                        sb.AppendLine($"- **{optionLetter})** {text.GetString()}");
                    }
                    optionLetter++;
                }
            }
            // Handle drag-and-drop
            else if (root.TryGetProperty("sources", out var sources) &&
                     root.TryGetProperty("targets", out var targets))
            {
                sb.AppendLine("**Drag items:**");
                if (sources.ValueKind == JsonValueKind.Array)
                {
                    foreach (var source in sources.EnumerateArray())
                    {
                        if (source.TryGetProperty("text", out var text))
                        {
                            sb.AppendLine($"- {text.GetString()}");
                        }
                    }
                }
                sb.AppendLine();
                sb.AppendLine("**Drop targets:**");
                if (targets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var target in targets.EnumerateArray())
                    {
                        if (target.TryGetProperty("label", out var label))
                        {
                            sb.AppendLine($"- {label.GetString()}");
                        }
                    }
                }
            }
            // Handle build list
            else if (root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Available steps (arrange in correct order):**");
                foreach (var step in steps.EnumerateArray())
                {
                    if (step.TryGetProperty("text", out var text))
                    {
                        sb.AppendLine($"- {text.GetString()}");
                    }
                }
            }
            // Generic fallback - just note there's content
            else
            {
                sb.AppendLine("*See content for this question type*");
            }
        }
        catch
        {
            sb.AppendLine("*Unable to render question content*");
        }
    }

    private static void AppendAnswerKey(
        StringBuilder sb,
        IReadOnlyList<FilteredQuestion> questions,
        IReadOnlyList<QuestionRationale> rationales)
    {
        sb.AppendLine("## Answer Key & Explanations");
        sb.AppendLine();

        foreach (var rationale in rationales)
        {
            var question = questions.FirstOrDefault(q => q.Id == rationale.QuestionId);

            sb.AppendLine($"### Question {rationale.QuestionNumber}");
            sb.AppendLine();

            // Try to extract correct answer(s) from content
            if (question != null)
            {
                var answers = ExtractCorrectAnswers(question.Content);
                if (!string.IsNullOrEmpty(answers))
                {
                    sb.AppendLine($"**Correct Answer**: {answers}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine($"**Explanation**: {rationale.Rationale}");
            sb.AppendLine();
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

    private static void AppendFooter(StringBuilder sb, ExportContext context)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"*Generated by AIExamForge on {context.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC*");
        sb.AppendLine();
        sb.AppendLine($"*{ExportContext.ShortDisclaimer}*");
    }

    private static string GenerateFileName(ExportContext context)
    {
        var sanitizedName = SanitizeFileName(context.ExamName);
        var modeIndicator = context.RenderMode == RenderMode.Exam ? "exam" : "study";
        var timestamp = context.GeneratedAt.ToString("yyyyMMdd");
        return $"{sanitizedName}_{modeIndicator}_{timestamp}.md";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Where(c => !invalid.Contains(c)));
        return sanitized.Replace(" ", "_").ToLowerInvariant();
    }
}
