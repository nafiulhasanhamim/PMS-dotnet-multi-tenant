using PMS.Application.Common.Products;
using FluentValidation;

namespace PMS.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        // The type-conditional and unit rules live in one place and are shared with update.
        ProductWriteRules.ApplyTo(this);
    }
}
