using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// A distributor the pharmacy buys from.
///
/// <para><b>Carries no balance.</b> What is owed to a supplier is the sum of their purchases,
/// less returns, less payments — three tables that change independently, and any one of them
/// changing while a stored total did not is a figure nobody can trust. It is computed on every
/// read by <c>ISupplierBalanceQueries</c>, which is the only place that arithmetic exists. See
/// <c>docs/04-suppliers-and-purchase.md</c>.</para>
///
/// <para><b>Deactivation is a soft delete, and it must stay that way.</b> A supplier is
/// referenced by every purchase ever recorded against them; deleting the row would either orphan
/// that history or cascade it away, and the purchases are the evidence behind the money. An
/// inactive supplier disappears from the "new purchase" dropdown and keeps every page that shows
/// what was bought from them.</para>
/// </summary>
public sealed class Supplier : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    // EF materialises through this.
    private Supplier()
    {
    }

    public Supplier(
        string name,
        string phone,
        string? contactPerson,
        string? email,
        string? address,
        string? company)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        Phone = phone.Trim();
        ContactPerson = Blank(contactPerson);
        Email = Blank(email);
        Address = Blank(address);
        Company = Blank(company);
        IsActive = true;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Required. What the pharmacy calls them.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Required, and the only other mandatory field.
    ///
    /// <para>A supplier with no phone number is of no use to somebody chasing a delivery, and it
    /// is the field a pharmacy will search by when they cannot remember the trading name. Email
    /// and address are genuinely optional — plenty of local distributors have neither.</para>
    /// </summary>
    public string Phone { get; private set; } = null!;

    public string? ContactPerson { get; private set; }

    public string? Email { get; private set; }

    public string? Address { get; private set; }

    /// <summary>
    /// The trading entity behind the name, where they differ — "Popular Pharmaceuticals Ltd"
    /// against a rep the pharmacy knows as "Popular". Free text, deliberately: it goes on nothing
    /// the system generates.
    /// </summary>
    public string? Company { get; private set; }

    /// <summary>
    /// Whether this supplier is still bought from. See the class remarks: never deleted.
    /// </summary>
    public bool IsActive { get; private set; }

    public void Update(
        string name,
        string phone,
        string? contactPerson,
        string? email,
        string? address,
        string? company)
    {
        Name = name.Trim();
        Phone = phone.Trim();
        ContactPerson = Blank(contactPerson);
        Email = Blank(email);
        Address = Blank(address);
        Company = Blank(company);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
