namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Interface for responses that can provide entity ID for access logging.
/// Implement this on DTOs returned from commands/queries that should log the entity ID.
/// </summary>
public interface IAccessLoggableResponse
{
    /// <summary>
    /// Gets the entity ID for access logging.
    /// </summary>
    string? GetEntityId();
}
