using PMS.SharedKernel.DependencyInjection;

namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Abstraction for date/time operations.
/// Allows for easier testing by providing a mockable time source.
/// Registered as Singleton lifetime.
/// </summary>
public interface IDateTime : ISingletonService
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets the current UTC date (time component is midnight).
    /// </summary>
    DateTime Today { get; }
}
