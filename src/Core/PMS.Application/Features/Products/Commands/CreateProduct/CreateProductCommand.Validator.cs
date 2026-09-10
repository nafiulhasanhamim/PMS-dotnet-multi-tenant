using PMS.Application.Common.Products;
using FluentValidation;

namespace PMS.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        // The type-conditional and unit rules live in one place and are shared with update.
        ProductWriteRules.ApplyTo(this);

        // Required here, though the column is nullable. Somebody filling in one product on a
        // form knows what it sells for; the unpriced path exists for bulk import, where the
        // whole point is to defer pricing on two hundred items at once. Allowing it here
        // would turn a forgotten field into a silently unsellable product.
        RuleFor(x => x.PricePerBase)
            .NotNull().WithMessage("Enter what one unit sells for.");
    }
}
