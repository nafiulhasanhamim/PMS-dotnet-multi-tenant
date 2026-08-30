namespace PMS.Domain.Exceptions;

/// <summary>
/// Base exception for domain rule violations.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Creates a new domain exception with the specified message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public DomainException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new domain exception with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
