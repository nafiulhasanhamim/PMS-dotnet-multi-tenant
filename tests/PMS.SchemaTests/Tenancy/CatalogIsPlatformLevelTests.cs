using FluentAssertions;
using PMS.Domain.Entities.Catalog;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests.Tenancy;

/// <summary>
/// The medicine reference catalog must stay OUTSIDE the tenant isolation mechanism.
///
/// This is the inverse of every other tenancy test in this folder. Those prove data cannot
/// escape its pharmacy; these prove the catalog is not confined to one — because the failure
/// mode is quiet and total. Make any of these entities tenant-scoped and every pharmacy sees
/// an empty catalog, the importer writes rows nobody can read, and nothing throws.
/// </summary>
public class CatalogIsPlatformLevelTests
{
    private static readonly Type[] CatalogEntities =
    [
        typeof(CatalogManufacturer),
        typeof(CatalogDrugClass),
        typeof(CatalogDosageForm),
        typeof(CatalogGeneric),
        typeof(CatalogMedicine),
    ];

    private static ApplicationDbContext NewContext(
        ICurrentTenantService? tenant = null, string? database = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(database ?? $"catalog_{Guid.NewGuid():N}")
            .Options;

        return tenant is null
            ? new ApplicationDbContext(options)
            : new ApplicationDbContext(options, tenant);
    }

    [Fact]
    public void NoCatalogEntity_ImplementsITenantEntity()
    {
        foreach (var entity in CatalogEntities)
        {
            typeof(ITenantEntity).IsAssignableFrom(entity)
                .Should().BeFalse(
                    "{0} is shared platform data; implementing ITenantEntity would confine it "
                    + "to one pharmacy and hide it from everyone else", entity.Name);
        }
    }

    [Fact]
    public void NoCatalogEntity_HasATenantIdProperty()
    {
        foreach (var entity in CatalogEntities)
        {
            entity.GetProperty("TenantId")
                .Should().BeNull("{0} must not carry a tenant", entity.Name);
        }
    }

    [Fact]
    public void NoCatalogTable_HasATenantIdColumn()
    {
        using var context = NewContext();

        foreach (var entity in CatalogEntities)
        {
            var properties = context.Model.FindEntityType(entity)!
                .GetProperties()
                .Select(p => p.Name);

            properties.Should().NotContain("TenantId", "{0} maps no tenant column", entity.Name);
        }
    }

    [Fact]
    public void NoCatalogEntity_HasAGlobalQueryFilter()
    {
        using var context = NewContext();

        foreach (var entity in CatalogEntities)
        {
            context.Model.FindEntityType(entity)!.GetQueryFilter()
                .Should().BeNull(
                    "{0} must be readable from every tenant context and from none", entity.Name);
        }
    }

    [Fact]
    public async Task Catalog_IsReadable_WithNoTenantContextAtAll()
    {
        // The importer runs exactly like this: a context built with no ICurrentTenantService.
        // For a tenant-scoped entity this resolves to Guid.Empty and matches nothing, which is
        // the fail-closed behaviour we want there and would be silent breakage here.
        var database = $"catalog_notenant_{Guid.NewGuid():N}";

        await using (var seed = NewContext(database: database))
        {
            seed.CatalogMedicines.Add(new CatalogMedicine
            {
                SourceBrandId = 4077,
                BrandName = "Napa",
                ImportedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
            });

            await seed.SaveChangesAsync();
        }

        await using var context = NewContext(database: database);

        var medicines = await context.CatalogMedicines.ToListAsync();

        medicines.Should().ContainSingle().Which.BrandName.Should().Be("Napa");
    }

    [Fact]
    public async Task Catalog_ReturnsTheSameRows_ForEveryTenant()
    {
        var database = $"catalog_alltenants_{Guid.NewGuid():N}";

        await using (var seed = NewContext(database: database))
        {
            seed.CatalogMedicines.AddRange(
                new CatalogMedicine
                {
                    SourceBrandId = 1, BrandName = "Napa",
                    ImportedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow,
                },
                new CatalogMedicine
                {
                    SourceBrandId = 2, BrandName = "Seclo",
                    ImportedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow,
                });

            await seed.SaveChangesAsync();
        }

        // Two unrelated pharmacies, and no pharmacy, must all see both rows.
        foreach (var tenant in new ICurrentTenantService?[]
                 {
                     null,
                     new StubTenantContext { TenantId = Guid.NewGuid() },
                     new StubTenantContext { TenantId = Guid.NewGuid() },
                     new StubTenantContext { TenantId = Guid.Empty },
                 })
        {
            await using var context = NewContext(tenant, database);

            var brands = await context.CatalogMedicines
                .OrderBy(m => m.BrandName)
                .Select(m => m.BrandName)
                .ToListAsync();

            brands.Should().Equal(["Napa", "Seclo"],
                "the catalog is shared, so tenant context must make no difference");
        }
    }
}
