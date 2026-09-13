using Ardalis.Specification;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Salary.Specifications;

/// <summary>
/// Active salary profiles by id, for generation.
///
/// <para>Active only, and that filter is load-bearing rather than cosmetic. Somebody deactivated
/// between opening the generation screen and pressing the button has left the pharmacy; paying
/// them because the browser still had their row is exactly the outcome the check prevents.</para>
/// </summary>
public sealed class ActiveProfilesByIdsSpec : Specification<EmployeeSalaryProfile>
{
    public ActiveProfilesByIdsSpec(IReadOnlyCollection<Guid> ids)
    {
        Query.Where(profile => profile.IsActive && ids.Contains(profile.Id));
    }
}

/// <summary>
/// Every unsettled advance belonging to a set of profiles, tracked so settlement can write to it.
///
/// <para><b>No date filter, deliberately.</b> An advance given in June and never deducted must
/// still appear in October's generation — an advance the system quietly forgot would be worse
/// than no advance tracking at all. See <c>SalaryAdvance</c>.</para>
/// </summary>
public sealed class UnsettledAdvancesForProfilesSpec : Specification<SalaryAdvance>
{
    public UnsettledAdvancesForProfilesSpec(IReadOnlyCollection<Guid> profileIds)
    {
        Query.Where(advance =>
            !advance.IsSettled && profileIds.Contains(advance.EmployeeSalaryProfileId));
    }
}

/// <summary>
/// Everything one unpaid entry could settle: the advances it currently holds, plus anything still
/// unsettled for the same employee.
///
/// <para>Both halves are needed because revising an entry can move settlement either way — a
/// deduction edited down releases advances, and one edited up takes more. Loading only the
/// currently-settled ones would make an increase silently recover nothing.</para>
/// </summary>
public sealed class AdvancesForEntryRevisionSpec : Specification<SalaryAdvance>
{
    public AdvancesForEntryRevisionSpec(Guid profileId, Guid entryId)
    {
        Query.Where(advance =>
            advance.EmployeeSalaryProfileId == profileId
            && (!advance.IsSettled || advance.SettledInSalaryEntryId == entryId));
    }
}

/// <summary>Existing entries for a period, so generation can refuse a duplicate before writing.</summary>
public sealed class EntriesForPeriodSpec : Specification<SalaryEntry>
{
    public EntriesForPeriodSpec(int month, int year, IReadOnlyCollection<Guid> profileIds)
    {
        Query.Where(entry =>
            entry.Month == month
            && entry.Year == year
            && profileIds.Contains(entry.EmployeeSalaryProfileId));
    }
}

/// <summary>An active profile for one user, to keep a second one off the payroll.</summary>
public sealed class ActiveProfileForUserSpec : SingleResultSpecification<EmployeeSalaryProfile>
{
    public ActiveProfileForUserSpec(Guid userId)
    {
        Query.Where(profile => profile.IsActive && profile.UserId == userId);
    }
}
