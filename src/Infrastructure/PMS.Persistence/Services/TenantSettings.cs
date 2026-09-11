using Microsoft.EntityFrameworkCore;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Interfaces;

namespace PMS.Persistence.Services;

/// <summary>
/// The current pharmacy's preferences, read once per request.
///
/// <para><b>The lifetime is the cache, and that is the whole design.</b> This is registered
/// scoped, so one instance serves one request and the field below holds the answer for its
/// duration. A sale asks for the antibiotic mode once per cart item; without this, a ten-item
/// cart would be ten identical queries about a value that cannot change between them.</para>
///
/// <para><b>Nothing is cached across requests, deliberately.</b> A longer-lived cache would be
/// faster and wrong in the way that matters: an Admin tightening the rule before an inspection
/// would be left wondering whether it had taken effect, and the answer would be "in up to five
/// minutes". Reading once per request means the change is in force on the very next one.</para>
///
/// <para><b>Reaching past the tenant filter is correct here.</b> <c>Tenants</c> is the tenant
/// list rather than tenant-owned data, so it is not an <c>ITenantEntity</c> and carries no
/// automatic filter. The id comes from the resolved tenant context, never from a caller.</para>
/// </summary>
public sealed class TenantSettings : ITenantSettings
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _tenant;

    private Profile? _profile;

    public TenantSettings(ApplicationDbContext context, ICurrentTenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    /// <inheritdoc />
    public async Task<AntibioticPrescriptionMode> GetAntibioticModeAsync(
        CancellationToken cancellationToken = default)
        => (await LoadAsync(cancellationToken)).Mode;

    /// <inheritdoc />
    public async Task<string?> GetPharmacyNameAsync(CancellationToken cancellationToken = default)
        => (await LoadAsync(cancellationToken)).Name;

    /// <summary>
    /// The one read. Both public methods come through here, so asking for the name and the mode
    /// in the same request is one query rather than two.
    /// </summary>
    private async Task<Profile> LoadAsync(CancellationToken cancellationToken)
    {
        if (_profile is { } cached)
        {
            return cached;
        }

        var tenantId = _tenant.TenantId;

        if (tenantId == Guid.Empty)
        {
            // No resolved pharmacy. Unreachable through a tenant-scoped request, and Off is the
            // safe answer anyway: defaulting to Required would block a sale because a lookup
            // found nothing, which is a worse failure than the one it would be guarding against.
            _profile = new Profile(AntibioticPrescriptionMode.Off, null);
            return _profile;
        }

        var rows = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new Profile(t.AntibioticPrescriptionMode, t.Name))
            .ToListAsync(cancellationToken);

        _profile = rows.Count > 0 ? rows[0] : new Profile(AntibioticPrescriptionMode.Off, null);

        return _profile;
    }

    private sealed record Profile(AntibioticPrescriptionMode Mode, string? Name);
}
