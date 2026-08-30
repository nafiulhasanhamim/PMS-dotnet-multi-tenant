using System.Text.RegularExpressions;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Results;

namespace PMS.Domain.ValueObjects;

/// <summary>
/// Represents a validated phone number.
/// </summary>
public sealed partial class PhoneNumber : ValueObject
{
    /// <summary>
    /// Gets the phone number value.
    /// </summary>
    public string Value { get; }

    private PhoneNumber(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new PhoneNumber value object.
    /// </summary>
    /// <param name="phoneNumber">The phone number string.</param>
    /// <returns>A Result containing the PhoneNumber or an error.</returns>
    public static Result<PhoneNumber> Create(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return Result.Failure<PhoneNumber>(Error.Validation("PhoneNumber", "Phone number is required."));
        }

        // Remove all non-digit characters for validation
        var digitsOnly = DigitsOnlyRegex().Replace(phoneNumber, "");

        if (digitsOnly.Length < 10 || digitsOnly.Length > 15)
        {
            return Result.Failure<PhoneNumber>(Error.Validation("PhoneNumber", "Phone number must be between 10 and 15 digits."));
        }

        return new PhoneNumber(phoneNumber.Trim());
    }

    /// <summary>
    /// Creates a PhoneNumber without validation (for EF Core materialization).
    /// </summary>
    internal static PhoneNumber CreateUnsafe(string phoneNumber) => new(phoneNumber);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(PhoneNumber phoneNumber) => phoneNumber.Value;

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnlyRegex();
}
