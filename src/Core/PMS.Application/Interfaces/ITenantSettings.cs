using PMS.Domain.Enums;

namespace PMS.Application.Interfaces;

/// <summary>
/// The current pharmacy's own preferences.
///
/// <para>One setting today. It exists as an interface rather than a direct read of the tenant row
/// because the read has to be cached and because a Settings module will replace where the value
/// comes from without changing a single caller.</para>
///
/// <para><b>Scoped, and the lifetime is the cache.</b> A sale asks for the mode once per cart
/// item, several times per request; a query per ask would be several round trips to answer the
/// same question about a value that cannot change mid-request. The implementation reads once and
/// remembers for the life of the request — so a change saved on the settings page is in force on
/// the very next request, which is the other half of the requirement. Nothing is cached across
/// requests, deliberately: an admin who tightens the rule before an inspection should not be
/// wondering whether it has taken yet.</para>
/// </summary>
public interface ITenantSettings
{
    /// <summary>
    /// How strictly this pharmacy captures antibiotic prescriptions.
    ///
    /// <para>Returns <see cref="AntibioticPrescriptionMode.Off"/> when there is no resolved
    /// tenant. That is unreachable through a tenant-scoped request and is the safe answer
    /// anyway: the alternative, defaulting to Required, would block a sale because a lookup
    /// failed.</para>
    /// </summary>
    Task<AntibioticPrescriptionMode> GetAntibioticModeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// This pharmacy's display name, for documents that have to say whose record they are.
    ///
    /// <para>Here rather than on <c>ICurrentTenantService</c>, which carries only the id on
    /// purpose: that interface is about isolation, and adding a name to it would invite reading
    /// tenant data from something whose job is to decide which rows exist. This service already
    /// reads the row, so the name costs nothing extra.</para>
    /// </summary>
    Task<string?> GetPharmacyNameAsync(CancellationToken cancellationToken = default);
}
