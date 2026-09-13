using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// One antibiotic line item, as the register lists it.
///
/// <para><b>One row per sale line, not per sale.</b> A single invoice can dispense two different
/// antibiotics, and a register that collapsed them would make neither quantity attributable to
/// its product. Two antibiotic lines on one invoice produce two rows sharing an invoice
/// number.</para>
/// </summary>
/// <param name="QuantityReturnedInBaseUnits">
/// Zero for almost every row. A partially returned sale stays in the register — it happened, and
/// removing it would be rewriting the record — with the returned quantity shown so the net
/// dispensed is visible.
/// </param>
/// <param name="PrescriptionVerified">
/// Only meaningful under Required mode. Under Off nothing was captured and under Optional it was
/// captured if somebody had it, so an unticked box there says nothing about compliance.
/// </param>
public sealed record AntibioticRegisterRowDto(
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
    /// <summary>
    /// Whether any prescription detail was captured for the sale this line belongs to.
    ///
    /// <para>Any, not all. A doctor's name and nothing else is still a record, and under Optional
    /// that is a normal and useful outcome.</para>
    /// </summary>
    public bool HasPrescription =>
        PatientName is not null || DoctorName is not null || PrescriptionNumber is not null;

    public bool HasReturns => QuantityReturnedInBaseUnits > 0;

    /// <summary>What was actually dispensed once returns are taken off.</summary>
    public int NetQuantityInBaseUnits =>
        QuantityInBaseUnits - QuantityReturnedInBaseUnits;
}

/// <summary>Prescription filter on the register. Most useful under Optional mode.</summary>
public enum PrescriptionStatusFilter
{
    All = 0,
    WithPrescription = 1,
    WithoutPrescription = 2,
}

/// <summary>
/// What the register page needs alongside the rows.
/// </summary>
/// <param name="TotalDispensedInBaseUnits">
/// Across the whole filtered set, not just the page, and net of returns — the summary line is a
/// statement about the range, and a figure that changed as somebody paged would be worthless.
/// </param>
/// <param name="UnitLabel">
/// The base unit the total is counted in, when every row shares one — "pieces". Otherwise
/// "units".
///
/// <para><b>Worth being careful about.</b> Summing base units across products is only meaningful
/// when those units are the same thing. Twelve tablets plus three bottles is not fifteen of
/// anything, so when the filtered set mixes base units the label says so rather than inventing a
/// unit that would make the number look more precise than it is.</para>
/// </param>
public sealed record AntibioticRegisterSummaryDto(
    int RowCount,
    int TotalDispensedInBaseUnits,
    string UnitLabel,
    AntibioticPrescriptionMode Mode);

/// <summary>The monthly figure behind the dashboard card.</summary>
/// <param name="TotalDispensedInBaseUnits">Net of returns, excluding cancelled sales.</param>
public sealed record AntibioticMonthlySummaryDto(
    int Month,
    int Year,
    int TotalDispensedInBaseUnits,
    string UnitLabel,
    int SaleLineCount);

/// <summary>The pharmacy's current antibiotic prescription mode.</summary>
public sealed record AntibioticModeDto(AntibioticPrescriptionMode Mode);

/// <summary>
/// Everything one request to the register returns: the page, the summary over the whole filtered
/// range, and the filter options.
///
/// <para>One response rather than three calls, because all three change together. A page that
/// fetched its rows and its total separately could render a total that disagreed with the table
/// beneath it — and on a document somebody may hand to an inspector, a header contradicting the
/// rows is worse than no header.</para>
/// </summary>
/// <param name="From">The range actually used, after defaults were filled in and any reversed
/// pair was swapped. Echoed back so the page and the printed header cannot claim a different
/// one.</param>
public sealed record AntibioticRegisterPageDto(
    DateOnly From,
    DateOnly To,
    SharedKernel.Grid.GridResult<AntibioticRegisterRowDto> Rows,
    AntibioticRegisterSummaryDto Summary,
    AntibioticFilterOptionsDto Options);

/// <summary>One antibiotic product, for the register's filter dropdown.</summary>
public sealed record AntibioticProductOptionDto(Guid ProductId, string BrandName);

/// <summary>
/// Everything the register page's filter bar needs, in one call: the antibiotic products this
/// pharmacy stocks and the cashiers who have dispensed one.
///
/// <para>Both drawn from the pharmacy's own data rather than from the full catalogue or the staff
/// list — a filter offering products nobody stocks or people who have never sold an antibiotic is
/// a list of dead ends.</para>
/// </summary>
public sealed record AntibioticFilterOptionsDto(
    IReadOnlyList<AntibioticProductOptionDto> Products,
    IReadOnlyList<CashierOptionDto> Cashiers);
