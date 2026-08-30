using PMS.SharedKernel.Common;

namespace PMS.Domain.Entities;

/// <summary>
/// Represents an access log entry for audit trail purposes.
/// Tracks user actions on entities for compliance and security monitoring.
/// </summary>
public class AccessLog : BaseEntity<Guid>
{
    /// <summary>
    /// The user ID who performed the action.
    /// </summary>
    public string? UserId { get; private set; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string? UserEmail { get; private set; }

    /// <summary>
    /// The user's role at the time of action.
    /// </summary>
    public string? UserRole { get; private set; }

    /// <summary>
    /// When the action was performed.
    /// </summary>
    public DateTime AccessDateUtc { get; private set; }

    /// <summary>
    /// The type of entity being accessed (e.g., "Customer", "Order").
    /// </summary>
    public string EntityName { get; private set; } = null!;

    /// <summary>
    /// The ID of the entity being accessed.
    /// </summary>
    public string? EntityId { get; private set; }

    /// <summary>
    /// The action performed (e.g., "Create", "Update", "Delete", "View").
    /// </summary>
    public string Action { get; private set; } = null!;

    /// <summary>
    /// The request URL that triggered this action.
    /// </summary>
    public string? RequestUrl { get; private set; }

    /// <summary>
    /// The HTTP method used (GET, POST, PUT, DELETE).
    /// </summary>
    public string? HttpMethod { get; private set; }

    /// <summary>
    /// The client's IP address.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// The client's user agent string.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Additional context or notes about the action.
    /// </summary>
    public string? AdditionalInfo { get; private set; }

    /// <summary>
    /// Whether the action was successful.
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Error message if the action failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    private AccessLog() { } // EF Core

    public static AccessLog Create(
        string action,
        string entityName,
        string? entityId = null,
        string? userId = null,
        string? userEmail = null,
        string? userRole = null,
        string? requestUrl = null,
        string? httpMethod = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? additionalInfo = null,
        bool isSuccess = true,
        string? errorMessage = null)
    {
        return new AccessLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            UserId = userId,
            UserEmail = userEmail,
            UserRole = userRole,
            AccessDateUtc = DateTime.UtcNow,
            RequestUrl = requestUrl,
            HttpMethod = httpMethod,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            AdditionalInfo = additionalInfo,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage
        };
    }
}
