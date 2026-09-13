using FluentValidation;

namespace PMS.Application.Features.Salary.Commands.SetSalaryProfileStatus;

/// <summary>
/// One rule, and the architecture tests are right to insist it exists.
///
/// <para>The command carries an id and a boolean; the boolean cannot be invalid and the id's
/// <em>existence</em> is the handler's business, since only a database can answer it. A validator
/// with one rule is worth more than an exemption from the convention - every exception carved
/// into <c>EachCommand_Should_HaveValidator</c> makes it weaker.</para>
/// </summary>
public sealed class SetSalaryProfileStatusCommandValidator
    : AbstractValidator<SetSalaryProfileStatusCommand>
{
    public SetSalaryProfileStatusCommandValidator()
    {
        RuleFor(c => c.ProfileId)
            .NotEmpty().WithMessage("Which profile?");
    }
}
