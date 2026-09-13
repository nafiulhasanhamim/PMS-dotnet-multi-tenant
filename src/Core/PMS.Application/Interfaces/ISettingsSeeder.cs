namespace PMS.Application.Interfaces;

/// <summary>
/// Gives a newly created pharmacy its settings.
///
/// <para><b>Separate from <c>ISettingsService</c> because it writes for a pharmacy that is not
/// the caller's.</b> Every ordinary settings write goes through the tenant query filter and the
/// tenant interceptor, which together guarantee an Admin can only ever change their own
/// pharmacy's rules. A platform operator creating a tenant has no resolved tenant at all — the
/// interceptor would throw rather than guess — so seeding takes a deliberate, narrow path that
/// names the tenant explicitly. Keeping it behind its own interface means that exception is one
/// obvious method rather than a flag on the service everything else uses.</para>
///
/// <para><b>Idempotent.</b> It inserts only keys the pharmacy does not already have, so running
/// it twice cannot overwrite a value an Admin has since changed. That is also what lets it be
/// used as a repair: a pharmacy created before Module 10, or one whose seed failed halfway, gets
/// whatever it is missing and keeps everything it has.</para>
/// </summary>
public interface ISettingsSeeder
{
    /// <summary>
    /// Writes every missing setting for one pharmacy, using the defaults from <c>SettingKeys</c>.
    /// </summary>
    /// <param name="tenantId">
    /// The pharmacy to seed. Explicit, because there is no resolved tenant on this path.
    /// </param>
    /// <param name="pharmacyName">
    /// What to use for <c>pharmacy_name</c>. The tenant's own name, because "My Pharmacy" on a
    /// shop that has just been given a real one would be a worse default than no default.
    /// </param>
    /// <returns>How many settings were created.</returns>
    Task<int> SeedAsync(
        Guid tenantId, string pharmacyName, CancellationToken cancellationToken = default);
}
