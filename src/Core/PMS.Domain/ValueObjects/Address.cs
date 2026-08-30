using PMS.SharedKernel.Common;
using PMS.SharedKernel.Results;

namespace PMS.Domain.ValueObjects;

/// <summary>
/// Represents a physical address.
/// </summary>
public sealed class Address : ValueObject
{
    /// <summary>
    /// Gets the street address (line 1).
    /// </summary>
    public string Street { get; }

    /// <summary>
    /// Gets the city.
    /// </summary>
    public string City { get; }

    /// <summary>
    /// Gets the state or province.
    /// </summary>
    public string State { get; }

    /// <summary>
    /// Gets the postal/ZIP code.
    /// </summary>
    public string PostalCode { get; }

    /// <summary>
    /// Gets the country.
    /// </summary>
    public string Country { get; }

    private Address(string street, string city, string state, string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    /// <summary>
    /// Creates a new Address value object.
    /// </summary>
    public static Result<Address> Create(
        string? street,
        string? city,
        string? state,
        string? postalCode,
        string? country)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(street))
            errors.Add(Error.Validation("Street", "Street is required."));

        if (string.IsNullOrWhiteSpace(city))
            errors.Add(Error.Validation("City", "City is required."));

        if (string.IsNullOrWhiteSpace(state))
            errors.Add(Error.Validation("State", "State is required."));

        if (string.IsNullOrWhiteSpace(postalCode))
            errors.Add(Error.Validation("PostalCode", "Postal code is required."));

        if (string.IsNullOrWhiteSpace(country))
            errors.Add(Error.Validation("Country", "Country is required."));

        if (errors.Count > 0)
        {
            return Result.Failure<Address>(errors.First());
        }

        return new Address(
            street!.Trim(),
            city!.Trim(),
            state!.Trim(),
            postalCode!.Trim(),
            country!.Trim());
    }

    /// <summary>
    /// Creates an Address without validation (for EF Core materialization).
    /// </summary>
    internal static Address CreateUnsafe(string street, string city, string state, string postalCode, string country)
        => new(street, city, state, postalCode, country);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }

    public override string ToString() => $"{Street}, {City}, {State} {PostalCode}, {Country}";
}
