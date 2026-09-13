using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// Today's trading, for the card an Admin and a Pharmacist both see.
/// </summary>
/// <param name="GrossProfit">
/// <b>Null for a Pharmacist.</b> Withheld by the server rather than hidden by the page: a figure
/// a role may not see should never reach their browser, where it is one developer-tools tab away.
/// Module 8 draws the same line for the same reason.
/// </param>
public sealed record DashboardTodayDto(
    decimal NetSales,
    int TransactionCount,
    decimal? GrossProfit);

/// <summary>Antibiotics dispensed this calendar month. Admin and Pharmacist.</summary>
public sealed record DashboardAntibioticsDto(
    int Month,
    int Year,
    int DispensedInBaseUnits,
    string UnitLabel,
    int SaleLineCount);

/// <summary>
/// What the pharmacy owes its suppliers, net. <b>Admin only.</b>
/// </summary>
/// <param name="TotalOutstanding">
/// The net figure, straight off Module 8's supplier dues report — which computes it from
/// <c>ISupplierBalanceQueries</c>. The dashboard sums nothing itself, which is what makes this
/// card and that report agree by construction rather than by luck.
/// </param>
/// <param name="TotalCreditHeld">
/// What suppliers are holding <em>of the pharmacy's</em> money, where any are. Reported beside
/// the net rather than folded into it, because "you owe 4,000" hides the fact that 1,500 of it is
/// already sitting with a distributor who owes it back. Module 4's word for this is
/// <b>in credit</b>, never "overpaid" — returning goods after paying produces the same state
/// without anybody overpaying.
/// </param>
public sealed record DashboardSupplierDuesDto(
    decimal TotalOutstanding,
    int OwingCount,
    decimal TotalCreditHeld,
    int InCreditCount)
{
    public bool HasCredit => InCreditCount > 0 && TotalCreditHeld > 0m;
}

/// <summary>Salary generated but not yet paid. <b>Admin only.</b></summary>
public sealed record DashboardUnpaidSalaryDto(int Count, decimal Total);

/// <summary>
/// Everything the home screen shows, in one response.
///
/// <para><b>One call, not one per card.</b> The dashboard loads on every visit and for every
/// user; three round trips to draw one screen is three chances for a partial render and three
/// times the latency on a shop's connection. It also lets the server decide role visibility once
/// — a card a role may not see is <c>null</c> here rather than present-but-hidden.</para>
/// </summary>
/// <param name="Role">
/// The caller's role at this pharmacy, from their token. Sent even though the client knows it,
/// because it is what explains why some cards below are null - and because a client deciding for
/// itself which cards to expect is a client that can be wrong about it.
/// </param>
/// <param name="PharmacyName">
/// From settings, and shown prominently. It matters most for somebody with access to more than
/// one pharmacy, where the only thing distinguishing two identical screens is this line.
///
/// <para>The user's own name is deliberately NOT here. The API's token carries no name claim -
/// only a subject, a tenant and a role - so returning one would mean a database round trip for a
/// greeting. The web app already has the full name in its own sign-in cookie and renders the
/// greeting from there.</para>
/// </param>
public sealed record DashboardDto(
    UserRole Role,
    string PharmacyName,
    AlertSummaryDto Alerts,
    DashboardTodayDto? Today,
    DashboardAntibioticsDto? Antibiotics,
    DashboardSupplierDuesDto? SupplierDues,
    DashboardUnpaidSalaryDto? UnpaidSalary)
{
    /// <summary>
    /// Whether anything on this dashboard is asking to be acted on.
    ///
    /// <para>Drives the calm-when-zero rule: a pharmacy with nothing expiring and nothing low
    /// gets one quiet line, not four warning-coloured boxes reading zero. A dashboard that shouts
    /// on a good day trains people to stop reading it.</para>
    /// </summary>
    public bool NeedsAttention => Alerts.HasAnything;
}
