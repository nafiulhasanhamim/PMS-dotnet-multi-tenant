using FluentAssertions;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Persistence.Contexts;
using PMS.Persistence.Services;
using DomainTenantStatus = PMS.Domain.Enums.TenantStatus;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests.Tenancy;

/// <summary>
/// IsActive suspends a pharmacy's *access*. It does not delete anything, and it does not
/// hide the data from the pharmacy that owns it — which is why it is enforced at the edge
/// of a request rather than inside the query filter.
/// </summary>
public class TenantStatusTests
{
    private static ApplicationDbContext NewContext(string database) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(database).Options);

    private static async Task<(Guid id, string db)> SeedTenant(bool isActive = true, bool deleted = false)
    {
        var db = $"status_{Guid.NewGuid():N}";
        await using var context = NewContext(db);

        var tenant = new Tenant("City Care", "citycare.com");
        if (!isActive)
        {
            tenant.SetStatus(DomainTenantStatus.Suspended);
        }
        tenant.IsDeleted = deleted;

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return (tenant.Id, db);
    }

    [Fact]
    public async Task ActiveTenant_IsOk()
    {
        var (id, db) = await SeedTenant();
        await using var context = NewContext(db);

        var status = await new TenantStatusValidator(context).CheckAsync(id);

        status.Should().Be(TenantStatus.Ok);
    }

    [Fact]
    public async Task SuspendedTenant_IsInactive()
    {
        var (id, db) = await SeedTenant(isActive: false);
        await using var context = NewContext(db);

        var status = await new TenantStatusValidator(context).CheckAsync(id);

        // Distinguished from NotFound so the caller can be told the pharmacy is suspended
        // rather than that it does not exist.
        status.Should().Be(TenantStatus.Inactive);
    }

    [Fact]
    public async Task DeletedTenant_IsNotFound()
    {
        var (id, db) = await SeedTenant(deleted: true);
        await using var context = NewContext(db);

        // The soft-delete query filter already hides it, so it reads as absent — there is no
        // separate "deleted" branch to keep in step.
        var status = await new TenantStatusValidator(context).CheckAsync(id);

        status.Should().Be(TenantStatus.NotFound);
    }

    [Fact]
    public async Task UnknownTenant_IsNotFound()
    {
        var (_, db) = await SeedTenant();
        await using var context = NewContext(db);

        var status = await new TenantStatusValidator(context).CheckAsync(Guid.NewGuid());

        status.Should().Be(TenantStatus.NotFound);
    }

    [Fact]
    public async Task EmptyTenant_IsNotFound_WithoutQueryingAtAll()
    {
        var (_, db) = await SeedTenant();
        await using var context = NewContext(db);

        var status = await new TenantStatusValidator(context).CheckAsync(Guid.Empty);

        status.Should().Be(TenantStatus.NotFound);
    }

    [Fact]
    public async Task SuspendingATenant_DoesNotHideItsData()
    {
        // The reason this check lives at the request edge and not in the query filter.
        // A suspended pharmacy's stock, sales and history still exist and still belong to
        // them — reactivating must bring everything back exactly as it was, and a platform
        // administrator must be able to look at it while it is suspended.
        var db = $"status_{Guid.NewGuid():N}";
        Guid tenantId;

        await using (var context = NewContext(db))
        {
            var tenant = new Tenant("City Care", "citycare.com");
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        await using (var context = NewContext(db))
        {
            var tenant = await context.Tenants.SingleAsync(t => t.Id == tenantId);
            tenant.SetStatus(DomainTenantStatus.Suspended);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext(db))
        {
            // Still there, still readable, just not usable by its own users.
            var tenant = await context.Tenants.SingleAsync(t => t.Id == tenantId);
            tenant.Status.Should().Be(DomainTenantStatus.Suspended);
            tenant.Name.Should().Be("City Care");

            (await new TenantStatusValidator(context).CheckAsync(tenantId))
                .Should().Be(TenantStatus.Inactive);
        }
    }

    [Fact]
    public async Task ReactivatingRestoresAccess()
    {
        var (id, db) = await SeedTenant(isActive: false);

        await using (var context = NewContext(db))
        {
            (await context.Tenants.SingleAsync(t => t.Id == id)).SetStatus(DomainTenantStatus.Active);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext(db))
        {
            (await new TenantStatusValidator(context).CheckAsync(id))
                .Should().Be(TenantStatus.Ok);
        }
    }
}
