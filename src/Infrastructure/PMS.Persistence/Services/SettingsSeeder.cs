using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.Settings;
using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;

namespace PMS.Persistence.Services;

/// <summary>
/// Gives a newly created pharmacy its settings. See <see cref="ISettingsSeeder"/>.
///
/// <para><b>Raw SQL, and that is the whole reason this class exists.</b> Inserting
/// <c>AppSetting</c> entities through EF would put them in front of
/// <c>TenantEntityInterceptor</c>, which stamps <em>the current</em> tenant on every inserted
/// <c>ITenantEntity</c> and throws when none is resolved. A platform operator creating a pharmacy
/// has no resolved tenant, so that path cannot work here — and weakening the interceptor to make
/// it work would weaken the guarantee that an Admin can only ever write their own pharmacy's
/// rows.</para>
///
/// <para>The query filter is the same story: it would hide the rows being checked for, so the
/// idempotence check would find nothing and insert duplicates until the unique index refused
/// one.</para>
///
/// <para>Every value is parameterised. A pharmacy name arriving from a form and concatenated into
/// SQL is the oldest mistake there is.</para>
/// </summary>
public sealed class SettingsSeeder : ISettingsSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SettingsSeeder> _logger;

    public SettingsSeeder(ApplicationDbContext context, ILogger<SettingsSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> SeedAsync(
        Guid tenantId, string pharmacyName, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "A pharmacy must be named to seed its settings.", nameof(tenantId));
        }

        var created = 0;

        foreach (var definition in SettingKeys.All)
        {
            var value = definition.Key == SettingKeys.PharmacyName
                       && !string.IsNullOrWhiteSpace(pharmacyName)
                ? pharmacyName.Trim()
                : definition.Default;

            // NOT EXISTS rather than a blind insert, so re-running never overwrites a value an
            // Admin has since changed - and so this doubles as a repair for a pharmacy whose
            // original seed was incomplete.
            created += await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO [dbo].[AppSettings]
                     ([Id], [TenantId], [Key], [Value], [CreatedOnUtc])
                 SELECT NEWID(), {tenantId}, {definition.Key}, {value}, SYSUTCDATETIME()
                 WHERE NOT EXISTS (
                     SELECT 1 FROM [dbo].[AppSettings]
                     WHERE [TenantId] = {tenantId} AND [Key] = {definition.Key});
                 """,
                cancellationToken);
        }

        if (created > 0)
        {
            _logger.LogInformation(
                "Seeded {Count} settings for tenant {TenantId}", created, tenantId);
        }

        return created;
    }
}
