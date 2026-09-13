using FluentValidation;

namespace PMS.Application.Features.Sales.Commands.CompleteSale;

/// <summary>
/// Shape checks only.
///
/// <para>What is <em>not</em> here is as deliberate as what is. Stock availability, prices,
/// role discount caps and the antibiotic prescription rules all need the database or the
/// caller's role, and a validator that reached for either would be doing the handler's job in a
/// place with no transaction around it — checking stock here and deducting it there is precisely
/// the race the handler exists to close. This rejects requests that could not be valid whatever
/// the database holds.</para>
/// </summary>
public sealed class CompleteSaleCommandValidator : AbstractValidator<CompleteSaleCommand>
{
    /// <summary>
    /// A cart this long is a client fault, not a customer with 201 items. Bounded so a
    /// malformed request cannot make the server walk a million FEFO allocations.
    /// </summary>
    public const int MaxItems = 200;

    public CompleteSaleCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Add at least one item before completing the sale.")
            .Must(items => items is null || items.Count <= MaxItems)
            .WithMessage($"A single sale can hold at most {MaxItems} items.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("Every cart row needs a product.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Enter a quantity greater than zero.");
        });

        RuleFor(x => x.CashReceived)
            .GreaterThanOrEqualTo(0).WithMessage("Cash received cannot be negative.");

        // The two discount fields only make sense together. Half a discount is a client bug,
        // and guessing which half was meant would either give away money or ignore an intended
        // discount - both silently.
        RuleFor(x => x.DiscountValue)
            .NotNull().WithMessage("Enter the discount, or clear the discount type.")
            .GreaterThan(0).WithMessage("A discount has to be greater than zero.")
            .When(x => x.DiscountType is not null);

        RuleFor(x => x.DiscountType)
            .NotNull().WithMessage("Choose whether the discount is a percentage or an amount.")
            .When(x => x.DiscountValue is not null);

        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(100)
            .WithMessage("A percentage discount cannot be more than 100%.")
            .When(x => x.DiscountType == Domain.Enums.DiscountType.Percent
                && x.DiscountValue is not null);

        RuleFor(x => x.CustomerName)
            .MaximumLength(200).WithMessage("That name is too long.");

        RuleFor(x => x.CustomerPhone)
            .MaximumLength(40).WithMessage("That phone number is too long.");
    }
}
