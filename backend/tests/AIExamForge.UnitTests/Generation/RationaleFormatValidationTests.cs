using System.Text.Json;
using AIExamForge.Application.Generation;
using Xunit;

namespace AIExamForge.UnitTests.Generation;

public sealed class RationaleFormatValidationTests
{
    [Fact]
    public void ValidateRationaleFormat_M1_Passes_WhenCorrectAndDistractorsAndLearnUrlPresent()
    {
        const string contentJson = """
        {
          "choices": [
            { "key": "A", "text": "Choice A" },
            { "key": "B", "text": "Choice B" },
            { "key": "C", "text": "Choice C" }
          ],
          "correctChoiceKey": "A",
          "randomizeChoices": true
        }
        """;

        const string rationale = """
        This is the correct answer for A. It is 100% correct because it matches the requirement. https://learn.microsoft.com/some/path

        This is a distractor. It is 100% incorrect because it violates the requirement. https://learn.microsoft.com/another/path

        This is a distractor. It is 100% incorrect because it is not supported. https://learn.microsoft.com/yet/another/path
        """;

        using var doc = JsonDocument.Parse(contentJson);
        var errors = QuestionGenerationService.ValidateRationaleFormatForTests("M1", doc.RootElement, rationale);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRationaleFormat_M1_Fails_WhenMissingLearnUrl()
    {
        const string contentJson = """
        {
          "choices": [
            { "key": "A", "text": "Choice A" },
            { "key": "B", "text": "Choice B" },
            { "key": "C", "text": "Choice C" }
          ],
          "correctChoiceKey": "A",
          "randomizeChoices": true
        }
        """;

        const string rationale = """
        This is the correct answer for A. It is 100% correct because it matches the requirement.

        This is a distractor. It is 100% incorrect because it violates the requirement.

        This is a distractor. It is 100% incorrect because it is not supported.
        """;

        using var doc = JsonDocument.Parse(contentJson);
        var errors = QuestionGenerationService.ValidateRationaleFormatForTests("M1", doc.RootElement, rationale);

        Assert.Contains(errors, e => e.Contains("Microsoft Learn URL", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRationaleFormat_M1_Fails_WhenMissingCorrectPhrase()
    {
        const string contentJson = """
        {
          "choices": [
            { "key": "A", "text": "Choice A" },
            { "key": "B", "text": "Choice B" },
            { "key": "C", "text": "Choice C" }
          ],
          "correctChoiceKey": "A",
          "randomizeChoices": true
        }
        """;

        const string rationale = """
        This is the correct answer for B. It is 100% correct because (wrong key). https://learn.microsoft.com/some/path

        This is a distractor. It is 100% incorrect because it violates the requirement. https://learn.microsoft.com/another/path

        This is a distractor. It is 100% incorrect because it is not supported. https://learn.microsoft.com/yet/another/path
        """;

        using var doc = JsonDocument.Parse(contentJson);
        var errors = QuestionGenerationService.ValidateRationaleFormatForTests("M1", doc.RootElement, rationale);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("correct-answer phrase", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRationaleFormat_M1_Fails_WhenTooFewDistractorPhrases()
    {
        const string contentJson = """
        {
          "choices": [
            { "key": "A", "text": "Choice A" },
            { "key": "B", "text": "Choice B" },
            { "key": "C", "text": "Choice C" }
          ],
          "correctChoiceKey": "A",
          "randomizeChoices": true
        }
        """;

        const string rationale = """
        This is the correct answer for A. It is 100% correct because it matches the requirement. https://learn.microsoft.com/some/path

        This is a distractor. It is 100% incorrect because it violates the requirement. https://learn.microsoft.com/another/path
        """;

        using var doc = JsonDocument.Parse(contentJson);
        var errors = QuestionGenerationService.ValidateRationaleFormatForTests("M1", doc.RootElement, rationale);

        Assert.Contains(errors, e => e.Contains("distractor phrase", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateRationaleFormat_ChooseN_RequiresCorrectPhraseForEachCorrectKey()
    {
        const string contentJson = """
        {
          "requiredSelectionCount": 2,
          "solutionCompleteness": "complete",
          "choices": [
            { "key": "A", "text": "Choice A" },
            { "key": "B", "text": "Choice B" },
            { "key": "C", "text": "Choice C" },
            { "key": "D", "text": "Choice D" }
          ],
          "correctChoiceKeys": ["A", "C"]
        }
        """;

        const string rationale = """
        This is the correct answer for A. It is 100% correct because it matches the requirement. https://learn.microsoft.com/some/path

        This is a distractor. It is 100% incorrect because it violates the requirement. https://learn.microsoft.com/another/path

        This is a distractor. It is 100% incorrect because it is not supported. https://learn.microsoft.com/yet/another/path

        This is a distractor. It is 100% incorrect because it is unrelated. https://learn.microsoft.com/yet/another/path
        """;

        using var doc = JsonDocument.Parse(contentJson);
        var errors = QuestionGenerationService.ValidateRationaleFormatForTests("CHOOSE_N", doc.RootElement, rationale);

        Assert.Contains(errors, e => e.Contains("for C", System.StringComparison.Ordinal));
    }
}
