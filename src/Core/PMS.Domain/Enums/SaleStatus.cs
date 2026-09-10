namespace PMS.Domain.Enums;

/// <summary>
/// Whether a sale still counts.
///
/// <para>There is deliberately no "Edited" or "Amended". A completed sale is a historical
/// fact — the customer has the invoice and the money has moved — so the corrective paths are
/// a return (part of it came back) and a cancellation (the whole thing was a mistake). Both
/// leave the original readable, which is the property that makes the day's takings
/// reconcilable at all.</para>
/// </summary>
public enum SaleStatus
{
    /// <summary>Sold. Counts towards revenue, profit and the antibiotic register.</summary>
    Completed = 0,

    /// <summary>
    /// Reversed in full by an Admin. Stock has been restored and the sale is excluded from
    /// every total. The row stays, because "which sales were cancelled, and why" is a
    /// question an owner asks about their own staff.
    /// </summary>
    Cancelled = 1,
}
