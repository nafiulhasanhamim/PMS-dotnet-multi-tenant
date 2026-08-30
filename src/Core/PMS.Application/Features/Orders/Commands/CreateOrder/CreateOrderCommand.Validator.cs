using FluentValidation;

namespace PMS.Application.Features.Orders.Commands.CreateOrder;

/// <summary>
/// Validator for CreateOrderCommand.
/// </summary>
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required");

        RuleFor(x => x.ShippingAddress)
            .NotNull().WithMessage("Shipping address is required")
            .SetValidator(new CreateOrderAddressDtoValidator());

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Order items are required")
            .NotEmpty().WithMessage("Order must have at least one item");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateOrderItemDtoValidator());

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}

/// <summary>
/// Validator for CreateOrderItemDto.
/// </summary>
public sealed class CreateOrderItemDtoValidator : AbstractValidator<CreateOrderItemDto>
{
    public CreateOrderItemDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero");
    }
}

/// <summary>
/// Validator for CreateOrderAddressDto.
/// </summary>
public sealed class CreateOrderAddressDtoValidator : AbstractValidator<CreateOrderAddressDto>
{
    public CreateOrderAddressDtoValidator()
    {
        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("Street is required")
            .MaximumLength(200).WithMessage("Street cannot exceed 200 characters");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required")
            .MaximumLength(100).WithMessage("State cannot exceed 100 characters");

        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("Postal code is required")
            .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(100).WithMessage("Country cannot exceed 100 characters");
    }
}
