using PMS.SharedKernel.Interfaces;

namespace PMS.SharedKernel.Services;

/// <summary>
/// Default implementation of IDateTime that uses system time.
/// </summary>
public sealed class DateTimeService : IDateTime
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime Now => DateTime.Now;

    /// <inheritdoc />
    public DateTime Today => DateTime.Today;
}
