using PMS.SharedKernel.Interfaces;

namespace PMS.Infrastructure.Services;

/// <summary>
/// Provides date/time operations using the system clock.
/// Allows for easier testing by providing a mockable time source.
/// </summary>
public class DateTimeService : IDateTime
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime Now => DateTime.Now;

    /// <inheritdoc />
    public DateTime Today => DateTime.Today;
}
