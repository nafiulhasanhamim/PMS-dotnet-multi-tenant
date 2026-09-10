using FluentValidation;

namespace PMS.Application.Features.Sales.Commands.CancelSale;

public sealed class CancelSaleCommandValidator : AbstractValidator<CancelSaleCommand>
{
    public CancelSaleCommandValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("Which sale is being cancelled?");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Say why this sale is being cancelled.")
            .MaximumLength(500).WithMessage("Keep the reason under 500 characters.");
    }
}
