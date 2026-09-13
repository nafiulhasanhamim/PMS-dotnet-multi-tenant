using FluentValidation;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Users.Commands.CreateTenantUser;

public sealed class CreateTenantUserCommandValidator : AbstractValidator<CreateTenantUserCommand>
{
    public CreateTenantUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);

        // A pharmacy Admin may create staff, not peers. Further Admins come from a platform
        // operator, and PlatformAdmin can never exist against a pharmacy at all.
        RuleFor(x => x.Role)
            .Must(r => r is UserRole.Pharmacist or UserRole.Employee)
            .WithMessage("Only Pharmacist or Employee can be created here.");
    }
}
