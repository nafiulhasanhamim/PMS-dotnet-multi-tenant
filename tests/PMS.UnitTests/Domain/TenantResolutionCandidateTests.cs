using PMS.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Domain;

/// <summary>
/// The rule that lets one column hold both kinds of pharmacy address without ambiguity.
///
/// A pharmacy is named either by a prefix under the platform's address (<c>popular-pharmacy</c>
/// → <c>popular-pharmacy.pms.example.com</c>) or by an address it owns (<c>citycare.com</c>).
/// Order is what disambiguates: the whole host is tried first, so a pharmacy that owns its
/// address is found by it, and only then is the leading label tried.
/// </summary>
public class TenantResolutionCandidateTests
{
    private const string Base = "pms.example.com";

    [Fact]
    public void APrefixHost_TriesTheWholeHostThenTheLabel()
    {
        var candidates = Tenant.ResolutionCandidates("popular-pharmacy.pms.example.com", Base);

        // Whole host first: a pharmacy that had registered the full address would win, which
        // is why registering one under the base domain is refused at creation.
        candidates.Should().Equal("popular-pharmacy.pms.example.com", "popular-pharmacy");
    }

    [Fact]
    public void AnUnrelatedHost_IsOnlyEverItself()
    {
        var candidates = Tenant.ResolutionCandidates("citycare.com", Base);

        candidates.Should().Equal("citycare.com");
    }

    [Fact]
    public void TheBaseDomainAlone_NamesNoPharmacy()
    {
        // The platform's front door. Resolving it to a pharmacy would mean the marketing site
        // and a pharmacy's login shared an address.
        Tenant.ResolutionCandidates(Base, Base).Should().BeEmpty();
        Tenant.ResolutionCandidates("PMS.Example.COM", Base).Should().BeEmpty();
    }

    [Fact]
    public void ADeeperHost_IsNotTreatedAsAPrefix()
    {
        // "a.b" is not a pharmacy name. Without this, a typo in a subdomain becomes a sign-in
        // attempt against a pharmacy nobody meant to name.
        var candidates = Tenant.ResolutionCandidates("a.b.pms.example.com", Base);

        candidates.Should().Equal("a.b.pms.example.com");
    }

    [Theory]
    [InlineData("https://popular-pharmacy.pms.example.com/login?next=/x")]
    [InlineData("POPULAR-PHARMACY.PMS.EXAMPLE.COM")]
    [InlineData("  popular-pharmacy.pms.example.com.  ")]
    public void NormalisesBeforeMatching(string input)
    {
        var candidates = Tenant.ResolutionCandidates(input, Base);

        candidates.Should().Contain("popular-pharmacy");
    }

    [Fact]
    public void WithNoBaseDomain_OnlyExactAddressesResolve()
    {
        Tenant.ResolutionCandidates("citycare.com", null).Should().Equal("citycare.com");
        Tenant.ResolutionCandidates("popular-pharmacy", "").Should().Equal("popular-pharmacy");
    }

    [Fact]
    public void ABareTypedName_IsUsedAsGiven()
    {
        // What someone types into the login form on the base domain, where the address names
        // no pharmacy. It is a candidate in its own right.
        Tenant.ResolutionCandidates("popular-pharmacy", Base).Should().Equal("popular-pharmacy");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://")]
    public void NothingUsable_YieldsNothing(string? input)
    {
        Tenant.ResolutionCandidates(input, Base).Should().BeEmpty();
    }
}
