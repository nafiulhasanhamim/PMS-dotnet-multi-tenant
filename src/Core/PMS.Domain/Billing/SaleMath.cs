using PMS.Domain.Enums;

namespace PMS.Domain.Billing;

/// <summary>
/// The money arithmetic behind a sale. <b>Two calculations here have to be exact, and both are
/// expensive to correct after the fact</b> — one because it decides what a customer is refunded
/// months later, the other because it decides what every profit figure ever reported was
/// based on.
///
/// <para><b>Why this is in the domain and not next to the handler.</b> The discount split is
/// not a policy the pharmacy chooses; it is what the numbers mean. Role caps and who may
/// discount are policy and live in the application layer. Putting the arithmetic here means
/// the unit tests exercise the rule itself, with no database, no roles and no HTTP.</para>
///
/// <para><b>Rounding is away from zero, not to even.</b> .NET's default
/// <see cref="MidpointRounding.ToEven"/> would turn a ৳13.125 refund into ৳13.12, and a
/// customer handed back two paisa less than the arithmetic says is a customer who is right and
/// a receipt that is wrong. Away-from-zero is also what a person does with a pencil.</para>
/// </summary>
public static class SaleMath
{
    /// <summary>Money is stored and compared at two decimal places, everywhere.</summary>
    public const int MoneyDecimals = 2;

    /// <summary>The one rounding rule. See the class remarks for why it is not the default.</summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, MoneyDecimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The taka figure a discount actually deducts.
    ///
    /// <para>Capped at the subtotal, so a mistyped flat discount cannot produce a negative
    /// bill and hand money to the customer. Capped here rather than only in the validator
    /// because this is the function every caller goes through.</para>
    /// </summary>
    public static decimal DiscountAmountFor(decimal subtotal, DiscountType type, decimal value)
    {
        if (subtotal <= 0m || value <= 0m)
        {
            return 0m;
        }

        var raw = type switch
        {
            DiscountType.Percent => subtotal * value / 100m,
            DiscountType.Flat => value,
            _ => 0m,
        };

        var rounded = Round(raw);
        var ceiling = Round(subtotal);

        return rounded < 0m ? 0m : rounded > ceiling ? ceiling : rounded;
    }

    /// <summary>
    /// Splits <paramref name="amount"/> across parts in proportion to
    /// <paramref name="weights"/>, so that the parts sum back to it <em>exactly</em>.
    ///
    /// <para><b>The primitive both of the module's splits are built on.</b> Rounding each part
    /// independently does not always add up: three parts of ৳111 sharing a ৳10 discount give
    /// ৳3.33 three times, which is ৳9.99. The missing paisa goes onto the last part, so
    /// <c>sum(parts) == amount</c> holds. Any rule that reconciles would do; what matters is
    /// that one exists and that there is a single place it is applied. The residual is bounded
    /// by half a paisa per part, so the last part is never visibly distorted.</para>
    ///
    /// <para>The reconciliation is signed, because rounding overshoots as readily as it
    /// undershoots.</para>
    /// </summary>
    public static decimal[] Distribute(decimal amount, IReadOnlyList<decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var parts = new decimal[weights.Count];

        if (weights.Count == 0 || amount == 0m)
        {
            return parts;
        }

        var totalWeight = 0m;
        foreach (var weight in weights)
        {
            totalWeight += weight;
        }

        if (totalWeight <= 0m)
        {
            // Nothing to apportion against. A sale whose lines total zero is a data-entry
            // mistake rather than an arithmetic one, and the validator refuses it upstream —
            // this returns zeroes rather than dividing by zero.
            return parts;
        }

        var running = 0m;

        for (var i = 0; i < weights.Count; i++)
        {
            parts[i] = Round(amount / totalWeight * weights[i]);
            running += parts[i];
        }

        parts[^1] += amount - running;

        return parts;
    }

