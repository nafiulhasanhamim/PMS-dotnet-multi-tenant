using PMS.SharedKernel.Common;
using PMS.SharedKernel.Results;

namespace PMS.Domain.ValueObjects;

/// <summary>
/// Represents a monetary value with currency.
/// </summary>
public sealed class Money : ValueObject
{
    /// <summary>
    /// Gets the monetary amount.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// Gets the currency code (e.g., "USD", "EUR").
    /// </summary>
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a new Money value object.
    /// </summary>
    /// <param name="amount">The monetary amount.</param>
    /// <param name="currency">The currency code (3 characters, e.g., "USD").</param>
    public static Result<Money> Create(decimal amount, string? currency)
    {
        if (amount < 0)
        {
            return Result.Failure<Money>(Error.Validation("Amount", "Amount cannot be negative."));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result.Failure<Money>(Error.Validation("Currency", "Currency is required."));
        }

        currency = currency.Trim().ToUpperInvariant();

        if (currency.Length != 3)
        {
            return Result.Failure<Money>(Error.Validation("Currency", "Currency must be a 3-character code."));
        }

        return new Money(amount, currency);
    }

    /// <summary>
    /// Creates a Money object in USD.
    /// </summary>
    public static Result<Money> Usd(decimal amount) => Create(amount, "USD");

    /// <summary>
    /// Creates a Money object in EUR.
    /// </summary>
    public static Result<Money> Eur(decimal amount) => Create(amount, "EUR");

    /// <summary>
    /// Creates Money without validation (for EF Core materialization).
    /// </summary>
    internal static Money CreateUnsafe(decimal amount, string currency) => new(amount, currency);

    /// <summary>
    /// Returns zero amount in the specified currency.
    /// </summary>
    public static Money Zero(string currency) => new(0, currency.ToUpperInvariant());

    /// <summary>
    /// Adds two Money values (must be same currency).
    /// </summary>
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add {Currency} to {other.Currency}");

        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Subtracts a Money value from this one (must be same currency).
    /// </summary>
    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot subtract {other.Currency} from {Currency}");

        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>
    /// Multiplies the amount by a factor.
    /// </summary>
    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:F2} {Currency}";

    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);
    public static Money operator *(Money money, decimal factor) => money.Multiply(factor);
    public static Money operator *(decimal factor, Money money) => money.Multiply(factor);
}
