using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Application.Common.Settings;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Dashboard.Queries.GetDashboard;

/// <summary>The home screen, in one request.</summary>
public sealed record GetDashboardQuery : IRequest<Result<DashboardDto>>, ITenantScopedRequest;

/// <summary>
/// Assembles the home screen from the query services the other modules already own.
///
/// <para><b>Nothing here computes an aggregate of its own.</b> The alert counts come from Module
/// 6's <c>IAlertQueries</c>, today's trading from Module 8's <c>IReportQueries</c>, the
/// antibiotic figure from Module 7, the supplier dues from Module 8's report — which itself gets
/// them from Module 4's <c>ISupplierBalanceQueries</c> — and the unpaid salary from Module 9.
/// That is what makes each card provably equal to the screen it links to: they are the same
/// query, not two implementations of the same idea. A dashboard that disagreed with the report it
/// linked to would destroy trust in both.</para>
///
/// <para><b>Role tiering happens here, not in the page.</b> A figure a role may not see is
/// <c>null</c> in the response rather than present-and-hidden — hiding it in Razor would leave it
/// one developer-tools tab away. Module 8 draws the same line.</para>
///
/// <para><b>On the aggregate-over-subquery trap</b> that has bitten Modules 6, 7, 8 and 9: it
/// cannot arise here, because this handler issues several independent queries and combines their
/// results in C#. There is no projection whose columns are correlated aggregates. That is a
/// consequence of reusing the existing services rather than writing one combined query, and it is
/// the second reason to do so.</para>
///
/// <para><b>Cost.</b> Four to six round trips depending on role, none of them per-card and none
/// of them in a loop. The one deliberate over-fetch is today's trading: <c>GetDailySalesReport</c>
/// returns the day's invoice rows and hourly breakdown when this card needs two numbers off the
/// top. A narrower query would be cheaper and would be a second definition of "today's takings" —
/// the exact drift this handler exists to avoid. It is one query, not many, and a day's invoices
/// is a small set.</para>
/// </summary>
public sealed class GetDashboardQueryHandler
    : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    private readonly IAlertQueries _alerts;
    private readonly IReportQueries _reports;
    private readonly IAntibioticQueries _antibiotics;
    private readonly ISalaryQueries _salary;
    private readonly ISettingsService _settings;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public GetDashboardQueryHandler(
        IAlertQueries alerts,
        IReportQueries reports,
        IAntibioticQueries antibiotics,
        ISalaryQueries salary,
        ISettingsService settings,
        ICurrentUserService currentUser,
        IDateTime clock)
    {
        _alerts = alerts;
        _reports = reports;
        _antibiotics = antibiotics;
        _salary = salary;
        _settings = settings;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<DashboardDto>> Handle(
        GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var role = _currentUser.TenantRole();

        if (role is null)
        {
            return Result.Failure<DashboardDto>(Error.Unauthorized("Not signed in."));
        }

        var isAdmin = role is UserRole.Admin;
        var seesTrading = role is UserRole.Admin or UserRole.Pharmacist;

        var pharmacyName = await _settings.GetStringAsync(
            SettingKeys.PharmacyName, cancellationToken);

        // The pharmacy's own window, and it travels back on the summary so the card can say
        // "within N days" using the number it actually counted with.
        var window = await _settings.GetIntAsync(
            SettingKeys.ExpiryAlertWindowDays, cancellationToken);

        var alerts = await _alerts.GetAlertSummaryAsync(window, cancellationToken);

        DashboardTodayDto? today = null;
        DashboardAntibioticsDto? antibiotics = null;
        DashboardSupplierDuesDto? dues = null;
        DashboardUnpaidSalaryDto? unpaidSalary = null;

        if (seesTrading)
        {
            var date = DateOnly.FromDateTime(_clock.UtcToday);
            var day = await _reports.GetDailySalesReportAsync(date, cancellationToken);

            today = new DashboardTodayDto(
                day.TotalSales,
                day.TransactionCount,

                // Withheld from a Pharmacist. What the business earns on what it sells is the
                // owner's business, and Module 8 gates every profit figure the same way.
                isAdmin ? day.GrossProfit : null);

            var month = await _antibiotics.GetMonthlySummaryAsync(
                date.Month, date.Year, cancellationToken);

            antibiotics = new DashboardAntibioticsDto(
                month.Month, month.Year, month.TotalDispensedInBaseUnits,
                month.UnitLabel, month.SaleLineCount);
        }

        if (isAdmin)
        {
            // All time, and no date range: a debt is a fact about now rather than about a window.
            // This is Module 8's own report, so the card and the report cannot disagree.
            var supplierDues = await _reports.GetSupplierDuesAsync(
                from: null, to: null, allTime: true, cancellationToken);

            dues = new DashboardSupplierDuesDto(
                supplierDues.TotalOutstanding,
                supplierDues.OwingCount,
                supplierDues.TotalCreditHeld,
                supplierDues.OverpaidCount);

            var salary = await _salary.GetSummaryAsync(cancellationToken);

            unpaidSalary = new DashboardUnpaidSalaryDto(
                salary.UnpaidEntryCount, salary.UnpaidEntryTotal);
        }

        return Result.Success(new DashboardDto(
            role.Value,
            pharmacyName,
            alerts,
            today,
            antibiotics,
            dues,
            unpaidSalary));
    }
}
