using PMS.Application.Common.Products;
using FluentValidation;

namespace PMS.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        ProductWriteRules.ApplyTo(this);
    }
}
