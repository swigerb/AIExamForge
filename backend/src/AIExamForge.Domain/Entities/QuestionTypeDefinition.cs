using AIExamForge.Domain.ValueObjects;

namespace AIExamForge.Domain.Entities;

/// <summary>
/// Represents an item type definition with its constraints per item-types.md.
/// This is a reference/lookup entity used to validate question generation.
/// </summary>
public sealed class QuestionTypeDefinition
{
    public ItemTypeCode Code { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public bool IsDichotomous { get; }
    public bool IsPolytomous => !IsDichotomous;

    /// <summary>
    /// Minimum number of choices/options required.
    /// </summary>
    public int? MinChoices { get; init; }

    /// <summary>
    /// Maximum number of choices/options allowed.
    /// </summary>
    public int? MaxChoices { get; init; }

    /// <summary>
    /// Minimum number of correct answers required.
    /// </summary>
    public int? MinCorrectAnswers { get; init; }

    /// <summary>
    /// Maximum number of correct answers allowed.
    /// </summary>
    public int? MaxCorrectAnswers { get; init; }

    /// <summary>
    /// Minimum number of distractors required.
    /// </summary>
    public int? MinDistractors { get; init; }

    /// <summary>
    /// Whether order matters for this item type.
    /// </summary>
    public bool OrderMatters { get; init; }

    /// <summary>
    /// Whether this item type requires an instruction statement.
    /// </summary>
    public bool RequiresInstruction { get; init; }

    /// <summary>
    /// Whether this item type supports interactive elements.
    /// </summary>
    public bool IsInteractive { get; init; }

    /// <summary>
    /// Special constraints or notes for this item type from item-types.md.
    /// </summary>
    public IReadOnlyList<string> Constraints { get; init; }

    private QuestionTypeDefinition(
        ItemTypeCode code,
        string displayName,
        string description,
        bool isDichotomous)
    {
        Code = code;
        DisplayName = displayName;
        Description = description;
        IsDichotomous = isDichotomous;
        Constraints = Array.Empty<string>();
    }

    /// <summary>
    /// Static definitions for all 19 item types per item-types.md.
    /// </summary>
    public static IReadOnlyDictionary<string, QuestionTypeDefinition> All { get; } = CreateDefinitions();

    private static Dictionary<string, QuestionTypeDefinition> CreateDefinitions()
    {
        return new Dictionary<string, QuestionTypeDefinition>
        {
            ["M1"] = new(ItemTypeCode.M1, "Multiple Choice, Select One", "Exactly 1 correct answer, at least 2 distractors", true)
            {
                MinCorrectAnswers = 1,
                MaxCorrectAnswers = 1,
                MinDistractors = 2,
                Constraints = ["True/false style items are not acceptable"]
            },

            ["BA"] = new(ItemTypeCode.BA, "Best Answer", "One answer is best; must instruct 'select the BEST answer'", true)
            {
                MinCorrectAnswers = 1,
                MaxCorrectAnswers = 1,
                MinDistractors = 2,
                RequiresInstruction = true,
                Constraints = ["Must instruct 'select the BEST answer'"]
            },

            ["DA"] = new(ItemTypeCode.DA, "Dynamic Answer Choice", "Author >=2 correct answers and >=6 distractors; delivery shows 1 correct + 2-3 distractors", true)
            {
                MinCorrectAnswers = 2,
                MinDistractors = 6,
                Constraints = ["Delivery shows 1 correct + (2 or 3) distractors based on distractor count"]
            },

            ["SC"] = new(ItemTypeCode.SC, "Sentence Correction", "Include 'No change is needed.' as first choice, do not randomize", true)
            {
                MinCorrectAnswers = 1,
                MaxCorrectAnswers = 1,
                MinDistractors = 2,
                RequiresInstruction = true,
                OrderMatters = true,
                Constraints =
                [
                    "Include 'No change is needed.' as the first choice",
                    "Do not randomize answer choices",
                    "Instruction statement precedes the question"
                ]
            },

            ["DOMC"] = new(ItemTypeCode.DOMC, "Dynamic Option Multiple Choice", "Set-based Yes/No questions; learner sees 3 randomly selected", true)
            {
                MinCorrectAnswers = 1,
                MinDistractors = 3,
                Constraints =
                [
                    "Same problem statement; different solution each item",
                    "Learner answers Yes/No",
                    "Must appear last; learner cannot navigate back",
                    "Set counts as a single item"
                ]
            },

            ["CHOOSE_N"] = new(ItemTypeCode.CHOOSE_N, "Choose N", "Must specify exact number to select; 'choose all that apply' not allowed", false)
            {
                MinChoices = 4, // n+2 where n>=2
                RequiresInstruction = true,
                Constraints =
                [
                    "Must specify exact number to select",
                    "'Choose all that apply' not allowed",
                    "Must offer at least n+2 choices"
                ]
            },

            ["AS"] = new(ItemTypeCode.AS, "Active Screen", "Screenshot-based interactive config, minimum 4 active areas", false)
            {
                MinChoices = 4,
                IsInteractive = true,
                Constraints = ["Minimum 4 active areas"]
            },

            ["BL"] = new(ItemTypeCode.BL, "Build List", "Order is critical, 3-5 steps, up to 2 valid orderings", false)
            {
                MinChoices = 3,
                MaxChoices = 5,
                OrderMatters = true,
                RequiresInstruction = true,
                Constraints =
                [
                    "Used when order is critical",
                    "Up to 2 solutions allowed (same options; different order)",
                    "Must state number of tasks to move in instruction"
                ]
            },

            ["MW"] = new(ItemTypeCode.MW, "Magnetic Words", "Code blocks variant of Build List, 6-12 blocks, 3-5 steps", false)
            {
                MinChoices = 6,
                MaxChoices = 12,
                OrderMatters = true,
                Constraints = ["3-5 steps in final answer"]
            },

            ["HA"] = new(ItemTypeCode.HA, "Hot Area", "Click hot areas in image/screenshot, 4-8 areas, select at least 2", false)
            {
                MinChoices = 4,
                MaxChoices = 8,
                MinCorrectAnswers = 2,
                IsInteractive = true,
                Constraints = ["Requires selecting at least 2 areas"]
            },

            ["DD"] = new(ItemTypeCode.DD, "Drag and Drop", "Sources 3-8, Targets 2-5, empty targets not allowed", false)
            {
                MinChoices = 3,
                MaxChoices = 8,
                IsInteractive = true,
                RequiresInstruction = true,
                Constraints =
                [
                    "Sources: min 3, max 8",
                    "Targets: min 2, max 5",
                    "Empty targets not allowed",
                    "Instruction must specify whether sources can be reused and/or unused"
                ]
            },

            ["MG"] = new(ItemTypeCode.MG, "Matching", "Matching variant of Drag and Drop, targets 3-6", false)
            {
                MinChoices = 3,
                MaxChoices = 6,
                IsInteractive = true,
                Constraints = ["Empty targets not allowed"]
            },

            ["GI"] = new(ItemTypeCode.GI, "Graphics Interpretation", "1 graphic, 2-5 partial statements, dropdowns with 3-5 choices", false)
            {
                MinChoices = 2,
                MaxChoices = 5,
                IsInteractive = true,
                Constraints =
                [
                    "Exactly 1 embedded graphic",
                    "Two dropdown lists in answer area",
                    "Each dropdown contains 3-5 choices",
                    "Use bold '[answer choice]' placeholder in partial statements"
                ]
            },

            ["MS"] = new(ItemTypeCode.MS, "Multi-Source Reasoning", "Reference >=3 data points, >=3 Yes/No statements", false)
            {
                MinChoices = 3,
                Constraints = ["Reference >=3 data points"]
            },

            ["PA"] = new(ItemTypeCode.PA, "Two-Part Analysis", "2 answer columns, >=3 rows, non-negative statements", false)
            {
                MinChoices = 3,
                Constraints =
                [
                    "2 answer columns",
                    "Non-negative statements",
                    "1 answer per column"
                ]
            },

            ["CA"] = new(ItemTypeCode.CA, "Code/Table Analysis", "3-8 statements, non-negative, predict outcomes", false)
            {
                MinChoices = 3,
                MaxChoices = 8,
                Constraints =
                [
                    "Non-negative statements",
                    "Predict outcomes"
                ]
            },

            ["AN"] = new(ItemTypeCode.AN, "Statement Analysis", "3-8 statements, non-negative, determine true/false", false)
            {
                MinChoices = 3,
                MaxChoices = 8,
                Constraints = ["Non-negative statements"]
            },

            ["LABS"] = new(ItemTypeCode.LABS, "Task-Based Labs", "Virtual environment tasks, scoring based on end state", false)
            {
                IsInteractive = true,
                Constraints = ["Tasks phrased as problems to solve"]
            },

            ["SA"] = new(ItemTypeCode.SA, "Short Answer Code", "Free-text code/command entry, <=10 lines total", false)
            {
                Constraints =
                [
                    "May include syntax checking (not correctness)",
                    "Provide necessary names/values in stem/exhibits",
                    "<=10 lines total; average 4-6 lines",
                    "<=5 pieces in single-line",
                    "<=4 lines in multi-line"
                ]
            }
        };
    }

    public static QuestionTypeDefinition GetByCode(string code)
    {
        return All.TryGetValue(code, out var definition)
            ? definition
            : throw new ArgumentException($"Unknown item type code: {code}", nameof(code));
    }

    public static QuestionTypeDefinition GetByCode(ItemTypeCode code) => GetByCode(code.Code);
}
