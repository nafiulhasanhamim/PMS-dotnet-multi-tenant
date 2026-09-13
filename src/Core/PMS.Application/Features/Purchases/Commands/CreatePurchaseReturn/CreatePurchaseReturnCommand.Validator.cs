using FluentValidation;

namespace PMS.Application.Features.Purchases.Commands.CreatePurchaseReturn;

public sealed class CreatePurchaseReturnCommandValidator
    : AbstractValidator<CreatePurchaseReturnCommand>
{
    public CreatePurchaseReturnCommandValidator()
    {
        RuleFor(c => c.PurchaseId).NotEmpty();
        RuleFor(c => c.PurchaseLineId).NotEmpty();

        RuleFor(c => c.Quantity)
            .GreaterThan(0).WithMessage("Enter how much is going back.");

        // Required, and free text with quick-picks on the form rather than a fixed list. A
        // closed list gets filled in as "Other" for the cases that actually matter - the same
        // reasoning as StockAdjustment's reason, whose audit row this one ends up tagging.
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Say why this is going back.")
            .MaximumLength(400);
    }
}
