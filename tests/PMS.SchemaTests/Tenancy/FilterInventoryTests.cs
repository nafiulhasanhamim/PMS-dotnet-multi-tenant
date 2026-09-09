using FluentAssertions;
using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests.Tenancy;

/// <summary>
/// An inventory of which entities carry a global query filter.
///
/// Worth pinning: IgnoreQueryFilters() is all-or-nothing, so knowing exactly what a bypass
/// switches off is the difference between a deliberate exception and an accidental one.
/// </summary>
public class FilterInventoryTests
{
    [Theory]
    // entity                          has a global query filter?
    [InlineData(typeof(PMS.Domain.Entities.Tenant), true)]                 // soft delete
    [InlineData(typeof(PMS.Domain.Entities.UserTenantMembership), true)]   // tenant (hand-written)
    [InlineData(typeof(PMS.Domain.Entities.User), false)]                  // global identity
    [InlineData(typeof(PMS.Domain.Entities.AccessLog), false)]             // audit trail
    // Module 2: each pharmacy's own catalogue. Tenant-scoped by convention, from
    // ITenantEntity alone - no filter is written by hand for it anywhere.
    [InlineData(typeof(PMS.Domain.Entities.Product), true)]
    // Module 3: physical stock and its audit trail, both tenant-scoped by convention. These
    // matter more than most: the stock list aggregates across batches, so an unfiltered Batch
    // would fold another pharmacy's quantities into this one's totals rather than merely
    // showing an extra row somebody might notice.
    [InlineData(typeof(PMS.Domain.Entities.Batch), true)]
    [InlineData(typeof(PMS.Domain.Entities.StockAdjustment), true)]
    // The medicine reference catalog: shared platform data, deliberately unfiltered. A filter
    // here would show every pharmacy an empty catalog without failing anything else.
    [InlineData(typeof(PMS.Domain.Entities.Catalog.CatalogManufacturer), false)]
    [InlineData(typeof(PMS.Domain.Entities.Catalog.CatalogDrugClass), false)]
    [InlineData(typeof(PMS.Domain.Entities.Catalog.CatalogDosageForm), false)]
    [InlineData(typeof(PMS.Domain.Entities.Catalog.CatalogGeneric), false)]
    [InlineData(typeof(PMS.Domain.Entities.Catalog.CatalogMedicine), false)]
    public void FilterInventory_IsWhatWeThinkItIs(Type entity, bool expected)
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"filters_{Guid.NewGuid():N}").Options);

        var hasFilter = context.Model.FindEntityType(entity)!.GetQueryFilter() is not null;

        hasFilter.Should().Be(expected);
    }
}
