using PMS.Domain.Enums;
using FluentValidation;

namespace PMS.Application.Features.Stock.Commands.AdjustBatch;

public sealed class AdjustBatchCommandValidator : AbstractValidator<AdjustBatchCommand>
{
    public const int ReasonMaxLength = 500;

    public AdjustBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();

        RuleFor(x => x.AdjustmentType)
            .IsInEnum().WithMessage("Choose what kind of change this is.");

        // A required reason is the entire justification for this table existing. An adjustment
        // with no reason records that a number changed and destroys the only thing anyone will
        // later want to know about it, so this is not a field to make optional for
        // convenience.
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Say why the quantity is changing.")
            .MaximumLength(ReasonMaxLength)
            .WithMessage($"A reason cannot be longer than {ReasonMaxLength} characters.");

        // Separate rules by type, because zero means different things.
        //
        // Adding or removing nothing is a no-op somebody typed by accident, and writing an
        // audit row for it is noise in the one history that has to stay readable.
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Enter how much to add or remove.")
            .When(x => x.AdjustmentType is AdjustmentType.Add or AdjustmentType.Remove);

        // A correction to zero is entirely legitimate — the shelf is empty and the system
        // thinks otherwise, which is exactly the discrepancy this is for.
        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("A quantity cannot be negative.")
            .When(x => x.AdjustmentType == AdjustmentType.Correction);
    }
}