    /// <summary>
    /// Distributes a bill-level discount across the lines in proportion to what each
    /// contributed to the subtotal.
    ///
    /// <para><b>Why store a per-line share at all.</b> A bill-level discount looks like one
    /// number on the invoice, and it is tempting to keep it that way. Then somebody returns one
    /// item out of five. Without a share per line there is nothing to refund from except the
    /// sticker price, and the pharmacy hands back money it never took — on every discounted
    /// sale, quietly, forever. The share is computed once at sale time and stored, because it
    /// has to survive later price changes and later edits to the discount rules.</para>
    /// </summary>
    /// <param name="lineTotals">Each line's pre-discount total, in the order they are stored.</param>
    /// <param name="discountAmount">The figure from <see cref="DiscountAmountFor"/>.</param>
    /// <returns>One share per line, in the same order, summing to <paramref name="discountAmount"/>.</returns>
    public static decimal[] SplitDiscount(
        IReadOnlyList<decimal> lineTotals, decimal discountAmount) =>
        discountAmount <= 0m
            ? new decimal[lineTotals?.Count ?? 0]
            : Distribute(discountAmount, lineTotals);

    /// <summary>
    /// Splits what a customer was quoted for one cart item across the sale lines that FEFO
    /// produced for it.
    ///
    /// <para><b>The subtler of the two splits, and the easier to miss.</b> Selling 5 pieces of
    /// a product priced ৳10 a strip of 3 is ৳16.67. If FEFO takes 4 from one batch and 1 from
    /// another, pricing each line on its own gives ৳13.33 + ৳3.33 = ৳16.66 — a paisa short of
    /// what the screen showed the cashier, on a bill the customer is holding. So the item's
    /// total is computed once from the quoted price and then apportioned by base-unit quantity,
    /// which makes the split invisible on the invoice, as it should be: which batch a tablet
    /// came out of is the pharmacy's business, not the customer's.</para>
    /// </summary>
    /// <param name="itemTotal">Quantity × unit price, as quoted for the whole cart item.</param>
    /// <param name="baseUnitQuantities">Base units taken from each batch, in order.</param>
    public static decimal[] SplitLineTotals(
        decimal itemTotal, IReadOnlyList<int> baseUnitQuantities)
    {
        ArgumentNullException.ThrowIfNull(baseUnitQuantities);

        var weights = new decimal[baseUnitQuantities.Count];
        for (var i = 0; i < baseUnitQuantities.Count; i++)
        {
            weights[i] = baseUnitQuantities[i];
        }

        return Distribute(itemTotal, weights);
    }

    /// <summary>
    /// What a return pays back.
    ///
    /// <para><b>Proportional to <c>NetLineTotal</c>, never <c>LineTotal</c>.</b> The customer
    /// paid the discounted price, so that is what comes back. Refunding from the pre-discount
    /// figure overpays on every discounted sale — and the more generous the discount, the more
    /// it overpays, which is the wrong way round.</para>
    ///
    /// <para><b>The completing return is exact.</b> A line returned in four parts of ten would
    /// otherwise refund ৳13.13 four times against a ৳52.50 line and overpay two paisa, which
    /// also breaks the property that returning everything on a sale refunds exactly
    /// <c>Sale.NetTotal</c>. So the return that empties a line pays the remainder rather than
    /// its own proportion — the same reconciliation idea as the discount residual, applied to
    /// the other end of the sale.</para>
    /// </summary>
    /// <param name="quantityReturnedInBaseUnits">What is coming back now.</param>
    /// <param name="quantitySoldInBaseUnits">The line's original quantity.</param>
    /// <param name="netLineTotal">What the customer paid for the whole line, after discount.</param>
    /// <param name="alreadyReturnedInBaseUnits">Quantity returned against this line before now.</param>
    /// <param name="alreadyRefunded">Taka already refunded against this line.</param>
    public static decimal RefundFor(
        int quantityReturnedInBaseUnits,
        int quantitySoldInBaseUnits,
        decimal netLineTotal,
        int alreadyReturnedInBaseUnits = 0,
        decimal alreadyRefunded = 0m)
    {
        if (quantitySoldInBaseUnits <= 0 || quantityReturnedInBaseUnits <= 0)
        {
            return 0m;
        }

        if (alreadyReturnedInBaseUnits + quantityReturnedInBaseUnits >= quantitySoldInBaseUnits)
        {
            var remainder = netLineTotal - alreadyRefunded;
            return remainder < 0m ? 0m : Round(remainder);
        }

        return Round(
            (decimal)quantityReturnedInBaseUnits / quantitySoldInBaseUnits * netLineTotal);
    }
}
