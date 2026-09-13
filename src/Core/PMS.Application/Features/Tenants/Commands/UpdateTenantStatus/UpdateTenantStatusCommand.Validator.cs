using FluentValidation;

namespace PMS.Application.Features.Tenants.Commands.UpdateTenantStatus;

public sealed class UpdateTenantStatusCommandValidator : AbstractValidator<UpdateTenantStatusCommand>
{
    public UpdateTenantStatusCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}
