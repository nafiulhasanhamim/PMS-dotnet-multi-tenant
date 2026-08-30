namespace PMS.SharedKernel.Results;

/// <summary>
/// Represents an error with a code and description.
/// </summary>
public record Error(string Code, string Description)
{
    /// <summary>
    /// Represents no error (for successful results).
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>
    /// Represents a null value error.
    /// </summary>
    public static readonly Error NullValue = new("Error.NullValue", "The specified value is null.");

    /// <summary>
    /// Creates a not found error.
    /// </summary>
    /// <param name="entityName">The name of the entity.</param>
    /// <param name="id">The identifier that was not found.</param>
    public static Error NotFound(string entityName, object id) =>
        new($"{entityName}.NotFound", $"{entityName} with id '{id}' was not found.");

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    /// <param name="propertyName">The property that failed validation.</param>
    /// <param name="description">The validation error description.</param>
    public static Error Validation(string propertyName, string description) =>
        new($"Validation.{propertyName}", description);

    /// <summary>
    /// Creates a conflict error (e.g., duplicate record).
    /// </summary>
    /// <param name="description">The conflict description.</param>
    public static Error Conflict(string description) =>
        new("Error.Conflict", description);

    /// <summary>
    /// Creates an unauthorized error.
    /// </summary>
    public static Error Unauthorized(string description = "Unauthorized access.") =>
        new("Error.Unauthorized", description);

    /// <summary>
    /// Creates a forbidden error.
    /// </summary>
    public static Error Forbidden(string description = "Access is forbidden.") =>
        new("Error.Forbidden", description);
}
