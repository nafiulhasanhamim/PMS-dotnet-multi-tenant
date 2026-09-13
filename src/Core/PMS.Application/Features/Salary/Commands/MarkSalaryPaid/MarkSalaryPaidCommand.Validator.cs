using FluentValidation;

namespace PMS.Application.Features.Salary.Commands.MarkSalaryPaid;

public sealed class MarkSalaryPaidCommandValidator : AbstractValidator<MarkSalaryPaidCommand>
{
    public MarkSalaryPaidCommandValidator()
    {
        RuleFor(c => c.EntryId)
            .NotEmpty().WithMessage("Which salary entry?");

        // A null date means today, which the handler supplies. An explicitly supplied default
        // DateOnly is a form that posted an empty field, and it would put the expense in year 1.
        RuleFor(c => c.PaymentDate)
            .NotEqual(default(DateOnly))
            .When(c => c.PaymentDate.HasValue)
            .WithMessage("Enter the date the salary was paid.");
    }
}
