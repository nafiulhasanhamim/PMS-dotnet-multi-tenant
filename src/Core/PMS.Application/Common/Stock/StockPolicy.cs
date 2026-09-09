namespace PMS.Application.Common.Stock;

/// <summary>
/// The numbers this module judges stock by.
///
/// <para><b>Constants for now, and named as one place on purpose.</b> Both of these belong to
/// the pharmacy — a shop turning over paracetamol weekly wants a different expiry horizon from
/// one stocking slow-moving supplies — and both move to per-tenant Settings in a later module.
/// Gathering them here means that change edits one file and a handful of call sites that
/// already pass the value as a parameter, rather than hunting literal 90s through queries,
/// pages and alerts.</para>
/// </summary>
public static class StockPolicy
{
    /// <summary>
    /// How many days ahead counts as "expiring soon": the amber window on every screen and the
    /// basis of the expiry filter.
    ///
    /// <para>Ninety days because that is roughly the point at which a pharmacy can still act —
    /// return it to the supplier, discount it, or stop reordering. A shorter window tells them
    /// only after it is too late to do anything but write it off.</para>
    /// </summary>
    public const int ExpiringSoonWindowDays = 90;

    /// <summary>
    /// How close to expiry a batch has to be before <em>adding</em> stock to it is treated as
    /// probably a mistake.
    ///
    /// <para>Much shorter than the alert window, and deliberately. Adding to a batch that
    /// expires in two months is ordinary — a miscount corrected. Adding to one that expires
    /// this month almost always means fresh stock arrived and somebody reached for the nearest
    /// existing row instead of creating a new batch, which silently gives the new delivery the
    /// old one's expiry date and cost.</para>
    /// </summary>
    public const int AddToExpiringBatchWarningDays = 30;
}
