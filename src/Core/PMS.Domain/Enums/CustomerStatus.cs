namespace PMS.Domain.Enums;

/// <summary>
/// Represents the status of a customer account.
/// </summary>
public enum CustomerStatus
{
    /// <summary>
    /// Customer account is pending activation.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Customer account is active and in good standing.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Customer account is temporarily suspended.
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// Customer account has been deactivated.
    /// </summary>
    Inactive = 3
}
