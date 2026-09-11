using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Units;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Grid;

namespace PMS.Persistence.Services;

/// <summary>
/// The antibiotic register: reads over sales that already happened.
///
/// <para><b>There is no WHERE on TenantId anywhere in this file except on Users, and that is
/// correct.</b> <c>Sale</c>, <c>SaleLine</c>, <c>Product</c> and <c>SalesReturn</c> are all
/// <c>ITenantEntity</c>, so the global query filter supplies the pharmacy. <c>Users</c> is the
/// global identity table — one person can work at two pharmacies — so reaching into it is an
/// explicit join, written out rather than buried in a navigation.</para>
///
/// <para><b>Two rules run through every query here.</b> A cancelled sale is excluded entirely,
/// never shown struck through — it dispensed nothing, and Module 8 will exclude them the same
/// way. And a partially returned sale stays, with its returned quantity shown: it happened, and
/// deleting the row would be rewriting the record rather than correcting it.</para>
/// </summary>
public sealed class AntibioticQueries : IAntibioticQueries
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantSettings _settings;

    public AntibioticQueries(ApplicationDbContext context, ITenantSettings settings)
    {
        _context = context;
        _settings = settings;
    }

    /// <summary>
    /// Every antibiotic line in a completed sale inside the range, before paging.
    ///
    /// <para>Composed once and used by the page, the summary and the export, so the three cannot
    /// disagree about what is in the register. A CSV that covered a different set from the table
    /// it was exported from would be the worst possible failure on a document handed to an
    /// inspector.</para>
    /// </summary>
    private IQueryable<SaleLine> Filtered(
        DateOnly from,
        DateOnly to,
        Guid? productId,
        string? doctorName,
        Guid? cashierUserId,
        PrescriptionStatusFilter prescriptionStatus)
    {
        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Inclusive of the whole end day. A range of "1 to 31 August" that stopped at midnight on
        // the 31st would silently omit every sale made on the 31st.
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var query = _context.SaleLines
            .AsNoTracking()
            .Where(line => line.Product.IsAntibiotic)

            // Completed only. A cancelled sale restored its stock and reversed its payment; it
            // dispensed nothing, so it does not belong in a dispensing register.
            .Where(line => line.Sale.Status == SaleStatus.Completed)
            .Where(line => line.Sale.SaleDate >= start && line.Sale.SaleDate < end);

        if (productId is { } product)
        {
            query = query.Where(line => line.ProductId == product);
        }

        if (!string.IsNullOrWhiteSpace(doctorName))
        {
            // Partial, because a register is searched by half-remembered names — "Karim" should
            // find "Dr A. Karim Chowdhury".
            var term = doctorName.Trim();
            query = query.Where(line =>
                line.Sale.DoctorName != null
                && EF.Functions.Like(line.Sale.DoctorName, $"%{term}%"));
        }

        if (cashierUserId is { } cashier)
        {
            query = query.Where(line => line.Sale.CashierUserId == cashier);
        }

        // "Has a prescription" means any detail was captured, not all of them. Under Optional a
        // doctor's name alone is a real record, and a filter that demanded the full set would
        // report a pharmacy as capturing nothing when it is capturing something.
        query = prescriptionStatus switch
        {
            PrescriptionStatusFilter.WithPrescription => query.Where(line =>
                line.Sale.PatientName != null
                || line.Sale.DoctorName != null
                || line.Sale.PrescriptionNumber != null),

            PrescriptionStatusFilter.WithoutPrescription => query.Where(line =>
                line.Sale.PatientName == null
                && line.Sale.DoctorName == null
                && line.Sale.PrescriptionNumber == null),

            _ => query,
        };

        return query;
    }

    /// <inheritdoc />
    public async Task<GridResult<AntibioticRegisterRowDto>> GetRegisterAsync(
        DateOnly from,
        DateOnly to,
        Guid? productId,
        string? doctorName,
        Guid? cashierUserId,
        PrescriptionStatusFilter prescriptionStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = Filtered(from, to, productId, doctorName, cashierUserId, prescriptionStatus);

        var total = await query.CountAsync(cancellationToken);

        var rows = await Project(query)
            .OrderByDescending(row => row.SaleDate)

            // A stable tiebreaker. Several lines can share a timestamp — two antibiotics on one
            // invoice share it exactly — and without this the order between them is whatever the
            // query plan produced, which is stable enough to pass a test once and to shuffle
            // rows between pages in production.
            .ThenBy(row => row.InvoiceNumber)
            .ThenBy(row => row.BrandName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return GridResult<AntibioticRegisterRowDto>.Create(
            rows.Select(ToDto).ToList(), total, page, pageSize);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AntibioticRegisterRowDto> StreamRegisterAsync(
        DateOnly from,
        DateOnly to,
        Guid? productId,
        string? doctorName,
        Guid? cashierUserId,
        PrescriptionStatusFilter prescriptionStatus,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = Filtered(from, to, productId, doctorName, cashierUserId, prescriptionStatus);

        // AsAsyncEnumerable, not ToListAsync. A busy pharmacy accumulates thousands of antibiotic
        // lines a year and an export is exactly the request that asks for all of them; buffering
        // the year into a list to write it straight out again would hold the lot in memory for
        // no reason. Rows reach the response as the reader produces them.
        var stream = Project(query)
            .OrderByDescending(row => row.SaleDate)
            .ThenBy(row => row.InvoiceNumber)
            .ThenBy(row => row.BrandName)
            .AsAsyncEnumerable();

        await foreach (var row in stream.WithCancellation(cancellationToken))
        {
            yield return ToDto(row);
        }
    }

    /// <inheritdoc />
    public async Task<AntibioticRegisterSummaryDto> GetRegisterSummaryAsync(
        DateOnly from,
        DateOnly to,
        Guid? productId,
        string? doctorName,
        Guid? cashierUserId,
        PrescriptionStatusFilter prescriptionStatus,
        CancellationToken cancellationToken = default)
    {
        var query = Filtered(from, to, productId, doctorName, cashierUserId, prescriptionStatus);

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Rows = g.Count(),
                Dispensed = g.Sum(line => line.QuantityInBaseUnits),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var returned = await ReturnedTotalAsync(query, cancellationToken);
        var units = await DistinctBaseUnitsAsync(query, cancellationToken);
        var mode = await _settings.GetAntibioticModeAsync(cancellationToken);

        return new AntibioticRegisterSummaryDto(
            totals?.Rows ?? 0,

            // Returns come off the total. The summary says what was dispensed, and stock that
            // came back was not.
            (totals?.Dispensed ?? 0) - returned,
            UnitLabel(units),
            mode);
    }

    /// <inheritdoc />
    public async Task<AntibioticMonthlySummaryDto> GetMonthlySummaryAsync(
        int month,
        int year,
        CancellationToken cancellationToken = default)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        var query = Filtered(first, last, null, null, null, PrescriptionStatusFilter.All);

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Lines = g.Count(),
                Dispensed = g.Sum(line => line.QuantityInBaseUnits),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var returned = await ReturnedTotalAsync(query, cancellationToken);
        var units = await DistinctBaseUnitsAsync(query, cancellationToken);

        return new AntibioticMonthlySummaryDto(
            month,
            year,
            (totals?.Dispensed ?? 0) - returned,
            UnitLabel(units),
            totals?.Lines ?? 0);
    }

    /// <inheritdoc />
    public async Task<AntibioticFilterOptionsDto> GetFilterOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        // Products this pharmacy actually holds, not the whole reference catalogue. A dropdown
        // offering medicines nobody stocks is a list of dead ends.
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsAntibiotic)
            .OrderBy(p => p.BrandName)
            .Select(p => new AntibioticProductOptionDto(p.Id, p.BrandName))
            .ToListAsync(cancellationToken);

        // Cashiers drawn from the antibiotic sales themselves, for the same reason — and so that
        // somebody who has since left still appears, because their sales are still in the
        // register and an audit that could not filter to them would be missing the point.
        var cashierRows = await _context.SaleLines
            .AsNoTracking()
            .Where(line => line.Product.IsAntibiotic)
            .Where(line => line.Sale.Status == SaleStatus.Completed)
            .GroupBy(line => line.Sale.CashierUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var ids = cashierRows.Select(row => row.UserId).ToList();

        var names = await _context.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToListAsync(cancellationToken);

        var nameById = names.ToDictionary(row => row.Id, row => row.FullName);

        var cashiers = cashierRows
            .Select(row => new CashierOptionDto(
                row.UserId,
                nameById.TryGetValue(row.UserId, out var name) ? name : "(unknown user)",
                row.Count))
            .OrderBy(option => option.Name)
            .ToList();

        return new AntibioticFilterOptionsDto(products, cashiers);
    }

    /// <summary>
    /// The projection, shared by the page and the export so the two return identical rows.
    /// </summary>
    private IQueryable<RegisterRow> Project(IQueryable<SaleLine> query) =>
        query.Select(line => new RegisterRow
        {
            SaleId = line.SaleId,
            SaleLineId = line.Id,
            InvoiceNumber = line.Sale.InvoiceNumber,
            SaleDate = line.Sale.SaleDate,
            ProductId = line.ProductId,
            Product = line.Product,
            QuantityInBaseUnits = line.QuantityInBaseUnits,
            QuantityReturned = line.Returns.Sum(r => (int?)r.QuantityReturnedInBaseUnits) ?? 0,
            PatientName = line.Sale.PatientName,
            PatientPhone = line.Sale.PatientPhone,
            DoctorName = line.Sale.DoctorName,
            PrescriptionNumber = line.Sale.PrescriptionNumber,
            PrescriptionDate = line.Sale.PrescriptionDate,
            PrescriptionVerified = line.Sale.PrescriptionVerified,
            CashierUserId = line.Sale.CashierUserId,
            BrandName = line.Product.BrandName,

            // The global Users table, reached explicitly rather than through a navigation. Users
            // has no TenantId — one person can work at two pharmacies — so this is a
            // tenant-scoped row reaching into unfiltered data, and writing it out keeps the
            // crossing visible instead of burying it in the model.
            CashierName = _context.Users
                .Where(user => user.Id == line.Sale.CashierUserId)
                .Select(user => user.FullName)
                .FirstOrDefault(),
        });

    private static AntibioticRegisterRowDto ToDto(RegisterRow row) =>
        new(
            row.SaleId,
            row.SaleLineId,
            row.InvoiceNumber,
            row.SaleDate,
            row.ProductId,
            row.Product.BrandName,
            row.Product.GenericName,
            row.QuantityInBaseUnits,

            // Module 2's formatter, never a hardcoded unit word.
            UnitConversion.FromBaseUnits(row.QuantityInBaseUnits, row.Product),
            row.Product.BaseUnitName,
            row.QuantityReturned,
            row.QuantityReturned > 0
                ? UnitConversion.FromBaseUnits(row.QuantityReturned, row.Product)
                : null,
            row.PatientName,
            row.PatientPhone,
            row.DoctorName,
            row.PrescriptionNumber,
            row.PrescriptionDate,
            row.PrescriptionVerified,
            row.CashierUserId,
            row.CashierName);

    /// <summary>
    /// Everything returned against a filtered set of lines.
    ///
    /// <para><b>Its own statement, and not by choice.</b> Summing a per-line sum is the shape SQL
    /// Server refuses — "Cannot perform an aggregate function on an expression containing an
    /// aggregate or a subquery". <c>SelectMany</c> over the navigation flattens to a join first,
    /// so the outer SUM is over plain columns.</para>
    /// </summary>
    private static async Task<int> ReturnedTotalAsync(
        IQueryable<SaleLine> query, CancellationToken cancellationToken) =>
        await query
            .SelectMany(line => line.Returns)
            .SumAsync(r => (int?)r.QuantityReturnedInBaseUnits, cancellationToken) ?? 0;

    /// <summary>
    /// The distinct base units in a filtered set, capped — the label only needs to know whether
    /// there is exactly one.
    /// </summary>
    private static async Task<List<string>> DistinctBaseUnitsAsync(
        IQueryable<SaleLine> query, CancellationToken cancellationToken) =>
        await query
            .Select(line => line.Product.BaseUnitName)
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// What unit the summed total is counted in.
    ///
    /// <para><b>The honest answer matters more than the tidy one.</b> Summing base units across
    /// products is only meaningful when those units are the same thing: twelve tablets plus three
    /// bottles is not fifteen of anything. When the filtered set mixes base units the label says
    /// "units" rather than borrowing whichever name happened to come first, which would make the
    /// figure look more precise than it is.</para>
    /// </summary>
    private static string UnitLabel(IReadOnlyList<string> distinctBaseUnits) =>
        distinctBaseUnits.Count == 1 ? Pluralise(distinctBaseUnits[0]) : "units";

    private static string Pluralise(string noun)
    {
        var lower = noun.ToLowerInvariant();

        if (lower.EndsWith('s') || lower.EndsWith('x') || lower.EndsWith('z')
            || lower.EndsWith("ch") || lower.EndsWith("sh"))
        {
            return noun + "es";
        }

        if (lower.Length > 1 && lower.EndsWith('y') && !"aeiou".Contains(lower[^2]))
        {
            return noun[..^1] + "ies";
        }

        return noun + "s";
    }

    /// <summary>One register row as EF projects it, before formatting.</summary>
    private sealed class RegisterRow
    {
        public Guid SaleId { get; init; }

        public Guid SaleLineId { get; init; }

        public string InvoiceNumber { get; init; } = null!;

        public DateTime SaleDate { get; init; }

        public Guid ProductId { get; init; }

        public Product Product { get; init; } = null!;

        public string BrandName { get; init; } = null!;

        public int QuantityInBaseUnits { get; init; }

        public int QuantityReturned { get; init; }

        public string? PatientName { get; init; }

        public string? PatientPhone { get; init; }

        public string? DoctorName { get; init; }

        public string? PrescriptionNumber { get; init; }

        public DateOnly? PrescriptionDate { get; init; }

        public bool PrescriptionVerified { get; init; }

        public Guid CashierUserId { get; init; }

        public string? CashierName { get; set; }
    }
}
