using PMS.Domain.Enums;
using FluentValidation;

namespace PMS.Application.Features.Orders.Commands.UpdateOrderStatus;

/// <summary>
/// Validator for UpdateOrderStatusCommand.
/// </summary>
public sealed class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Order ID is required");

        RuleFor(x => x.NewStatus)
            .IsInEnum().WithMessage("Invalid order status")
            .Must(status => status != OrderStatus.Pending)
            .WithMessage("Cannot manually set status to Pending");
    }
}
