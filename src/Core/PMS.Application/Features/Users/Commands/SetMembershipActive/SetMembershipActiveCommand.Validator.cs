using FluentValidation;

namespace PMS.Application.Features.Users.Commands.SetMembershipActive;

public sealed class SetMembershipActiveCommandValidator
    : AbstractValidator<SetMembershipActiveCommand>
{
    public SetMembershipActiveCommandValidator()
    {
        RuleFor(x => x.MembershipId).NotEmpty();
    }
}
