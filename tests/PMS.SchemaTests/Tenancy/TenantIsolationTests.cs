using FluentAssertions;
using PMS.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests.Tenancy;

/// <summary>
/// Proves tenant isolation actually holds.
///
/// These are the highest-value tests in the system. Every other bug shows itself; a broken
/// tenant filter shows one pharmacy another pharmacy's stock, prices and patients, and does
/// it silently.
/// </summary>
public class TenantIsolationTests
{
    private static readonly Guid PharmacyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PharmacyB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>One shared in-memory database, so the tenants really do share storage.</summary>
    private static TenantTestContext ContextFor(StubTenantContext tenant, string database)
    {
        var options = new DbContextOptionsBuilder<TenantTestContext>()
            .UseInMemoryDatabase(database)
            .AddInterceptors(new TenantEntityInterceptor(tenant))
            .Options;

        return new TenantTestContext(options, tenant);
    }

    private static string NewDatabase() => $"tenancy_{Guid.NewGuid():N}";

    [Fact]
    public void Query_ReturnsOnlyTheCurrentTenantsRows()
    {
        var db = NewDatabase();

        using (var a = ContextFor(new StubTenantContext { TenantId = PharmacyA }, db))
        {
            a.Things.Add(new TenantThing { Name = "A's paracetamol" });
            a.SaveChanges();
        }

        using (var b = ContextFor(new StubTenantContext { TenantId = PharmacyB }, db))
        {
            b.Things.Add(new TenantThing { Name = "B's paracetamol" });
            b.SaveChanges();

            // B must not see A's row, even though both live in the same table.
            b.Things.Should().ContainSingle().Which.Name.Should().Be("B's paracetamol");
        }

        using (var a = ContextFor(new StubTenantContext { TenantId = PharmacyA }, db))
        {
            a.Things.Should().ContainSingle().Which.Name.Should().Be("A's paracetamol");
        }
    }

    [Fact]
    public void Insert_StampsTheCurrentTenant_WithoutTheCallerSettingIt()
    {
        var db = NewDatabase();
        using var context = ContextFor(new StubTenantContext { TenantId = PharmacyA }, db);

        // Note nothing sets TenantId here — that is the interceptor's job.
        context.Things.Add(new TenantThing { Name = "stamped" });
        context.SaveChanges();

        context.Things.Single().TenantId.Should().Be(PharmacyA);
    }

    [Fact]
    public void NoResolvedTenant_SeesNothing_RatherThanEverything()
    {
        var db = NewDatabase();

        using (var a = ContextFor(new StubTenantContext { TenantId = PharmacyA }, db))
        {
            a.Things.Add(new TenantThing { Name = "A's row" });
            a.SaveChanges();
        }

        // An anonymous or misconfigured request resolves no tenant. The filter compares
        // against Guid.Empty, which matches nothing: the failure mode is an empty screen,
        // never someone else's data.
        using var anonymous = ContextFor(new StubTenantContext(), db);
        anonymous.Things.Should().BeEmpty();
    }

    [Fact]
    public void Writing_WithoutAResolvedTenant_Throws()
    {
        var db = NewDatabase();
        using var context = ContextFor(new StubTenantContext(), db);

        context.Things.Add(new TenantThing { Name = "orphan" });

        // A row belonging to no pharmacy could never be read back by anyone. Better to fail
        // at the write than to leave it stranded.
        var act = () => context.SaveChanges();
        act.Should().Throw<InvalidOperationException>().WithMessage("*no tenant is resolved*");
    }

    [Fact]
    public void MovingARow_BetweenTenants_Throws()
    {
        var db = NewDatabase();
        var tenant = new StubTenantContext { TenantId = PharmacyA };

        using var context = ContextFor(tenant, db);
        context.Things.Add(new TenantThing { Name = "A's row" });
        context.SaveChanges();

        var thing = context.Things.Single();
        thing.ForceTenant(PharmacyB);

        var act = () => context.SaveChanges();
        act.Should().Throw<InvalidOperationException>().WithMessage("*between tenants*");
    }

    [Fact]
    public void SoftDeleteAndTenantFilters_BothApply()
    {
        // The trap this guards: HasQueryFilter replaces rather than combines, so applying
        // the two filters in separate passes would silently drop one of them. Losing the
        // soft-delete half resurrects deleted rows; losing the tenant half leaks data.
        var db = NewDatabase();
        using var context = ContextFor(new StubTenantContext { TenantId = PharmacyA }, db);

        context.Things.Add(new TenantThing { Name = "live" });
        context.Things.Add(new TenantThing { Name = "deleted", IsDeleted = true });
        context.SaveChanges();

        context.Things.Should().ContainSingle().Which.Name.Should().Be("live");

        // and the tenant half is still in force alongside it
        context.Things.IgnoreQueryFilters().Should().HaveCount(2);
    }

    [Fact]
    public void PlatformAdmin_CrossesTheBoundary_OnlyByAskingExplicitly()
    {
        var db = NewDatabase();

        foreach (var tenantId in new[] { PharmacyA, PharmacyB })
        {
            using var seed = ContextFor(new StubTenantContext { TenantId = tenantId }, db);
            seed.Things.Add(new TenantThing { Name = $"row for {tenantId}" });
            seed.SaveChanges();
        }

        var admin = new StubTenantContext { IsPlatformAdmin = true };
        using var context = ContextFor(admin, db);

        // Being a platform admin does not widen the filter by itself...
        context.Things.Should().BeEmpty();

        // ...crossing tenants is always a visible, searchable act at the call site.
        context.Things.IgnoreQueryFilters().Should().HaveCount(2);
    }

    [Fact]
    public void TheFilterIsReEvaluatedPerContext_NotBakedIntoTheCachedModel()
    {
        // The subtlest trap. EF Core builds the model once per context type and caches it.
        // Had the tenant been captured while building that model, every later request would
        // be filtered to whichever pharmacy happened to be first. Reading it from a context
        // member makes EF treat it as a query parameter instead.
        var db = NewDatabase();

        using (var a = ContextFor(new StubTenantContext { TenantId = PharmacyA }, db))
        {
            a.Things.Add(new TenantThing { Name = "A" });
            a.SaveChanges();
            _ = a.Things.ToList();          // forces the model to be built and cached
        }

        using (var b = ContextFor(new StubTenantContext { TenantId = PharmacyB }, db))
        {
            b.Things.Add(new TenantThing { Name = "B" });
            b.SaveChanges();
            b.Things.Should().ContainSingle().Which.Name.Should().Be("B");
        }

        using (var a = ContextFor(new StubTenantContext { TenantId = PharmacyA }, db))
        {
            a.Things.Should().ContainSingle().Which.Name.Should().Be("A");
        }
    }
}
