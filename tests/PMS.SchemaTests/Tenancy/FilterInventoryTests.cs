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
