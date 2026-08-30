using FluentValidation;

namespace PMS.Application.Features.Orders.Commands.CancelOrder;

/// <summary>
/// Validator for CancelOrderCommand.
/// </summary>
public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Order ID is required");
    }
}
