using AIExamForge.Application.Generation.DTOs;
using AIExamForge.Domain.Interfaces;
using FluentValidation;

namespace AIExamForge.Application.Generation.Validators;

/// <summary>
/// FluentValidation validator for GenerationRequestDto per openapi.yaml and tasks T084-T085.
/// Validates:
/// - questionCount: 1-50
/// - selectedNodeIds: at least one node selected
/// - questionTypes: at least one valid item type code
/// - difficulty: percentages sum to 100 if specified
/// </summary>
public sealed class GenerationRequestValidator : AbstractValidator<GenerationRequestDto>
{
    public GenerationRequestValidator(IItemTypeRegistry itemTypeRegistry)
    {
        RuleFor(x => x.OutlineId)
            .NotEmpty()
            .WithMessage("Outline ID is required.");

        RuleFor(x => x.SelectedNodeIds)
            .NotNull()
            .WithMessage("Selected node IDs are required.")
            .NotEmpty()
            .WithMessage("At least one outline section must be selected.")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("All selected node IDs must be valid GUIDs.");

        RuleFor(x => x.QuestionCount)
            .InclusiveBetween(1, 50)
            .WithMessage("Question count must be between 1 and 50.");

        RuleFor(x => x.QuestionTypes)
            .NotNull()
            .WithMessage("Question types are required.")
            .NotEmpty()
            .WithMessage("At least one question type must be selected.")
            .Must(types => types.All(t => itemTypeRegistry.IsValidCode(t)))
            .WithMessage(x => GetInvalidTypesMessage(x.QuestionTypes, itemTypeRegistry));

        RuleFor(x => x.RenderMode)
            .IsInEnum()
            .WithMessage("Render mode must be a valid value (Exam or Study).");

        When(x => x.Difficulty != null, () =>
        {
            RuleFor(x => x.Difficulty!.EasyPercent)
                .InclusiveBetween(0, 100)
                .WithMessage("Easy percent must be between 0 and 100.");

            RuleFor(x => x.Difficulty!.MediumPercent)
                .InclusiveBetween(0, 100)
                .WithMessage("Medium percent must be between 0 and 100.");

            RuleFor(x => x.Difficulty!.HardPercent)
                .InclusiveBetween(0, 100)
                .WithMessage("Hard percent must be between 0 and 100.");

            RuleFor(x => x.Difficulty)
                .Must(d => d != null && d.EasyPercent + d.MediumPercent + d.HardPercent == 100)
                .WithMessage("Difficulty percentages must sum to 100.");
        });

        When(x => x.ToneProfile.HasValue, () =>
        {
            RuleFor(x => x.ToneProfile)
                .IsInEnum()
                .WithMessage("Tone profile must be a valid value (Concise, Professional, or ExamLike).");
        });
    }

    private static string GetInvalidTypesMessage(IReadOnlyList<string> types, IItemTypeRegistry registry)
    {
        var invalidTypes = types.Where(t => !registry.IsValidCode(t)).ToList();
        if (invalidTypes.Count == 0)
        {
            return string.Empty;
        }

        return $"Invalid question type(s): {string.Join(", ", invalidTypes)}. " +
               "Valid types are: M1, BA, DA, SC, DOMC, CHOOSE_N, AS, BL, MW, HA, DD, MG, GI, MS, PA, CA, AN, LABS, SA.";
    }
}
