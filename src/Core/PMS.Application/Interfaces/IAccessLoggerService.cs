using PMS.SharedKernel.DependencyInjection;

namespace PMS.Application.Interfaces;

/// <summary>
/// Service for logging access/audit trail entries to the database.
/// </summary>
public interface IAccessLoggerService : IScopedService
{
    /// <summary>
    /// Logs an access entry asynchronously.
    /// </summary>
    /// <param name="entry">The access log entry details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogAsync(AccessLogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an access log entry to be recorded.
/// </summary>
public record AccessLogEntry(
    string Action,
    string EntityName,
    string? EntityId = null,
    string? AdditionalInfo = null,
    bool IsSuccess = true,
    string? ErrorMessage = null);
