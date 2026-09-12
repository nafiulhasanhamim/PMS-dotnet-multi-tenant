using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Grid;

namespace PMS.Persistence.Services;

/// <summary>
/// The read side of Module 9. See <see cref="ISalaryQueries"/>.
///
/// <para><b>Every method that needs an advance total runs it as a separate grouped aggregate and
/// joins in memory.</b> Writing "each profile, with the sum of its unsettled advances" as a
/// correlated subquery inside a projection reads far better and fails at runtime: SQL Server
/// rejects <em>"Cannot perform an aggregate function on an expression containing an aggregate or a
/// subquery"</em>, which this codebase has now hit in Modules 6, 7 and 8. The membership query
/// stays aggregate-free — it decides <em>which</em> profiles — and the aggregate runs over
/// <c>SalaryAdvances</c> filtered by those ids. Identical structure to
/// <see cref="SupplierBalanceQueries"/>, for identical reasons.</para>
///
/// <para>Employee names come from <c>Users</c>, which is a global table with no tenant filter. That
/// is safe here because the profile rows doing the joining are tenant-filtered: a name only ever
/// arrives attached to a profile this pharmacy owns.</para>
/// </summary>
public sealed class SalaryQueries : ISalaryQueries
{
    private readonly ApplicationDbContext _context;

    public SalaryQueries(ApplicationDbContext context) => _context = context;

    // ── Profiles ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<GridResult<SalaryProfileListItemDto>> GetProfilesAsync(
        string? search,
        SalaryProfileStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = from profile in _context.EmployeeSalaryProfiles.AsNoTracking()
                    join user in _context.Users.AsNoTracking() on profile.UserId equals user.Id
                    select new { profile, user };

