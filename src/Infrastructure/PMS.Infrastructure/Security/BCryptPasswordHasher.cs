using PMS.Application.Interfaces;

namespace PMS.Infrastructure.Security;

/// <inheritdoc />
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    // BCrypt generates and embeds a per-password salt, so equal passwords never produce
    // equal hashes and a stolen table cannot be attacked with a single rainbow table.
    private const int WorkFactor = 12;

    /// <inheritdoc />
    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    /// <inheritdoc />
    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A malformed hash in the database must read as "wrong password", not blow up
            // the login endpoint.
            return false;
        }
    }
}
