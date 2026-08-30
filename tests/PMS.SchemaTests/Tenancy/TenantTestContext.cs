using PMS.Persistence.Contexts;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PMS.SchemaTests.Tenancy;

/// <summary>
/// A stand-in tenant-owned entity.
///
/// PMS has no domain entities yet, but the isolation rules must be proven now rather than
/// discovered later against real stock. This is deliberately ordinary — it implements the
/// same two interfaces a Medicine or a Sale will — so it exercises exactly the convention
/// those entities will rely on.
/// </summary>
public class TenantThing : BaseEntity<Guid>, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; private set; }
    public string Name { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public string? DeletedBy { get; set; }

    public TenantThing() => Id = Guid.NewGuid();

    /// <summary>Test-only: forces a tenant, to prove the interceptor refuses reassignment.</summary>
    public void ForceTenant(Guid tenantId) => TenantId = tenantId;
}

/// <summary>Settable tenant context, so a test can act as a given pharmacy.</summary>
public sealed class StubTenantContext : ITenantContext
{
    public Guid TenantId { get; set; } = Guid.Empty;
    public bool HasTenant => TenantId != Guid.Empty;
    public bool IsPlatformAdmin { get; set; }
}

/// <summary>
/// ApplicationDbContext plus the stand-in entity. The filters under test come entirely
/// from the base class — nothing about them is redefined here.
/// </summary>
public sealed class TenantTestContext : ApplicationDbContext
{
    public TenantTestContext(DbContextOptions<TenantTestContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<TenantThing> Things => Set<TenantThing>();
}
