using FluentValidation;

namespace PMS.Application.Features.Purchases.Commands.CreatePurchase;

/// <summary>
/// What can be checked without touching the database.
///
/// <para><b>Deliberately thin.</b> Whether the expiry date is required, whether the batch number
/// is already taken, and whether the quantity resolves to whole base units are all rules that
/// depend on the product — and all three already exist, once, inside
/// <c>CreateBatchCommandHandler</c>. Re-stating any of them here would be a second implementation
/// that could drift from the one Add Stock uses, which is precisely what this module is supposed
/// to avoid. They come back as field errors from the batch command instead.</para>
///
/// <para>What is left is shape: there is at least one line, the numbers entered are positive, and
/// the same batch number is not used twice within this one form.</para>
/// </summary>
public sealed class CreatePurchaseCommandValidator : AbstractValidator<CreatePurchaseCommand>
{
    public CreatePurchaseCommandValidator()
    {
        RuleFor(c => c.SupplierId)
            .NotEmpty().WithMessage("Choose the supplier this delivery came from.");

        RuleFor(c => c.Lines)
            .NotEmpty().WithMessage("A purchase needs at least one item.");

        RuleFor(c => c.Notes).MaximumLength(1000);

        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .NotEmpty().WithMessage("Choose a product for every line.");

            line.RuleFor(l => l.BatchNumber)
                .NotEmpty().WithMessage("Every line needs a batch number.")
                .MaximumLength(100);

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be more than zero.");

            // Zero is allowed: a free sample or a bonus pack genuinely costs nothing, and it is
            // still stock that arrived on a bill. Negative is not.
            line.RuleFor(l => l.PurchasePrice)
                .GreaterThanOrEqualTo(0).WithMessage("Price cannot be negative.");

            line.RuleFor(l => l.Notes).MaximumLength(500);
        });

        // Two lines claiming the same batch number for the same product would be refused by the
        // batch command on the second line — after the first had already created a batch inside
        // the transaction. Catching it here turns a rollback into a form error naming the row.
        RuleFor(c => c.Lines)
            .Must(NoDuplicateBatchNumbersWithinTheForm)
            .WithMessage(
                "Two lines use the same batch number for the same product. Each delivery of a "
                + "product needs its own batch number.")
            .When(c => c.Lines is { Count: > 1 });
    }

    private static bool NoDuplicateBatchNumbersWithinTheForm(
        IReadOnlyList<CreatePurchaseLine> lines) =>
        lines
            .Where(l => !string.IsNullOrWhiteSpace(l.BatchNumber))
            .Select(l => (l.ProductId, Number: l.BatchNumber.Trim().ToLowerInvariant()))
            .Distinct()
            .Count()
        == lines.Count(l => !string.IsNullOrWhiteSpace(l.BatchNumber));
}
