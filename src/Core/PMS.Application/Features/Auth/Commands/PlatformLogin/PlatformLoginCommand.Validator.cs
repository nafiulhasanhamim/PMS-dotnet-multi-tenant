using FluentValidation;

namespace PMS.Application.Features.Auth.Commands.PlatformLogin;

public sealed class PlatformLoginCommandValidator : AbstractValidator<PlatformLoginCommand>
{
    public PlatformLoginCommandValidator()
    {
        // Shape only. Whether the credentials are *correct* is decided by the handler, which
        // answers every failure identically so nothing here leaks which part was wrong.
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
    }
}
