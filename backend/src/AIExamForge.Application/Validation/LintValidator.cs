using AIExamForge.Application.Validation.Interfaces;
using AIExamForge.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AIExamForge.Application.Validation;

/// <summary>
/// Validates question text against exam writing style rules per docs/exam-writing-style-rules.md.
/// </summary>
public sealed partial class LintValidator : ILintValidator
{
    private readonly ILogger<LintValidator> _logger;

    // Contractions to check (from section 2.2)
    private static readonly string[] Contractions =
    [
        "can't", "won't", "don't", "doesn't", "isn't", "aren't", "wasn't", "weren't",
        "it's", "that's", "there's", "here's", "you're", "they're", "we're", "I'm", "let's",
        "couldn't", "wouldn't", "shouldn't", "haven't", "hasn't", "hadn't", "mustn't",
        "didn't", "ain't", "who's", "what's", "where's", "when's", "how's"
    ];

    // Forbidden answer patterns (from section 2.3)
    private static readonly string[] ForbiddenPatterns =
    [
        "all of the above",
        "none of the above",
        "both a and b",
        "choose all that apply"
    ];

    // Teaching/explanation indicators (from section 2.1)
    private static readonly string[] TeachingIndicators =
    [
        "this is because",
        "the reason is",
        "in other words",
        "this means that",
        "for example",
        "to understand",
        "remember that",
        "note that",
        "keep in mind",
        "it is important to",
        "this works by",
        "the way this works"
    ];

    // Absolutes that may be problematic (from section 7.1)
    private static readonly string[] Absolutes =
    [
        "always", "never", "only", "all", "none", "every", "must always", "can never"
    ];

    public LintValidator(ILogger<LintValidator> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<LintViolation> Validate(string text, string context = "text")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var violations = new List<LintViolation>();

        // Check contractions
        var contractionViolation = CheckContractions(text, context);
        if (contractionViolation != null)
        {
            violations.Add(contractionViolation);
        }

        // Check forbidden patterns
        var patternViolation = CheckForbiddenPatterns(text, context);
        if (patternViolation != null)
        {
            violations.Add(patternViolation);
        }

        // Check teaching text (not in rationale context)
        if (!context.Equals("rationale", StringComparison.OrdinalIgnoreCase))
        {
            var teachingViolation = CheckTeachingText(text, context);
            if (teachingViolation != null)
            {
                violations.Add(teachingViolation);
            }
        }

        return violations;
    }

    public IReadOnlyList<LintViolation> ValidateQuestion(
        string stem,
        string? instruction,
        IEnumerable<string> choices,
        string? rationale)
    {
        var violations = new List<LintViolation>();

        // Validate stem
        violations.AddRange(Validate(stem, "stem"));

        // Validate instruction if present
        if (!string.IsNullOrWhiteSpace(instruction))
        {
            violations.AddRange(Validate(instruction, "instruction"));
        }

        // Validate each choice
        var choiceList = choices.ToList();
        for (int i = 0; i < choiceList.Count; i++)
        {
            var choiceViolations = Validate(choiceList[i], $"choice[{i}]");
            violations.AddRange(choiceViolations);

            // Check for explanations in choices (section 3.3)
            var explanationViolation = CheckExplanationInChoice(choiceList[i], i);
            if (explanationViolation != null)
            {
                violations.Add(explanationViolation);
            }
        }

        // Validate rationale (teaching text is allowed in rationale)
        if (!string.IsNullOrWhiteSpace(rationale))
        {
            // Only check contractions and forbidden patterns in rationale
            var contractionViolation = CheckContractions(rationale, "rationale");
            if (contractionViolation != null)
            {
                violations.Add(contractionViolation);
            }
        }

        // Check minimum answer choices (section 3.1)
        if (choiceList.Count < 3)
        {
            violations.Add(new LintViolation
            {
                Rule = LintRule.MinimumAnswerChoices,
                Severity = LintSeverity.Error,
                Message = $"Minimum 3 answer choices required; found {choiceList.Count}",
                Location = "choices"
            });
        }

        return violations;
    }

