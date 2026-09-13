namespace PMS.Application.Interfaces;

/// <summary>
/// Allocates the next purchase number for the current pharmacy.
///
/// <para><b>Why this is an interface and not a count + 1.</b> Two deliveries booked in at the
/// same moment both read the same count and both claim PUR-000124. One loses to the unique index,
/// which is the good outcome; the bad one is a pharmacy with two PUR-000124 and no way to say
/// which bill a payment settled. The implementation allocates with a single atomic statement.</para>
///
/// <para>Scoped to the pharmacy through the tenant context, never by a parameter — an argument a
/// caller could pass wrongly is an argument a caller will eventually pass wrongly, and here that
/// would hand one pharmacy another pharmacy's numbering.</para>
/// </summary>
public interface IPurchaseNumberGenerator
{
    /// <summary>
    /// Reserves and returns the next number, formatted for display — "PUR-000124".
    ///
    /// <para>Call this inside the purchase transaction. A number allocated by a purchase that then
    /// fails is rolled back with it, so the sequence has no gaps.</para>
    /// </summary>
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
