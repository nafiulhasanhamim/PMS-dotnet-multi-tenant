using FluentValidation;

namespace PMS.Application.Features.Salary.Commands.UpdateSalaryProfile;

public sealed class UpdateSalaryProfileCommandValidator
    : AbstractValidator<UpdateSalaryProfileCommand>
{
    public UpdateSalaryProfileCommandValidator()
    {
        RuleFor(c => c.ProfileId)
            .NotEmpty().WithMessage("Which profile?");

        RuleFor(c => c.Designation).MaximumLength(200);

        RuleFor(c => c.MonthlyBaseSalary)
            .GreaterThan(0).WithMessage("Enter a monthly salary greater than zero.");

        RuleFor(c => c.JoiningDate)
            .NotEqual(default(DateOnly)).WithMessage("Enter the joining date.");
    }
}
