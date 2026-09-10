namespace PMS.Application.Interfaces;

/// <summary>
/// Allocates the next invoice number for the current pharmacy.
///
/// <para><b>Why this is an interface and not a MAX() + 1.</b> Two cashiers completing a sale in
/// the same second both read the same maximum and both write the same invoice number. One of
/// them loses to the unique index, which is the good outcome; the bad one is a system without
/// that index, where the pharmacy ends up with two INV-000452 and no way to tell which return
/// belongs to which. The implementation allocates with a single atomic statement instead.</para>
///
/// <para>Scoped to the pharmacy through the tenant context, never by a parameter — an argument
/// a caller could pass wrongly is an argument a caller will eventually pass wrongly, and here
/// that would hand one pharmacy another pharmacy's numbering.</para>
/// </summary>
public interface IInvoiceNumberGenerator
{
    /// <summary>
    /// Reserves and returns the next number, formatted for display — "INV-000452".
    ///
    /// <para>Call this inside the sale transaction. A number allocated by a sale that then
    /// fails is rolled back with it, so the sequence has no gaps.</para>
    /// </summary>
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
