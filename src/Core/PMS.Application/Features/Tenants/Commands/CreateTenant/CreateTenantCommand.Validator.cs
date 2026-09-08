using System.Text.RegularExpressions;
using PMS.Application.Common.Tenancy;
using PMS.Domain.Entities;
using FluentValidation;

namespace PMS.Application.Features.Tenants.Commands.CreateTenant;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    // A pharmacy is named EITHER by a prefix under the platform's address
    // ("popular-pharmacy" -> popular-pharmacy.pms.example.com) OR by an address it owns
    // outright ("citycare.com"). Both are allowed, so this accepts one label or a full host:
    // labels of letters, digits and inner hyphens, joined by dots.
    //
    // 253 is the longest a fully-qualified domain name can be (RFC 1035); 63 is the limit for
    // any single label within it.
    private static readonly Regex HostPattern = new(
        @"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly TenancySettings _tenancy;

    public CreateTenantCommandValidator(TenancySettings tenancy)
    {
        _tenancy = tenancy;

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.DomainName)
            .NotEmpty()
            // Judged after normalisation, so a pasted "https://citycare.com/portal" is
            // accepted and stored as the host it reduces to.
            .Must(BeAValidHost)
            .WithMessage("A domain must be either a short name such as 'popular-pharmacy' or "
                + "a full address such as 'citycare.com' - letters, numbers, hyphens and dots.")
            // Checked separately so the message can explain the actual problem rather than
            // lumping it in with malformed input.
            .Must(NotShadowTheBaseDomain)
            .WithMessage(_ => "A domain cannot sit under the platform's own address "
                + $"('{Tenant.NormalizeDomain(_tenancy.BaseDomain)}'). Register the short name "
                + "on its own and the full address is built from it.");

        RuleFor(x => x.SubscriptionPlan).MaximumLength(100);
    }

    private static bool BeAValidHost(string? domainName)
    {
        var host = Tenant.NormalizeDomain(domainName);

        return host is not null && host.Length <= 253 && HostPattern.IsMatch(host);
    }

    /// <summary>
    /// Refuses "popular-pharmacy.pms.example.com" when the base domain is "pms.example.com".
    ///
    /// Such a row would be reachable by exact match, which wins over prefix matching, so it
    /// would silently shadow the pharmacy that legitimately owns that prefix - two rows, one
    /// address, and the wrong one found. Storing the bare prefix is the supported way to get
    /// that address.
    /// </summary>
    private bool NotShadowTheBaseDomain(string? domainName)
    {
        var host = Tenant.NormalizeDomain(domainName);
        var baseDomain = Tenant.NormalizeDomain(_tenancy.BaseDomain);

        if (host is null || baseDomain is null)
        {
            return true;
        }

        return host != baseDomain
               && !host.EndsWith("." + baseDomain, StringComparison.Ordinal);
    }
}
