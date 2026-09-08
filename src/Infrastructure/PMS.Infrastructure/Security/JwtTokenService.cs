using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PMS.Application.Common.Security;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PMS.Infrastructure.Security;

/// <inheritdoc />
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly IDateTime _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IDateTime clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    /// <inheritdoc />
    public TokenResult IssueForTenantUser(Guid userId, Guid tenantId, UserRole role) =>
        Issue([
            new Claim(AuthClaims.Subject, userId.ToString()),
            new Claim(AuthClaims.TenantId, tenantId.ToString()),
            // Also as the framework's role claim, so [Authorize(Roles = "Admin")] works.
            new Claim(AuthClaims.Role, role.ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
        ]);

    /// <inheritdoc />
    public TokenResult IssueForPlatformAdmin(Guid userId) =>
        Issue([
            new Claim(AuthClaims.Subject, userId.ToString()),
            new Claim(AuthClaims.PlatformAdmin, "true"),
            new Claim(ClaimTypes.Role, UserRole.PlatformAdmin.ToString()),
        ]);

    private TokenResult Issue(IEnumerable<Claim> claims)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException(
                "No JWT signing key is configured. Set Jwt:SigningKey — tokens must never be " +
                "signed with a built-in default.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var now = _clock.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new TokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            token.ValidTo);
    }
}
