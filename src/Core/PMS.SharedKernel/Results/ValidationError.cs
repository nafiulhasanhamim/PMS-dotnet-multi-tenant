namespace PMS.SharedKernel.Results;

/// <summary>
/// Represents a validation error with multiple property errors.
/// </summary>
public sealed record ValidationError : Error
{
    public ValidationError(IReadOnlyDictionary<string, string[]> errors)
        : base("Validation.Error", "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets the validation errors grouped by property name.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Creates a validation error from a dictionary of property errors.
    /// </summary>
    public static ValidationError FromDictionary(Dictionary<string, string[]> errors) =>
        new(errors);

    /// <summary>
    /// Creates a validation error for a single property.
    /// </summary>
    public static ValidationError FromProperty(string propertyName, params string[] messages) =>
        new(new Dictionary<string, string[]> { { propertyName, messages } });
}
