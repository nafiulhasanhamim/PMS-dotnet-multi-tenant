using PMS.SharedKernel.Interfaces;

namespace PMS.SharedKernel.Services;

/// <summary>
/// The system clock, in UTC.
/// </summary>
/// <remarks>
/// <b>This is a second implementation of <see cref="IDateTime"/>.</b>
/// <c>PMS.Infrastructure.Services.DateTimeService</c> is the one registered explicitly, and
/// <see cref="IDateTime"/> derives from <c>ISingletonService</c>, which
/// <c>ServiceLifetimeRegistrar</c> auto-registers by scanning assemblies — so which of the
/// two wins depends on registration order rather than on anyone's intent. They behave
/// identically today, which is exactly why the ambiguity has never shown itself. Worth
/// deleting one; both are kept in step here so the choice stays free.
/// </remarks>
public sealed class DateTimeService : IDateTime
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime UtcToday => DateTime.UtcNow.Date;
}
