using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One configurable value belonging to one pharmacy.
///
/// <para><b>Key-value rather than a row of typed columns, and that is a deliberate trade.</b>
/// Adding a setting later becomes a seed row rather than a migration, an entity change, a
/// configuration change and a DTO change. The cost is that the database cannot type-check
/// anything: <see cref="Value"/> is text, and "90 days" and "ninety days" are equally valid as far
/// as SQL Server is concerned.</para>
///
/// <para>That cost is paid in exactly one place — <c>ISettingsService</c> owns every cast and
/// every fallback, and the settings command validates before writing. No call site ever parses a
/// string. A second place that parsed one would be the beginning of two pharmacies behaving
/// differently for reasons nobody could find.</para>
///
/// <para><b>There is no delete.</b> A key that exists is a key something reads; removing the row
/// would send that reader to its fallback silently. Settings are seeded once per tenant and
/// updated thereafter.</para>
/// </summary>
public sealed class AppSetting : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    // EF materialises through this.
    private AppSetting()
    {
    }

    public AppSetting(string key, string value, Guid? updatedByUserId = null)
    {
        Id = Guid.NewGuid();
        Key = key;
        Value = value ?? string.Empty;
        UpdatedByUserId = updatedByUserId;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The setting's name, from <c>SettingKeys</c>. Unique per pharmacy.
    ///
    /// <para>Snake case rather than the PascalCase the rest of the system uses, because these
    /// strings appear in the database, in seed scripts and in the API's JSON alike — and a
    /// convention that survives all three unchanged is worth more than matching C# here.</para>
    /// </summary>
    public string Key { get; private set; } = null!;

    /// <summary>
    /// The value, as text. Cast on read by <c>ISettingsService</c> and nowhere else.
    ///
    /// <para>Empty rather than null when a setting is blank — a drug licence number the pharmacy
    /// does not have is "no value", not "unknown", and making the column non-nullable removes the
    /// question of which one an empty string means.</para>
    /// </summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>
    /// Who last changed it, or null for a seeded value nobody has touched.
    ///
    /// <para>Not a foreign key to <c>Users</c>, and deliberately. A setting outlives the person
    /// who set it; a Restrict constraint would block removing a user who once saved the settings
    /// page, and Cascade would take the setting with them. The id is here to answer "who changed
    /// the discount cap", and an id that no longer resolves to a name still answers it better
    /// than a blank.</para>
    /// </summary>
    public Guid? UpdatedByUserId { get; private set; }

    /// <summary>
    /// Changes the value. <see cref="Key"/> is immutable — a renamed key is a new setting, and
    /// renaming one in place would strand every caller still asking for the old name.
    /// </summary>
    public void Update(string value, Guid? updatedByUserId)
    {
        Value = value ?? string.Empty;
        UpdatedByUserId = updatedByUserId;
    }
}
