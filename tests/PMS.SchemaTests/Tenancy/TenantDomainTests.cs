using FluentAssertions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests.Tenancy;

/// <summary>
/// The domain a login is addressed to.
///
/// Normalisation carries real weight: the uniqueness guarantee is only as good as the
/// agreement on what counts as the same domain. If "https://CityCare.com/" and "citycare.com"
/// stored differently, two pharmacies could hold the same host and a login could not be
/// resolved to one of them.
/// </summary>
public class TenantDomainTests
{
    [Fact]
    public void Domain_IsRequired()
    {
        // Unlike the earlier design, a pharmacy cannot exist without one: the domain is how
        // a login finds it.
        var act = () => new Tenant("City Care Pharmacy", "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("popular-pharmacy", "popular-pharmacy")]
    [InlineData("citycare.com", "citycare.com")]
    [InlineData("  CityCare.com  ", "citycare.com")]          // trimmed and lowercased
    [InlineData("https://citycare.com", "citycare.com")]      // scheme stripped
    [InlineData("http://citycare.com/login", "citycare.com")] // path stripped
    [InlineData("citycare.com:8443", "citycare.com")]         // port stripped
    [InlineData("citycare.com/?next=/stock", "citycare.com")] // query stripped
    [InlineData("citycare.pms.app", "citycare.pms.app")]      // subdomain kept
    public void Domain_IsReducedToABareHost(string typed, string expected)
    {
        new Tenant("City Care", typed).DomainName.Should().Be(expected);
    }

    [Fact]
    public void NewTenant_StartsOnTrial()
    {
        var tenant = new Tenant("City Care", "citycare.com");

        tenant.Status.Should().Be(TenantStatus.Trial);
        tenant.CanBeUsed.Should().BeTrue("a trial pharmacy is usable");
    }

    [Theory]
    [InlineData(TenantStatus.Trial, true)]
    [InlineData(TenantStatus.Active, true)]
    [InlineData(TenantStatus.Suspended, false)]
    public void CanBeUsed_FollowsStatus(TenantStatus status, bool expected)
    {
        var tenant = new Tenant("City Care", "citycare.com");
        tenant.SetStatus(status);

        tenant.CanBeUsed.Should().Be(expected);
    }

    [Fact]
    public void SoftDeletedTenant_CannotBeUsed_EvenWhenActive()
    {
        var tenant = new Tenant("City Care", "citycare.com");
        tenant.SetStatus(TenantStatus.Active);
        tenant.IsDeleted = true;

        tenant.CanBeUsed.Should().BeFalse();
    }

    [Fact]
    public void Domain_CanBeChanged()
    {
        var tenant = new Tenant("City Care", "citycare.com");

        tenant.SetDomainName("CityCare.co.uk");

        tenant.DomainName.Should().Be("citycare.co.uk");
    }

    [Fact]
    public void TheModel_MapsDomainName_WithAUniqueIndex()
    {
        using var context = NewContext();
        var entity = context.Model.FindEntityType(typeof(Tenant))!;

        entity.FindProperty(nameof(Tenant.DomainName))!.GetMaxLength().Should().Be(253);

        var index = entity.GetIndexes()
            .SingleOrDefault(i => i.Properties.Any(p => p.Name == nameof(Tenant.DomainName)));

        index.Should().NotBeNull("a domain must resolve to exactly one pharmacy");
        index!.IsUnique.Should().BeTrue();
    }

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"domains_{Guid.NewGuid():N}").Options);
}
