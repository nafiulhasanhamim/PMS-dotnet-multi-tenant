namespace PMS.Application.Interfaces;

/// <summary>
/// Hashes and verifies passwords. Applied identically whether the account ends up being a
/// platform operator, a pharmacy Admin or counter staff — there is one identity table and
/// one hashing rule.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
