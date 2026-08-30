using FluentAssertions;
using PMS.Domain.Entities;
using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests.Tenancy;

/// <summary>
/// The pharmacy's own domain.
///
/// Normalisation carries real weight here: the uniqueness guarantee is only as good as the
/// agreement on what counts as the same domain. If "https://CityCare.com/" and "citycare.com"
/// stored differently, two tenants could hold the same host and the index would not object.
/// </summary>
public class TenantDomainTests
{
    [Fact]
    public void Domain_IsOptional()
    {
        new Tenant("City Care Pharmacy", "citycare").DomainName.Should().BeNull();
    }

    [Theory]
    [InlineData("citycare.com", "citycare.com")]
    [InlineData("  CityCare.com  ", "citycare.com")]          // trimmed and lowercased
    [InlineData("https://citycare.com", "citycare.com")]      // scheme stripped
    [InlineData("http://citycare.com/login", "citycare.com")] // path stripped
    [InlineData("citycare.com:8443", "citycare.com")]         // port stripped
    [InlineData("citycare.com/?next=/stock", "citycare.com")] // query stripped
    [InlineData("citycare.pms.app", "citycare.pms.app")]      // subdomain kept
    public void Domain_IsReducedToABareHost(string typed, string expected)
    {
        new Tenant("City Care", "citycare", typed).DomainName.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankDomain_BecomesNull_NotAnEmptyString(string? typed)
    {
        // An empty string would collide with another empty string under the unique index,
        // so "no domain" has to be NULL.
        new Tenant("City Care", "citycare", typed).DomainName.Should().BeNull();
    }

    [Fact]
    public void Domain_CanBeSetAndCleared()
    {
        var tenant = new Tenant("City Care", "citycare");

        tenant.SetDomainName("CityCare.com");
        tenant.DomainName.Should().Be("citycare.com");

        tenant.SetDomainName(null);
        tenant.DomainName.Should().BeNull();
    }

    [Fact]
    public void SeveralTenants_MayHaveNoDomain()
    {
        // The reason the unique index is filtered on IS NOT NULL: SQL Server treats NULLs as
        // equal in a unique index, so an unfiltered one would allow exactly one tenant
        // without a domain.
        using var context = NewContext();

        context.Tenants.Add(new Tenant("City Care", "citycare"));
        context.Tenants.Add(new Tenant("Green Life", "greenlife"));
        context.Tenants.Add(new Tenant("Wellness", "wellness"));
        context.SaveChanges();

        context.Tenants.Count(t => t.DomainName == null).Should().Be(3);
    }

    [Fact]
    public void TheModel_MapsDomainName_WithAUniqueFilteredIndex()
    {
        using var context = NewContext();
        var entity = context.Model.FindEntityType(typeof(Tenant))!;

        entity.FindProperty(nameof(Tenant.DomainName))!.GetMaxLength().Should().Be(253);

        var index = entity.GetIndexes()
            .SingleOrDefault(i => i.Properties.Any(p => p.Name == nameof(Tenant.DomainName)));

        index.Should().NotBeNull("a domain must resolve to exactly one tenant");
        index!.IsUnique.Should().BeTrue();
    }

    private static ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"domains_{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
