namespace PMS.Domain.Enums;

/// <summary>
/// Why a batch's quantity changed, for changes that are not sales.
///
/// <para>A sale is deliberately not one of these. Sales deduct stock through the billing
/// module (Module 5) and are audited by their own sale lines, which carry the price charged
/// and the customer. Folding them in here would produce two competing records of the same
/// event and a stock-adjustment history nobody could read for the handful of entries that
/// actually need explaining.</para>
/// </summary>
public enum AdjustmentType
{
    /// <summary>
    /// Stock arrived or was found. Positive change.
    ///
    /// <para>Not the way to record a new delivery — that is a new batch, with its own expiry
    /// and its own cost. This is for stock that belongs to <em>this</em> batch and was missed:
    /// a miscount on arrival, a returned pack going back on the shelf.</para>
    /// </summary>
    Add = 0,

    /// <summary>
    /// Stock is gone and was not sold. Negative change: damage, breakage, theft, expiry
    /// disposal.
    /// </summary>
    Remove = 1,

    /// <summary>
    /// The recorded quantity was wrong and this is the true figure. The screen collects an
    /// absolute number and the handler computes the difference, because "the shelf has 175"
    /// is what someone counting knows — not "the count is out by five".
    /// </summary>
    Correction = 2,
}