    public LintViolation? ValidateRule(string text, LintRule rule)
    {
        return rule switch
        {
            LintRule.NoContractions => CheckContractions(text, "text"),
            LintRule.NoForbiddenAnswerPatterns => CheckForbiddenPatterns(text, "text"),
            LintRule.NoTeachingText => CheckTeachingText(text, "text"),
            LintRule.NoIncorrectAbsolutes => CheckAbsolutes(text, "text"),
            _ => null // Rule not implemented for single-text validation
        };
    }

    private LintViolation? CheckContractions(string text, string context)
    {
        var lowerText = text.ToLowerInvariant();
        foreach (var contraction in Contractions)
        {
            // Use word boundary check to avoid false positives
            if (ContainsWholeWord(lowerText, contraction))
            {
                _logger.LogDebug("Found contraction '{Contraction}' in {Context}", contraction, context);
                return new LintViolation
                {
                    Rule = LintRule.NoContractions,
                    Severity = LintSeverity.Error,
                    Message = $"Contractions are forbidden. Found: '{contraction}'",
                    Location = context,
                    ViolatingText = contraction
                };
            }
        }
        return null;
    }

    private LintViolation? CheckForbiddenPatterns(string text, string context)
    {
        var lowerText = text.ToLowerInvariant();
        foreach (var pattern in ForbiddenPatterns)
        {
            if (lowerText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Found forbidden pattern '{Pattern}' in {Context}", pattern, context);
                return new LintViolation
                {
                    Rule = LintRule.NoForbiddenAnswerPatterns,
                    Severity = LintSeverity.Error,
                    Message = $"Forbidden answer pattern found: '{pattern}'",
                    Location = context,
                    ViolatingText = pattern
                };
            }
        }
        return null;
    }

    private LintViolation? CheckTeachingText(string text, string context)
    {
        var lowerText = text.ToLowerInvariant();
        foreach (var indicator in TeachingIndicators)
        {
            if (lowerText.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Found teaching indicator '{Indicator}' in {Context}", indicator, context);
                return new LintViolation
                {
                    Rule = LintRule.NoTeachingText,
                    Severity = LintSeverity.Error,
                    Message = $"Teaching or explanation text detected: '{indicator}'",
                    Location = context,
                    ViolatingText = indicator
                };
            }
        }
        return null;
    }

    private LintViolation? CheckExplanationInChoice(string choice, int index)
    {
        var lowerChoice = choice.ToLowerInvariant();
        
        // Check for explanation patterns in choices
        var explanationPatterns = new[] { "because", "since", "therefore", "thus", "hence" };
        foreach (var pattern in explanationPatterns)
        {
            if (ContainsWholeWord(lowerChoice, pattern))
            {
                return new LintViolation
                {
                    Rule = LintRule.NoExplanationsInChoices,
                    Severity = LintSeverity.Warning,
                    Message = $"Answer choice may contain explanation text: '{pattern}'",
                    Location = $"choice[{index}]",
                    ViolatingText = pattern
                };
            }
        }
        return null;
    }

    private LintViolation? CheckAbsolutes(string text, string context)
    {
        var lowerText = text.ToLowerInvariant();
        foreach (var absolute in Absolutes)
        {
            if (ContainsWholeWord(lowerText, absolute))
            {
                // This is a warning, not an error - absolutes may be valid if technically accurate
                return new LintViolation
                {
                    Rule = LintRule.NoIncorrectAbsolutes,
                    Severity = LintSeverity.Warning,
                    Message = $"Absolute term found: '{absolute}'. Verify it is technically accurate.",
                    Location = context,
                    ViolatingText = absolute
                };
            }
        }
        return null;
    }

    private static bool ContainsWholeWord(string text, string word)
    {
        // Create a regex pattern that matches the word with word boundaries
        var pattern = $@"\b{Regex.Escape(word)}\b";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }
}
