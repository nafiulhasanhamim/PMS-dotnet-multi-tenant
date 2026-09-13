using PMS.Application.Common.Stock;
using FluentValidation;

namespace PMS.Application.Features.Stock.Commands.UpdateBatch;

public sealed class UpdateBatchCommandValidator : AbstractValidator<UpdateBatchCommand>
{
    public UpdateBatchCommandValidator()
    {
        BatchWriteRules.ApplyTo(this);

        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.PurchasePrice)
            .GreaterThanOrEqualTo(0).WithMessage("A purchase price cannot be negative.");

        // No "expiry must be in the future" rule here, unlike create.
        //
        // A pharmacy entering the stock already on its shelves will have packs that expired
        // last month, and it needs to record them — that is precisely the stock somebody has
        // to go and pull. Refusing the date would leave it either unrecorded or entered with a
        // fictional one, and a fictional expiry is the worse outcome by a distance: it looks
        // correct and it drives the alerts.
        //
        // The upper bound from BatchWriteRules still applies, so a typo of 2206 is caught in
        // both directions.
    }
}
