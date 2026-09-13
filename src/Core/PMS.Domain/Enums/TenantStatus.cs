namespace PMS.Domain.Enums;

/// <summary>Where a pharmacy stands with the platform.</summary>
public enum TenantStatus
{
    /// <summary>Newly created and usable. The default for a new pharmacy.</summary>
    Trial = 0,

    /// <summary>Paying and usable.</summary>
    Active = 1,

    /// <summary>Access withdrawn. Nobody can sign in; the data is untouched.</summary>
    Suspended = 2,
}
