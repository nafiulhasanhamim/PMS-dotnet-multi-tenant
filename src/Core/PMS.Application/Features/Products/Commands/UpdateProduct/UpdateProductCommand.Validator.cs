using PMS.Application.Common.Products;
using FluentValidation;

namespace PMS.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        ProductWriteRules.ApplyTo(this);

        // Required, as on create. Editing an unpriced product through this form is one of the
        // ways to complete its setup, so demanding the price is the desired behaviour rather
        // than an obstacle.
        RuleFor(x => x.PricePerBase)
            .NotNull().WithMessage("Enter what one unit sells for.");
    }
}
