using PMS.SharedKernel.Interfaces;

namespace PMS.Infrastructure.Services;

/// <summary>
/// The system clock, in UTC. This is the implementation registered in
/// <c>PMS.Infrastructure.DependencyInjection</c>.
/// </summary>
/// <remarks>
/// <see cref="UtcToday"/> is <c>DateTime.UtcNow.Date</c> and not <c>DateTime.Today</c>. The
/// two are the same date for eighteen hours a day in Bangladesh and differ for the other six,
/// so the old code was right often enough to look correct and wrong every night.
/// </remarks>
public class DateTimeService : IDateTime
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime UtcToday => DateTime.UtcNow.Date;
}