        query = status switch
        {
            SalaryProfileStatusFilter.Active => query.Where(x => x.profile.IsActive),
            SalaryProfileStatusFilter.Inactive => query.Where(x => !x.profile.IsActive),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                EF.Functions.Like(x.user.FullName, $"%{term}%")
                || (x.profile.Designation != null
                    && EF.Functions.Like(x.profile.Designation, $"%{term}%")));
        }

        var total = await query.CountAsync(cancellationToken);

        // Active first, then by name. A payroll screen is read to find somebody currently on it.
        var rows = await query
            .OrderByDescending(x => x.profile.IsActive)
            .ThenBy(x => x.user.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.profile.Id,
                x.profile.UserId,
                x.user.FullName,
                x.user.Email,
                x.profile.Designation,
                x.profile.MonthlyBaseSalary,
                x.profile.JoiningDate,
                x.profile.IsActive,
            })
            .ToListAsync(cancellationToken);

        var outstanding = await GetUnsettledTotalsAsync(
            rows.Select(r => r.Id).ToList(), cancellationToken);

        return new GridResult<SalaryProfileListItemDto>
        {
            Data = rows.Select(r =>
            {
                var owed = outstanding.TryGetValue(r.Id, out var o) ? o : (0m, 0);

                return new SalaryProfileListItemDto(
                    r.Id, r.UserId, r.FullName, r.Email, r.Designation,
                    r.MonthlyBaseSalary, r.JoiningDate, r.IsActive, owed.Item1, owed.Item2);
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <inheritdoc />
    public async Task<SalaryProfileDetailDto?> GetProfileAsync(
        Guid profileId, CancellationToken cancellationToken = default) =>
        await (from profile in _context.EmployeeSalaryProfiles.AsNoTracking()
               join user in _context.Users.AsNoTracking() on profile.UserId equals user.Id
               where profile.Id == profileId
               select new SalaryProfileDetailDto(
                   profile.Id, profile.UserId, user.FullName, user.Email,
                   profile.Designation, profile.MonthlyBaseSalary, profile.JoiningDate,
                   profile.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SalaryEligibleUserDto>> GetEligibleUsersAsync(
        CancellationToken cancellationToken = default)
    {
        // Memberships carry their own tenant filter, so this is already this pharmacy's staff.
        // Platform-level rows have a null TenantId and are excluded by that filter.
        var members = await (from membership in _context.UserTenantMemberships.AsNoTracking()
                             join user in _context.Users.AsNoTracking()
                                 on membership.UserId equals user.Id
                             where membership.IsActive
                             select new
                             {
                                 user.Id,
                                 user.FullName,
                                 user.Email,
                                 membership.Role,
                             })
            .ToListAsync(cancellationToken);

        // Only ACTIVE profiles disqualify somebody. A rejoiner whose old profile was deactivated
        // gets a new one — which is exactly what the filtered unique index allows.
        var alreadyOnPayroll = await _context.EmployeeSalaryProfiles
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        var taken = alreadyOnPayroll.ToHashSet();

        return members
            .Where(m => !taken.Contains(m.Id))
            .OrderBy(m => m.FullName)
            .Select(m => new SalaryEligibleUserDto(
                m.Id, m.FullName, m.Email, m.Role.ToString()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SalaryProfileOptionDto>> GetProfileOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await (from profile in _context.EmployeeSalaryProfiles.AsNoTracking()
                              join user in _context.Users.AsNoTracking()
                                  on profile.UserId equals user.Id
                              where profile.IsActive
                              orderby user.FullName
                              select new
                              {
                                  profile.Id,
                                  user.FullName,
                                  profile.Designation,
                                  profile.MonthlyBaseSalary,
                              })
            .ToListAsync(cancellationToken);

        var outstanding = await GetUnsettledTotalsAsync(
            profiles.Select(p => p.Id).ToList(), cancellationToken);

        return profiles
            .Select(p => new SalaryProfileOptionDto(
                p.Id, p.FullName, p.Designation, p.MonthlyBaseSalary,
                outstanding.TryGetValue(p.Id, out var o) ? o.Total : 0m))
            .ToList();
    }

    // ── Advances ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<GridResult<SalaryAdvanceRowDto>> GetAdvancesAsync(
        Guid? profileId,
        AdvanceSettlementFilter settlement,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = from advance in _context.SalaryAdvances.AsNoTracking()
                    join profile in _context.EmployeeSalaryProfiles.AsNoTracking()
                        on advance.EmployeeSalaryProfileId equals profile.Id
                    join employee in _context.Users.AsNoTracking()
                        on profile.UserId equals employee.Id
                    join giver in _context.Users.AsNoTracking()
                        on advance.GivenByUserId equals giver.Id
                    select new { advance, profile, employee, giver };

        if (profileId is { } id)
        {
            query = query.Where(x => x.advance.EmployeeSalaryProfileId == id);
        }

        query = settlement switch
        {
            AdvanceSettlementFilter.Unsettled => query.Where(x => !x.advance.IsSettled),
            AdvanceSettlementFilter.Settled => query.Where(x => x.advance.IsSettled),
            _ => query,
        };

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.advance.AdvanceDate)
            .ThenByDescending(x => x.advance.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.advance.Id,
                ProfileId = x.profile.Id,
                EmployeeName = x.employee.FullName,
                x.profile.Designation,
                x.advance.Amount,
                x.advance.AdvanceDate,
                x.advance.Reason,
                GivenByName = x.giver.FullName,
                x.advance.IsSettled,
                x.advance.SettledInSalaryEntryId,
            })
            .ToListAsync(cancellationToken);

        // Which month settled each one. A second round-trip rather than a fourth join, so that a
        // page of entirely unsettled advances costs nothing extra.
        var settledIds = rows
            .Where(r => r.SettledInSalaryEntryId is not null)
            .Select(r => r.SettledInSalaryEntryId!.Value)
            .Distinct()
            .ToList();

        var periods = settledIds.Count == 0
            ? []
            : await _context.SalaryEntries
                .AsNoTracking()
                .Where(e => settledIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Month, e.Year })
                .ToListAsync(cancellationToken);

        var periodById = periods.ToDictionary(p => p.Id, p => (p.Month, p.Year));

        return new GridResult<SalaryAdvanceRowDto>
        {
            Data = rows.Select(r =>
            {
                (int Month, int Year)? period = r.SettledInSalaryEntryId is { } entryId
                    && periodById.TryGetValue(entryId, out var found)
                    ? found
                    : null;

                return new SalaryAdvanceRowDto(
                    r.Id, r.ProfileId, r.EmployeeName, r.Designation, r.Amount, r.AdvanceDate,
                    r.Reason, r.GivenByName, r.IsSettled, r.SettledInSalaryEntryId,
                    period?.Month, period?.Year);
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    // ── Generation ───────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SalaryGenerationPreviewDto> GetGenerationPreviewAsync(
        int month, int year, CancellationToken cancellationToken = default)
    {
        var profiles = await (from profile in _context.EmployeeSalaryProfiles.AsNoTracking()
                              join user in _context.Users.AsNoTracking()
                                  on profile.UserId equals user.Id
                              where profile.IsActive
                              orderby user.FullName
                              select new
                              {
                                  profile.Id,
                                  profile.UserId,
                                  user.FullName,
                                  profile.Designation,
                                  profile.MonthlyBaseSalary,
                              })
            .ToListAsync(cancellationToken);

        if (profiles.Count == 0)
        {
            return new SalaryGenerationPreviewDto(month, year, []);
        }

        var ids = profiles.Select(p => p.Id).ToList();

        // The advances themselves, not a sum: the screen shows each one's date, amount and
        // reason, and the total is added up from them. One list serves both, and the total can
        // never disagree with the breakdown under it.
        var advances = await _context.SalaryAdvances
            .AsNoTracking()
            .Where(a => !a.IsSettled && ids.Contains(a.EmployeeSalaryProfileId))
            .OrderBy(a => a.AdvanceDate)
            .ThenBy(a => a.CreatedOnUtc)
            .Select(a => new
            {
                a.EmployeeSalaryProfileId,
                a.Id,
                a.Amount,
                a.AdvanceDate,
                a.Reason,
            })
            .ToListAsync(cancellationToken);

        var byProfile = advances
            .GroupBy(a => a.EmployeeSalaryProfileId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<UnsettledAdvanceDto>)g
                    .Select(a => new UnsettledAdvanceDto(a.Id, a.Amount, a.AdvanceDate, a.Reason))
                    .ToList());

        // Already generated for this period. Flagged, not filtered out — an employee missing from
        // the list is indistinguishable from one nobody put on the payroll.
        var existing = await _context.SalaryEntries
            .AsNoTracking()
            .Where(e => e.Month == month && e.Year == year
                        && ids.Contains(e.EmployeeSalaryProfileId))
            .Select(e => new { e.EmployeeSalaryProfileId, e.Id })
            .ToListAsync(cancellationToken);

        var existingByProfile = existing.ToDictionary(e => e.EmployeeSalaryProfileId, e => e.Id);

        var rows = profiles.Select(p =>
        {
            var unsettled = byProfile.TryGetValue(p.Id, out var list) ? list : [];

            return new SalaryGenerationRowDto(
                p.Id,
                p.UserId,
                p.FullName,
                p.Designation,
                p.MonthlyBaseSalary,
                unsettled.Sum(a => a.Amount),
                unsettled,
                existingByProfile.ContainsKey(p.Id),
                existingByProfile.TryGetValue(p.Id, out var entryId) ? entryId : null);
        }).ToList();

        return new SalaryGenerationPreviewDto(month, year, rows);
    }

    // ── Entries ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<GridResult<SalaryEntryRowDto>> GetEntriesAsync(
        int? month,
        int? year,
        Guid? profileId,
        SalaryStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Filtered on SalaryEntry itself, BEFORE the join. See ProjectEntries for why that
        // ordering is forced rather than stylistic.
        var entries = _context.SalaryEntries.AsNoTracking();

        if (month is { } m)
        {
            entries = entries.Where(e => e.Month == m);
        }

        if (year is { } y)
        {
            entries = entries.Where(e => e.Year == y);
        }

        if (profileId is { } id)
        {
            entries = entries.Where(e => e.EmployeeSalaryProfileId == id);
        }

        entries = status switch
        {
            SalaryStatusFilter.Unpaid =>
                entries.Where(e => e.PaymentStatus == SalaryPaymentStatus.Unpaid),
            SalaryStatusFilter.Paid =>
                entries.Where(e => e.PaymentStatus == SalaryPaymentStatus.Paid),
            _ => entries,
        };

        // Counted over the entries alone. The joins are inner joins on required foreign keys, so
        // they cannot change the count - and counting before projecting keeps the count query
        // free of the two joins entirely.
        var total = await entries.CountAsync(cancellationToken);

        var rows = await ProjectEntries(entries)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new GridResult<SalaryEntryRowDto>
        {
            Data = rows,
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <inheritdoc />
    public async Task<SalaryEntryRowDto?> GetEntryAsync(
        Guid entryId, CancellationToken cancellationToken = default) =>
        await ProjectEntries(_context.SalaryEntries.AsNoTracking().Where(e => e.Id == entryId))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<SalarySlipDto?> GetSlipAsync(
        Guid entryId, CancellationToken cancellationToken = default)
    {
        var slip = await (from entry in _context.SalaryEntries.AsNoTracking()
                          join profile in _context.EmployeeSalaryProfiles.AsNoTracking()
                              on entry.EmployeeSalaryProfileId equals profile.Id
                          join employee in _context.Users.AsNoTracking()
                              on profile.UserId equals employee.Id
                          join generator in _context.Users.AsNoTracking()
                              on entry.GeneratedByUserId equals generator.Id
                          where entry.Id == entryId
                          select new
                          {
                              entry.Id,
                              EmployeeName = employee.FullName,
                              EmployeeEmail = employee.Email,
                              profile.Designation,
                              profile.JoiningDate,
                              entry.Month,
                              entry.Year,
                              entry.BaseSalary,
                              entry.Bonus,
                              entry.AdvanceDeduction,
                              entry.OtherDeduction,
                              entry.AdjustmentNotes,
                              entry.NetPayable,
                              entry.PaymentStatus,
                              entry.PaymentDate,
                              GeneratedByName = generator.FullName,
                              entry.CreatedOnUtc,
                          })
            .FirstOrDefaultAsync(cancellationToken);

        if (slip is null)
        {
            return null;
        }

        // What this entry actually recovered. The reason a slip is worth printing: an employee
        // handed less than their base salary can see which advances account for the difference,
        // and on what dates.
        var settled = await _context.SalaryAdvances
            .AsNoTracking()
            .Where(a => a.SettledInSalaryEntryId == entryId)
            .OrderBy(a => a.AdvanceDate)
            .Select(a => new UnsettledAdvanceDto(a.Id, a.Amount, a.AdvanceDate, a.Reason))
            .ToListAsync(cancellationToken);

        return new SalarySlipDto(
            slip.Id, slip.EmployeeName, slip.EmployeeEmail, slip.Designation, slip.JoiningDate,
            slip.Month, slip.Year, slip.BaseSalary, slip.Bonus, slip.AdvanceDeduction,
            slip.OtherDeduction, slip.AdjustmentNotes, slip.NetPayable, slip.PaymentStatus,
            slip.PaymentDate, settled, slip.GeneratedByName, slip.CreatedOnUtc);
    }

    // ── Landing ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SalarySummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        // Three round-trips over three base tables. Composing them into one projection is the
        // shape that fails — see the class remarks.
        var unpaid = await _context.SalaryEntries
            .AsNoTracking()
            .Where(e => e.PaymentStatus == SalaryPaymentStatus.Unpaid)
            .GroupBy(e => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(e => e.NetPayable) })
            .FirstOrDefaultAsync(cancellationToken);

        var advances = await _context.SalaryAdvances
            .AsNoTracking()
            .Where(a => !a.IsSettled)
            .GroupBy(a => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(a => a.Amount) })
            .FirstOrDefaultAsync(cancellationToken);

        var headcount = await _context.EmployeeSalaryProfiles
            .AsNoTracking()
            .CountAsync(p => p.IsActive, cancellationToken);

        return new SalarySummaryDto(
            unpaid?.Count ?? 0,
            unpaid?.Total ?? 0m,
            advances?.Count ?? 0,
            advances?.Total ?? 0m,
            headcount);
    }

    // ── Shared shapes ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Joins already-filtered entries to their employee and projects the row. The list and the
    /// single-entry read both go through here.
    ///
    /// <para><b>The projection has to be terminal, and the filters have to come first.</b>
    /// Projecting the join into a named record so that both callers could filter and order over
    /// it reads better and does not translate: EF cannot see through a record constructor, and
    /// the whole query fails at runtime with "could not be translated". So the caller narrows
    /// <paramref name="entries"/> - plain predicates over one entity, which always translate -
    /// and everything after the join happens here.</para>
    ///
    /// <para>Sharing it is still worth the awkwardness. Module 8 shipped a report whose streaming
    /// path worked while its paginated path 500'd, because the two built their own projections;
    /// one expression that both call cannot drift.</para>
    /// </summary>
    private IQueryable<SalaryEntryRowDto> ProjectEntries(IQueryable<SalaryEntry> entries) =>
        from entry in entries
        join profile in _context.EmployeeSalaryProfiles.AsNoTracking()
            on entry.EmployeeSalaryProfileId equals profile.Id
        join user in _context.Users.AsNoTracking() on profile.UserId equals user.Id
        orderby entry.Year descending, entry.Month descending, user.FullName
        select new SalaryEntryRowDto(
            entry.Id,
            profile.Id,
            user.FullName,
            profile.Designation,
            entry.Month,
            entry.Year,
            entry.BaseSalary,
            entry.Bonus,
            entry.AdvanceDeduction,
            entry.OtherDeduction,
            entry.AdjustmentNotes,
            entry.NetPayable,
            entry.PaymentStatus,
            entry.PaymentDate);

    /// <summary>
    /// Unsettled advance totals and counts for a set of profiles.
    ///
    /// <para>Aggregates over <c>SalaryAdvances</c> filtered by id, rather than being projected
    /// into the profile row. See the class remarks — this is the shape that works.</para>
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, (decimal Total, int Count)>>
        GetUnsettledTotalsAsync(
            IReadOnlyCollection<Guid> profileIds, CancellationToken cancellationToken)
    {
        if (profileIds.Count == 0)
        {
            return new Dictionary<Guid, (decimal, int)>();
        }

        var ids = profileIds.Distinct().ToList();

        var totals = await _context.SalaryAdvances
            .AsNoTracking()
            .Where(a => !a.IsSettled && ids.Contains(a.EmployeeSalaryProfileId))
            .GroupBy(a => a.EmployeeSalaryProfileId)
            .Select(g => new
            {
                ProfileId = g.Key,
                Total = g.Sum(a => a.Amount),
                Count = g.Count(),
            })
            .ToListAsync(cancellationToken);

        return totals.ToDictionary(t => t.ProfileId, t => (t.Total, t.Count));
    }
}
