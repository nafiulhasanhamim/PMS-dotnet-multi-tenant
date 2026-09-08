using FluentValidation;

namespace PMS.Application.Features.Auth.Commands.TenantLogin;

public sealed class TenantLoginCommandValidator : AbstractValidator<TenantLoginCommand>
{
    public TenantLoginCommandValidator()
    {
        RuleFor(x => x.DomainName).NotEmpty().MaximumLength(253);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
    }
}
