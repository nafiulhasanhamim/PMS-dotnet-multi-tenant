using PMS.Application.Common.Tenancy;
using PMS.Application.Features.Tenants.Commands.CreateTenant;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Application.Tenants;

/// <summary>
/// A pharmacy is named one of two ways, and both are legitimate:
///
/// <list type="bullet">
/// <item>a short prefix under the platform's own address — <c>popular-pharmacy</c>, reached at
/// <c>popular-pharmacy.pms.example.com</c>, which works the moment the row is created;</item>
/// <item>an address the pharmacy owns outright — <c>citycare.com</c>, which needs that
/// domain's DNS pointed at the platform before anyone can reach it.</item>
/// </list>
///
/// So the validator accepts either shape. The one thing it must refuse is a value that sits
/// *under* the base domain: exact matching wins during resolution, so such a row would shadow
/// the pharmacy that legitimately owns that prefix.
/// </summary>
public class CreateTenantCommandValidatorTests
{
    private static CreateTenantCommandValidator ValidatorFor(string? baseDomain = "pms.example.com") =>
        new(new TenancySettings { BaseDomain = baseDomain });

    private static CreateTenantCommand Command(string name, string domain) =>
        new(name, domain, null);

    [Theory]
    [InlineData("popular-pharmacy")]        // prefix form
    [InlineData("Popular-Pharmacy")]        // normalised to lowercase
    [InlineData("greenlife")]
    [InlineData("shefa-pharmacy-2")]
    [InlineData("citycare.com")]            // owns its address
    [InlineData("city-care.co.uk")]
    [InlineData("https://citycare.com")]            // scheme stripped
    [InlineData("https://citycare.com/portal")]     // path stripped
    [InlineData("http://citycare.com:8080/x?y=1")]  // port and query stripped
    public void AcceptsEitherAPrefixOrAFullAddress(string domain)
    {
        var result = ValidatorFor().Validate(Command("City Care", domain));

        result.IsValid.Should().BeTrue("'{0}' normalises to a usable host", domain);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a domain!")]
    [InlineData("city care.com")]
    [InlineData("under_score.com")]
    [InlineData("-leading-hyphen")]
    [InlineData("trailing-hyphen-")]
    [InlineData("double..dot.com")]
    [InlineData("https://")]
    public void RejectsWhatIsNotAHost(string domain)
    {
        var result = ValidatorFor().Validate(Command("City Care", domain));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTenantCommand.DomainName));
    }

    [Theory]
    [InlineData("popular-pharmacy.pms.example.com")]
    [InlineData("PMS.EXAMPLE.COM")]
    [InlineData("https://anything.pms.example.com/")]
    public void RefusesADomainUnderThePlatformsOwnAddress(string domain)
    {
        // Exact matching wins over prefix matching, so this row would be found instead of the
        // pharmacy that owns the prefix — two rows, one address, the wrong one returned.
        var result = ValidatorFor().Validate(Command("Shadow", domain));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateTenantCommand.DomainName)
            && e.ErrorMessage.Contains("platform"));
    }

    [Fact]
    public void WithNoBaseDomainConfigured_FullAddressesAreStillFine()
    {
        // Prefix resolution is then simply switched off; nothing can shadow anything.
        var result = ValidatorFor(baseDomain: null).Validate(Command("City Care", "citycare.com"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RejectsALabelLongerThanDnsAllows()
    {
        var result = ValidatorFor().Validate(Command("Long", new string('a', 64)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AcceptsALabelAtExactlyTheDnsLimit()
    {
        var result = ValidatorFor().Validate(Command("Long", new string('a', 63)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RequiresAName()
    {
        var result = ValidatorFor().Validate(Command("", "popular-pharmacy"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTenantCommand.Name));
    }
}
