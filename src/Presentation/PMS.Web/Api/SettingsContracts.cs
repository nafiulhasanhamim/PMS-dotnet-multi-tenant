namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 10 wire contracts: settings and the dashboard.
//
// Enum values must match the server's NUMERIC values — the API serialises enums as integers.
// AntibioticPrescriptionMode is declared once in AntibioticContracts.cs and reused here rather
// than redeclared; two copies of a three-value enum with legal consequences is two chances to
// get it wrong differently.
//
// Nothing here holds a default that duplicates a server-side one. Where a fallback is needed for
// a failed call, it is an empty object rather than an invented number: a dashboard that quietly
// showed "90 days" because the API was down would be lying about which window it counted with.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Every setting for this pharmacy, typed.
///
/// <para>Read by any signed-in user — the billing screen needs the caps and the mode, an invoice
/// needs the pharmacy details. Written by an Admin only.</para>
/// </summary>
public sealed record TenantSettings(
    string PharmacyName,
    string PharmacyAddress,
    string PharmacyPhone,
    string PharmacyLicenseNumber,
    int ExpiryAlertWindowDays,
    int DeadStockThresholdDays,
    int DefaultReorderLevel,
    int DiscountCapEmployeePercent,
    int DiscountCapPharmacistPercent,
    AntibioticPrescriptionMode AntibioticPrescriptionMode)
{
    /// <summary>
    /// What a page gets when the settings call fails.
    ///
    /// <para>The numbers here are the same fallbacks <c>SettingKeys</c> declares server-side, and
    /// this is the one place the web app repeats them. A page that reached this has already
    /// failed to load settings and is about to say so; rendering a form full of zeroes would be
    /// worse than rendering one full of the values that were in force before Module 10.</para>
    /// </summary>
    public static TenantSettings Fallback { get; } = new(
        "My Pharmacy", string.Empty, string.Empty, string.Empty,
        90, 90, 100, 5, 10, AntibioticPrescriptionMode.Off);

    public bool HasAddress => !string.IsNullOrWhiteSpace(PharmacyAddress);

    public bool HasPhone => !string.IsNullOrWhiteSpace(PharmacyPhone);

    public bool HasLicense => !string.IsNullOrWhiteSpace(PharmacyLicenseNumber);
}

/// <summary>What a save changed. Lets the screen say something true rather than "Saved".</summary>
public sealed record SettingsSaved(TenantSettings Settings, IReadOnlyList<string> ChangedKeys)
{
    public bool AnythingChanged => ChangedKeys.Count > 0;
}

// ── Dashboard ────────────────────────────────────────────────────────────────────────────

/// <param name="GrossProfit">Null for a Pharmacist — withheld by the server, not hidden here.</param>
public sealed record DashboardToday(
    decimal NetSales,
    int TransactionCount,
    decimal? GrossProfit);

public sealed record DashboardAntibiotics(
    int Month,
    int Year,
    int DispensedInBaseUnits,
    string UnitLabel,
    int SaleLineCount);

/// <param name="TotalCreditHeld">
/// What suppliers hold of the pharmacy's money. Module 4's word for this is <b>in credit</b>,
/// never "overpaid" — returning goods after paying produces the same state without anybody
/// overpaying.
/// </param>
public sealed record DashboardSupplierDues(
    decimal TotalOutstanding,
    int OwingCount,
    decimal TotalCreditHeld,
    int InCreditCount)
{
    public bool HasCredit => InCreditCount > 0 && TotalCreditHeld > 0m;
}

public sealed record DashboardUnpaidSalary(int Count, decimal Total);

/// <summary>
/// The whole home screen, from one call.
///
/// <para>A card a role may not see arrives null. The page renders what is present rather than
/// deciding for itself who sees what — the server already decided, and two implementations of
/// that rule is one too many.</para>
/// </summary>
public sealed record Dashboard(
    UserRole Role,
    string PharmacyName,
    AlertSummary Alerts,
    DashboardToday? Today,
    DashboardAntibiotics? Antibiotics,
    DashboardSupplierDues? SupplierDues,
    DashboardUnpaidSalary? UnpaidSalary)
{
    /// <summary>
    /// Whether anything here is asking to be acted on.
    ///
    /// <para>Drives the calm-when-zero rule: a pharmacy with nothing expiring and nothing low
    /// gets one quiet line, not four warning-coloured boxes reading zero. A dashboard that shouts
    /// on a good day trains people to stop reading it.</para>
    /// </summary>
    public bool NeedsAttention => Alerts.HasAnything;
}
