using FluentValidation;

namespace PMS.Application.Features.Tenants.Commands.CreateTenantAdmin;

public sealed class CreateTenantAdminCommandValidator : AbstractValidator<CreateTenantAdminCommand>
{
    public CreateTenantAdminCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        // Only enforced when an account is actually created; an existing account keeps its
        // own password and this value is ignored.
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
