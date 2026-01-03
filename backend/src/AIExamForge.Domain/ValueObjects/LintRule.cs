namespace AIExamForge.Domain.ValueObjects;

/// <summary>
/// Lint rules derived from docs/exam-writing-style-rules.md.
/// Each rule corresponds to a machine-enforceable requirement.
/// </summary>
public enum LintRule
{
    // Section 2: Absolute Prohibitions
    NoTeachingText = 100,
    NoContractions = 101,
    NoForbiddenAnswerPatterns = 102,
    NoTrueFalseItems = 103,

    // Section 3: Required Structural Rules
    MinimumAnswerChoices = 200,
    ParallelAnswerChoices = 201,
    NoExplanationsInChoices = 202,
    NumericAnswerOrdering = 203,

    // Section 4: Question Stem Rules
    StemCompleteness = 300,
    NegativeConstruction = 301,
    InterrogativeClarity = 302,

    // Section 5: Instruction Statement Rules
    InstructionSeparation = 400,
    InstructionPlacement = 401,

    // Section 6: Terminology Rules
    TerminologyConsistency = 500,
    AcronymDefinition = 501,

    // Section 7: Qualifiers and Absolutes
    NoIncorrectAbsolutes = 600,
    SignaledQualifiers = 601,

    // Section 8: Exhibits, Tables, and Graphics
    ExhibitNecessity = 700,
    TableFormatting = 701,
    GraphicsAccessibility = 702,

    // Section 9: Item-Type Compliance
    ValidItemTypeCode = 800,
    ItemTypeConstraints = 801,
    SchemaValidation = 802,

    // Section 10: Rationales
    NoRationaleLeakage = 900
}
