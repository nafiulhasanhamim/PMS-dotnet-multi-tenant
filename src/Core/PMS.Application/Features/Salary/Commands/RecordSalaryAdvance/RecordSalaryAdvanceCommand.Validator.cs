using FluentValidation;

namespace PMS.Application.Features.Salary.Commands.RecordSalaryAdvance;

public sealed class RecordSalaryAdvanceCommandValidator
    : AbstractValidator<RecordSalaryAdvanceCommand>
{
    public RecordSalaryAdvanceCommandValidator()
    {
        RuleFor(c => c.ProfileId)
            .NotEmpty().WithMessage("Choose an employee.");

        // Zero is not an advance, and a negative one is a salary. Both refused here so neither
        // can quietly move an employee's outstanding total the wrong way.
        RuleFor(c => c.Amount)
            .GreaterThan(0).WithMessage("Enter an amount greater than zero.");

        RuleFor(c => c.Reason).MaximumLength(500);

        // NOTE: there is deliberately NO rule capping the advance at the employee's monthly
        // salary. An owner who hands over more than a month's pay has done so; refusing to record
        // it would leave the cash untracked, which is the one outcome this module exists to
        // prevent. Generation caps the DEDUCTION instead, and carries the remainder forward -
        // see SalaryEntry.Generate.
    }
}
