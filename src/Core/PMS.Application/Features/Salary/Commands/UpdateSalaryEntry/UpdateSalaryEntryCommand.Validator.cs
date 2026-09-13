using FluentValidation;

namespace PMS.Application.Features.Salary.Commands.UpdateSalaryEntry;

public sealed class UpdateSalaryEntryCommandValidator
    : AbstractValidator<UpdateSalaryEntryCommand>
{
    public UpdateSalaryEntryCommandValidator()
    {
        RuleFor(c => c.EntryId)
            .NotEmpty().WithMessage("Which salary entry?");

        RuleFor(c => c.Bonus)
            .GreaterThanOrEqualTo(0).WithMessage("A bonus cannot be negative.");

        RuleFor(c => c.AdvanceDeduction)
            .GreaterThanOrEqualTo(0).WithMessage("An advance deduction cannot be negative.");

        RuleFor(c => c.OtherDeduction)
            .GreaterThanOrEqualTo(0).WithMessage("A deduction cannot be negative.");

        RuleFor(c => c.AdjustmentNotes).MaximumLength(1000);

        RuleFor(c => c.AdjustmentNotes)
            .NotEmpty()
            .When(c => c.Bonus > 0 || c.OtherDeduction > 0)
            .WithMessage("Say what the bonus or deduction is for.");
    }
}
