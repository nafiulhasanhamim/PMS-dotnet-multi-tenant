using FluentAssertions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;
using PMS.Persistence.Interceptors;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests.Tenancy;

/// <summary>
/// The mirror of <see cref="CatalogIsPlatformLevelTests"/>.
///
/// The catalogue must be shared; a pharmacy's own product list must not be. Both failures are
/// silent in opposite directions - a filtered catalogue shows nobody anything, and an
/// unfiltered Product table shows every pharmacy everyone else's stock and prices. The second
/// is the one that would be a data breach, so it gets its own test rather than relying on the
/// convention pass being right.
/// </summary>
public class ProductIsTenantScopedTests
{
    /// <summary>
    /// A context wired the way the application wires one: the query filter comes from the
    /// convention pass in ApplicationDbContext, and TenantEntityInterceptor does the stamping
    /// on insert. Both halves are needed — without the interceptor a row is written with an
    /// empty TenantId and then invisible to everybody, which would make these tests pass for
    /// the wrong reason.
    /// </summary>
    private static ApplicationDbContext NewContext(
        ICurrentTenantService? tenant = null, string? database = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(database ?? $"products_{Guid.NewGuid():N}");

        if (tenant is not null)
        {
            builder.AddInterceptors(new TenantEntityInterceptor(tenant));
        }

        return tenant is null
            ? new ApplicationDbContext(builder.Options)
            : new ApplicationDbContext(builder.Options, tenant);
    }

    private static Product NewProduct(string brand) =>
        new(ProductType.Medicine, brand, "piece", 1.20m);

    [Fact]
    public void Product_ImplementsITenantEntity()
        => typeof(ITenantEntity).IsAssignableFrom(typeof(Product))
            .Should().BeTrue("this is what makes the filter and the stamping automatic");

    [Fact]
    public void Product_HasAGlobalQueryFilter()
    {
        using var context = NewContext();

        context.Model.FindEntityType(typeof(Product))!.GetQueryFilter()
            .Should().NotBeNull(
                "without it every pharmacy would read every other pharmacy's catalogue");
    }

    [Fact]
    public async Task OnePharmacysProducts_AreInvisibleToAnother()
    {
        var database = $"products_isolation_{Guid.NewGuid():N}";
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var a = NewContext(new StubTenantContext { TenantId = tenantA }, database))
        {
            // TenantId is not set here on purpose: the interceptor stamps it. A handler that
            // set it itself could write into another pharmacy's data.
            a.Products.Add(NewProduct("Napa 500"));
            await a.SaveChangesAsync();
        }

        await using (var b = NewContext(new StubTenantContext { TenantId = tenantB }, database))
        {
            b.Products.Add(NewProduct("Seclo 20"));
            await b.SaveChangesAsync();
        }

        await using var readA = NewContext(new StubTenantContext { TenantId = tenantA }, database);
        await using var readB = NewContext(new StubTenantContext { TenantId = tenantB }, database);

        (await readA.Products.Select(p => p.BrandName).ToListAsync())
            .Should().Equal(["Napa 500"]);

        (await readB.Products.Select(p => p.BrandName).ToListAsync())
            .Should().Equal(["Seclo 20"]);
    }

    [Fact]
    public async Task WithNoTenantContext_NoProductsAreVisibleAtAll()
    {
        // Fail closed. An unresolved tenant is Guid.Empty, which matches nothing - the
        // opposite of the catalogue, where the same situation must return everything.
        var database = $"products_notenant_{Guid.NewGuid():N}";

        await using (var seeded = NewContext(new StubTenantContext { TenantId = Guid.NewGuid() }, database))
        {
            seeded.Products.Add(NewProduct("Napa 500"));
            await seeded.SaveChangesAsync();
        }

        await using var context = NewContext(database: database);

        (await context.Products.ToListAsync()).Should().BeEmpty();
    }
}
