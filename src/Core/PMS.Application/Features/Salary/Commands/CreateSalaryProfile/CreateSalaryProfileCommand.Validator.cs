using FluentValidation;

namespace PMS.Application.Features.Salary.Commands.CreateSalaryProfile;

public sealed class CreateSalaryProfileCommandValidator
    : AbstractValidator<CreateSalaryProfileCommand>
{
    public CreateSalaryProfileCommandValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty().WithMessage("Choose an employee.");

        RuleFor(c => c.Designation).MaximumLength(200);

        // Greater than zero, not merely non-negative. A profile is a statement that somebody is
        // paid; a zero base salary would generate zero-value entries every month and put a name
        // on the payroll that costs nothing, which is a record of nothing.
        RuleFor(c => c.MonthlyBaseSalary)
            .GreaterThan(0).WithMessage("Enter a monthly salary greater than zero.");

        RuleFor(c => c.JoiningDate)
            .NotEqual(default(DateOnly)).WithMessage("Enter the joining date.");
    }
}
