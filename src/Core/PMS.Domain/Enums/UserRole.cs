namespace PMS.Domain.Enums;

/// <summary>
/// What a user may do, within one membership.
///
/// A role belongs to a <c>UserTenantMembership</c>, never to the user: the same person can be
/// a Pharmacist at one pharmacy and an Employee at another.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Operates the platform itself — creates pharmacies and their first Admin. Held only by
    /// a membership with no tenant, and first so it reads clearly in constraints and data.
    /// </summary>
    PlatformAdmin = 0,

    /// <summary>Runs one pharmacy, including its staff.</summary>
    Admin = 1,

    /// <summary>Pharmacy staff who may dispense.</summary>
    Pharmacist = 2,

    /// <summary>Counter staff.</summary>
    Employee = 3,
}
