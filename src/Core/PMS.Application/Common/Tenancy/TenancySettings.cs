namespace PMS.Application.Common.Tenancy;

/// <summary>
/// The platform's own address, from the <c>Tenancy</c> configuration section.
///
/// A plain object registered as a singleton rather than IOptions, so the Application layer
/// needs no configuration package to read it.
/// </summary>
public sealed class TenancySettings
{
    public const string SectionName = "Tenancy";

    /// <summary>
    /// The address the platform owns, under which pharmacies may sit as a prefix - e.g.
    /// <c>pms.example.com</c>, making <c>popular-pharmacy.pms.example.com</c> resolve to the
    /// pharmacy whose domain name is <c>popular-pharmacy</c>.
    ///
    /// Empty disables prefix resolution entirely: every pharmacy must then be named by its
    /// own full address.
    /// </summary>
    public string? BaseDomain { get; set; }
}
