namespace PMS.Domain.Billing;

/// <summary>
/// What a sale actually earned. <b>The easiest number in the system to get subtly wrong</b>, and
/// the one a pharmacy owner will act on.
///
/// <para>Three corrections from earlier modules all have to be respected together, and each of
/// them fails silently on its own — no exception, no empty screen, just a margin that is wrong by
/// a plausible-looking amount:</para>
///
/// <list type="number">
/// <item><description><b>Revenue is <c>NetLineTotal</c>, never <c>LineTotal</c>.</b> Module 5
/// split the bill-level discount proportionally across the lines and stored each line's share.
/// Using the pre-discount figure reports inflated margin on every discounted sale, and the more
/// generous the discount the more inflated it is.</description></item>
/// <item><description><b>Cost comes from the batch, never from the product.</b> Each batch was
/// bought at its own price — that is the entire reason batches exist. A product's current
/// purchase price is what the <em>next</em> delivery cost, which has nothing to do with what the
/// units on last month's invoice cost.</description></item>
/// <item><description><b>A cancelled sale is excluded, not zeroed.</b> It never happened: no
/// revenue, no cost, no transaction count. Zeroing it would leave it counted in every
/// denominator — average basket size, transactions per hour, margin per sale.</description></item>
/// </list>
///
/// <para>The third is a filter rather than arithmetic, so it lives in the queries. The first two
/// are here, in one place, so that "profit" means exactly one thing across eight reports.</para>
///
/// <para><b>Nothing here rounds.</b> Aggregation happens over unrounded values and presentation
/// rounds once, at the end. Rounding per line and then summing drifts by a paisa per line, which
/// on a month of sales is a figure somebody will eventually try to reconcile.</para>
/// </summary>
public static class ProfitMath
{
    /// <summary>
    /// What the pharmacy paid for the units on one sale line.
    ///
    /// <para><paramref name="purchasePricePerBaseUnit"/> is the <em>batch's</em> price. The
    /// column is <c>DECIMAL(18,4)</c> rather than 2 because a per-base-unit cost derived from a
    /// bulk pack is frequently fractional, and rounding it here would misstate margin on every
    /// sale out of that batch.</para>
    /// </summary>
    public static decimal LineCost(int quantityInBaseUnits, decimal purchasePricePerBaseUnit) =>
        quantityInBaseUnits * purchasePricePerBaseUnit;

    /// <summary>
    /// One line's contribution to gross profit.
    /// </summary>
    /// <param name="netLineTotal">
    /// <b><c>SaleLine.NetLineTotal</c></b> — after the line's share of the bill discount. Passing
    /// <c>LineTotal</c> here is the single most likely way to make every report wrong.
    /// </param>
    public static decimal LineProfit(
        decimal netLineTotal, int quantityInBaseUnits, decimal purchasePricePerBaseUnit) =>
        netLineTotal - LineCost(quantityInBaseUnits, purchasePricePerBaseUnit);

    /// <summary>
    /// What a return takes back out of profit.
    ///
    /// <para>A return reverses <em>both</em> sides: the money refunded and the cost of the goods
    /// that came back onto the shelf. Subtracting only the refund would treat returned stock as
    /// though it had evaporated, and the pharmacy would appear to lose its full sale price on
    /// every return rather than its margin.</para>
    ///
    /// <para><b>Attributed to the period the return happened in</b>, not the period of the
    /// original sale — see the module doc. A sale in August returned in September reduces
    /// September, which is when the cash actually went back.</para>
    /// </summary>
    public static decimal ReturnImpact(
        decimal refundAmount, int quantityReturnedInBaseUnits, decimal purchasePricePerBaseUnit) =>
        refundAmount - LineCost(quantityReturnedInBaseUnits, purchasePricePerBaseUnit);

    /// <summary>
    /// Gross profit less what it cost to keep the doors open.
    ///
    /// <para>Operating expenses come from <c>IOperatingExpenses</c>, which returns zero until
    /// Module 9 (Salary) fills it in. Until then this is an identity function, and the monthly
    /// report says so on screen rather than quietly presenting gross profit as net.</para>
    /// </summary>
    public static decimal NetProfit(decimal grossProfit, decimal operatingExpenses) =>
        grossProfit - operatingExpenses;

    /// <summary>
    /// Profit as a percentage of revenue.
    ///
    /// <para>Zero revenue gives zero rather than a divide-by-zero or a null: a product type with
    /// no sales in the period has no margin to report, and a blank cell in a column of
    /// percentages reads as a missing figure rather than an absent one. Negative revenue cannot
    /// occur — a period whose returns exceed its sales still has non-negative gross revenue,
    /// because returns are accounted separately.</para>
    /// </summary>
    public static decimal MarginPercent(decimal profit, decimal revenue) =>
        revenue == 0m ? 0m : profit / revenue * 100m;

    /// <summary>
    /// Weighted average cost per base unit across a set of batches.
    ///
    /// <para>Weighted by quantity, not a plain average of the prices. Ten units at ৳1 and one
    /// unit at ৳100 average ৳1.09 a unit, not ৳50.50 — and the stock valuation's total depends on
    /// getting that right, because it is the number an owner reads as "money tied up".</para>
    ///
    /// <para>Returns zero for no stock, which is the honest answer: there is nothing to have an
    /// average cost of.</para>
    /// </summary>
    public static decimal WeightedAverageCost(decimal totalValue, int totalQuantityInBaseUnits) =>
        totalQuantityInBaseUnits == 0 ? 0m : totalValue / totalQuantityInBaseUnits;
}
