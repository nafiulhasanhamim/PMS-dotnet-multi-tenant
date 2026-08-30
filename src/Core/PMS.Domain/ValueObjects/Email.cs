using System.Text.RegularExpressions;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Results;

namespace PMS.Domain.ValueObjects;

/// <summary>
/// Represents a validated email address.
/// </summary>
public sealed partial class Email : ValueObject
{
    /// <summary>
    /// Maximum allowed length for an email address.
    /// </summary>
    public const int MaxLength = 256;

    /// <summary>
    /// Gets the email address value.
    /// </summary>
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new Email value object.
    /// </summary>
    /// <param name="email">The email address string.</param>
    /// <returns>A Result containing the Email or an error.</returns>
    public static Result<Email> Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<Email>(Error.Validation("Email", "Email is required."));
        }

        email = email.Trim().ToLowerInvariant();

        if (email.Length > MaxLength)
        {
            return Result.Failure<Email>(Error.Validation("Email", $"Email must not exceed {MaxLength} characters."));
        }

        if (!EmailRegex().IsMatch(email))
        {
            return Result.Failure<Email>(Error.Validation("Email", "Email format is invalid."));
        }

        return new Email(email);
    }

    /// <summary>
    /// Creates an Email without validation (for EF Core materialization).
    /// </summary>
    internal static Email CreateUnsafe(string email) => new(email);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();
}
