using FluentValidation;
using PMS.Application.Common.Salary;

namespace PMS.Application.Features.Salary.Commands.GenerateSalary;

public sealed class GenerateSalaryCommandValidator : AbstractValidator<GenerateSalaryCommand>
{
    public GenerateSalaryCommandValidator()
    {
        RuleFor(c => c.Month)
            .InclusiveBetween(1, 12).WithMessage("Choose a month between 1 and 12.");

        RuleFor(c => c.Year)
            .InclusiveBetween(SalaryPeriod.MinYear, SalaryPeriod.MaxYear)
            .WithMessage($"Choose a year between {SalaryPeriod.MinYear} and "
                + $"{SalaryPeriod.MaxYear}.");

        RuleFor(c => c.Lines)
            .NotEmpty().WithMessage("Select at least one employee to generate salary for.");

        // The unique index would catch a repeated profile as a 500 mid-transaction. Catching it
        // here says which employee, before anything is written.
        RuleFor(c => c.Lines)
            .Must(lines => lines.Select(l => l.ProfileId).Distinct().Count() == lines.Count)
            .When(c => c.Lines is { Count: > 0 })
            .WithMessage("The same employee appears twice in this payroll.");

        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProfileId)
                .NotEmpty().WithMessage("Which employee?");

            line.RuleFor(l => l.Bonus)
                .GreaterThanOrEqualTo(0).WithMessage("A bonus cannot be negative.");

            // A negative deduction would be a bonus recorded in the deduction column, where no
            // screen and no report would ever describe it correctly.
            line.RuleFor(l => l.AdvanceDeduction)
                .GreaterThanOrEqualTo(0).WithMessage("An advance deduction cannot be negative.");

            line.RuleFor(l => l.OtherDeduction)
                .GreaterThanOrEqualTo(0).WithMessage("A deduction cannot be negative.");

            line.RuleFor(l => l.AdjustmentNotes).MaximumLength(1000);

            // A bonus or a deduction with no explanation is a number nobody can account for six
            // months later, and this module is read precisely when somebody is querying a figure.
            line.RuleFor(l => l.AdjustmentNotes)
                .NotEmpty()
                .When(l => l.Bonus > 0 || l.OtherDeduction > 0)
                .WithMessage("Say what the bonus or deduction is for.");
        });
    }
}
