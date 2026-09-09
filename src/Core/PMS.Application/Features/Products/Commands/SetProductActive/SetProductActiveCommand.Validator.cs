using FluentValidation;

namespace PMS.Application.Features.Products.Commands.SetProductActive;

public sealed class SetProductActiveCommandValidator : AbstractValidator<SetProductActiveCommand>
{
    public SetProductActiveCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
