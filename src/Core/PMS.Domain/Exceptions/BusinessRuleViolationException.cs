namespace PMS.Domain.Exceptions;

/// <summary>
/// Exception thrown when a business rule is violated.
/// </summary>
public class BusinessRuleViolationException : DomainException
{
    /// <summary>
    /// Gets the name of the business rule that was violated.
    /// </summary>
    public string RuleName { get; }

    /// <summary>
    /// Creates a new business rule violation exception.
    /// </summary>
    /// <param name="ruleName">The name of the rule.</param>
    /// <param name="message">The violation message.</param>
    public BusinessRuleViolationException(string ruleName, string message)
        : base(message)
    {
        RuleName = ruleName;
    }

    /// <summary>
    /// Creates a new business rule violation exception with details.
    /// </summary>
    /// <param name="ruleName">The name of the rule.</param>
    /// <param name="message">The violation message.</param>
    /// <param name="innerException">The inner exception.</param>
    public BusinessRuleViolationException(string ruleName, string message, Exception innerException)
        : base(message, innerException)
    {
        RuleName = ruleName;
    }
}
