using FluentValidation;

namespace PMS.Application.Features.Sales.Commands.CreateSalesReturn;

public sealed class CreateSalesReturnCommandValidator : AbstractValidator<CreateSalesReturnCommand>
{
    public CreateSalesReturnCommandValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("Which sale is this return against?");

        RuleFor(x => x.SaleLineId)
            .NotEmpty().WithMessage("Choose the item being returned.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Enter how much is coming back.");

        // How much is *available* to return is not checked here: it needs the line and its
        // prior returns, and a check made outside the transaction could pass and then be wrong
        // by the time the row is written. The handler does it against the loaded line.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Say why the item is being returned.")
            .MaximumLength(500).WithMessage("Keep the reason under 500 characters.");
    }
}
