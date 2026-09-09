using PMS.Application.Common.Stock;
using FluentValidation;

namespace PMS.Application.Features.Stock.Commands.CreateBatch;

public sealed class CreateBatchCommandValidator : AbstractValidator<CreateBatchCommand>
{
    public CreateBatchCommandValidator()
    {
        BatchWriteRules.ApplyTo(this);

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Choose the product this stock is for.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Enter how much arrived.");

        RuleFor(x => x.PurchasePrice)
            .GreaterThanOrEqualTo(0).WithMessage("A purchase price cannot be negative.");

        // Only on create. An edit may set a past expiry, because historical stock has to be
        // enterable — a pharmacy recording what is already on its shelves will have packs that
        // expired last month, and refusing them would leave that stock invisible rather than
        // flagged. Accepting a past date on a *new* delivery is a different thing entirely:
        // nobody takes delivery of expired stock on purpose, so it is a typo worth catching.
        RuleFor(x => x.ExpiryDate)
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("That expiry date has already passed. Check the date on the pack.")
            .When(x => x.ExpiryDate is not null);
    }
}
