namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 7 wire contracts.
//
// Enum values must match the server's NUMERIC values — the API serialises enums as integers.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// How strictly this pharmacy captures prescriptions for antibiotic sales.
///
/// <para>See the server-side enum for the argument. The short version: antibiotics legally
/// require a prescription and most retail pharmacies sell them over the counter, so a system
/// that hard-blocks that produces invented patient names rather than compliance. Each pharmacy
/// picks the level that matches how it actually operates, and the register records every
/// antibiotic sale either way.</para>
/// </summary>
public enum AntibioticPrescriptionMode
{
    Off = 0,
    Optional = 1,
    Required = 2,
}

public enum PrescriptionStatusFilter
{
    All = 0,
    WithPrescription = 1,
    WithoutPrescription = 2,
}

/// <summary>One antibiotic line item in the register.</summary>
public sealed record AntibioticRegisterRow(
    Guid SaleId,
    Guid SaleLineId,
    string InvoiceNumber,
    DateTime SaleDate,
    Guid ProductId,
    string BrandName,
    string? GenericName,
    int QuantityInBaseUnits,
    string FormattedQuantity,
    string BaseUnitName,
    int QuantityReturnedInBaseUnits,
    string? FormattedQuantityReturned,
    string? PatientName,
    string? PatientPhone,
    string? DoctorName,
    string? PrescriptionNumber,
    DateOnly? PrescriptionDate,
    bool PrescriptionVerified,
    Guid CashierUserId,
    string? CashierName)
{
    /// <summary>Any detail captured, not all of them — a doctor's name alone still counts.</summary>
    public bool HasPrescription =>
        PatientName is not null || DoctorName is not null || PrescriptionNumber is not null;

    public bool HasReturns => QuantityReturnedInBaseUnits > 0;

    /// <summary>
    /// Whether this row should carry a red flag.
    ///
    /// <para><b>Only under Required.</b> Under Off nothing was captured, by design, and under
    /// Optional it was captured when somebody had it — flagging either would be crying wolf on
    /// every row, and the first thing a person learns from that is to ignore the indicator. Under
    /// Required a missing or unverified prescription means Module 5's enforcement let something
    /// through, which is worth shouting about.</para>
    ///
    /// <para>A row from an earlier Off or Optional period will light up once a pharmacy switches
    /// to Required. That is expected historical data rather than a current bug — the register
    /// page says so, and so does the module doc.</para>
    /// </summary>
    public bool IsFlagged(AntibioticPrescriptionMode mode) =>
        mode == AntibioticPrescriptionMode.Required
        && (!HasPrescription || !PrescriptionVerified);
}

public sealed record AntibioticRegisterSummary(
    int RowCount,
    int TotalDispensedInBaseUnits,
    string UnitLabel,
    AntibioticPrescriptionMode Mode);

public sealed record AntibioticProductOption(Guid ProductId, string BrandName);

public sealed record AntibioticFilterOptions(
    IReadOnlyList<AntibioticProductOption> Products,
    IReadOnlyList<CashierOption> Cashiers);

/// <summary>Everything one register request returns: the page, the range total, the filters.</summary>
public sealed record AntibioticRegisterPage(
    DateOnly From,
    DateOnly To,
    ApiPage<AntibioticRegisterRow> Rows,
    AntibioticRegisterSummary Summary,
    AntibioticFilterOptions Options)
{
    public static AntibioticRegisterPage Empty { get; } = new(
        default,
        default,
        ApiPage<AntibioticRegisterRow>.Empty,
        new AntibioticRegisterSummary(0, 0, "units", AntibioticPrescriptionMode.Off),
        new AntibioticFilterOptions([], []));
}

public sealed record AntibioticMonthlySummary(
    int Month,
    int Year,
    int TotalDispensedInBaseUnits,
    string UnitLabel,
    int SaleLineCount)
{
    public static AntibioticMonthlySummary Empty { get; } = new(0, 0, 0, "units", 0);
}

public sealed record AntibioticMode(AntibioticPrescriptionMode Mode)
{
    /// <summary>
    /// The safe default when the call fails: the loosest mode.
    ///
    /// <para>Deliberate. If the lookup fails at the till, the wrong answer to fall back on is
    /// Required — that would block an Employee from selling because a request failed, which is a
    /// worse outcome than not showing a prescription panel.</para>
    /// </summary>
    public static AntibioticMode Default { get; } = new(AntibioticPrescriptionMode.Off);
}

public sealed record SetAntibioticModePayload(AntibioticPrescriptionMode Mode);

/// <summary>A streamed file on its way from the API to the browser.</summary>
public sealed record ApiFile(Stream Content, string ContentType, string FileName);
