using PMS.Application.Common.Settings;
using PMS.Domain.Enums;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Antibiotics;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Features.Antibiotics.Queries.GetMonthlySummary;
using PMS.Application.Features.Antibiotics.Queries.GetRegister;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using PMS.WebApi.Csv;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// The antibiotic sales register: what antibiotics this pharmacy dispensed, to whom, against
/// whose prescription.
///
/// <para><b>Admin and Pharmacist only, and the asymmetry with Module 5 is deliberate.</b> An
/// Employee may well be able to <em>sell</em> an antibiotic — that now depends on the pharmacy's
/// prescription mode — but never to read the register. Selling is counter work; the register is
/// the regulatory record of it, and reading back what colleagues dispensed, to which named
/// patients, is oversight. The two questions have different answers and the policies say so.</para>
///
/// <para>Every action is tenant-scoped and none mentions a tenant: the queries carry
/// <c>ITenantScopedRequest</c> and the global query filter supplies the pharmacy.</para>
/// </summary>
[Route("api/antibiotics")]
[Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
public class AntibioticsController : ApiControllerBase
{
    private readonly IAntibioticQueries _antibiotics;
    private readonly ISettingsService _settings;
    private readonly IDateTime _clock;

    public AntibioticsController(
        IAntibioticQueries antibiotics,
        ISettingsService settings,
        IDateTime clock)
    {
        _antibiotics = antibiotics;
        _settings = settings;
        _clock = clock;
    }

    /// <summary>
    /// One page of the register, with the summary over the whole filtered range and the filter
    /// dropdowns.
    ///
    /// <para>Dates default to the current month. One row per antibiotic sale line, so an invoice
    /// dispensing two different antibiotics appears twice — each quantity attributable to its own
    /// product, which a collapsed row could not manage.</para>
    ///
    /// <para>Cancelled sales are absent entirely. A partially returned sale stays, with its
    /// returned quantity, because it happened.</para>
    /// </summary>
    [HttpGet("register")]
    [ProducesResponseType(typeof(AntibioticRegisterPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegister(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] string? doctorName = null,
        [FromQuery] Guid? cashierUserId = null,
        [FromQuery] PrescriptionStatusFilter prescriptionStatus = PrescriptionStatusFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlertPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetRegisterQuery(
                dateFrom, dateTo, productId, doctorName, cashierUserId,
                prescriptionStatus, page, pageSize),
            cancellationToken));

    /// <summary>
    /// The whole filtered set as CSV. <b>Not one page</b> — an export somebody hands to an
    /// inspector that quietly stopped at row 25 would be worse than no export.
    ///
    /// <para><b>Streamed.</b> Rows are written to the response as the database produces them
    /// rather than gathered into a list first: a busy pharmacy accumulates thousands of
    /// antibiotic lines a year, and an export is precisely the request that asks for all of
    /// them.</para>
    ///
    /// <para>The file opens with a header block naming the pharmacy, the range, the prescription
    /// mode and when it was taken, so a CSV that has been emailed on is still self-describing —
    /// and so that empty prescription columns have an explanation attached to them.</para>
    /// </summary>
    [HttpGet("register/export")]
    [Produces("text/csv")]
    public async Task ExportRegister(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] string? doctorName = null,
        [FromQuery] Guid? cashierUserId = null,
        [FromQuery] PrescriptionStatusFilter prescriptionStatus = PrescriptionStatusFilter.All,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = RegisterRange.Resolve(dateFrom, dateTo, _clock.UtcDateToday());
        var mode = await _settings.GetEnumAsync<AntibioticPrescriptionMode>(
            SettingKeys.AntibioticPrescriptionMode, cancellationToken);
        var pharmacy = await _settings.GetStringAsync(SettingKeys.PharmacyName, cancellationToken);

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition =
            $"attachment; filename=\"antibiotic-register-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.csv\"";

        // Written straight to the body. The alternative — building a string or a MemoryStream and
        // returning a FileResult — would buffer a year of rows in memory to hand them back
        // unchanged, which is the one thing the streaming requirement is about.
        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(true));

        await writer.WriteLineAsync($"Antibiotic sales register");
        await writer.WriteLineAsync($"Pharmacy,{CsvField.Text(pharmacy)}");
        await writer.WriteLineAsync($"Date range,{from:yyyy-MM-dd} to {to:yyyy-MM-dd}");
        await writer.WriteLineAsync($"Prescription capture,{mode}");
        await writer.WriteLineAsync(
            $"Exported,{_clock.UtcNow:yyyy-MM-dd HH:mm} UTC");
        await writer.WriteLineAsync();

        await writer.WriteLineAsync(
            "Date,Invoice,Product,Generic,Quantity,Quantity returned,"
            + "Patient name,Patient phone,Doctor,Prescription number,Prescription date,"
            + "Prescription verified,Sold by");

        var rows = _antibiotics.StreamRegisterAsync(
            from, to, productId, doctorName, cashierUserId, prescriptionStatus,
            cancellationToken);

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(string.Join(',', new[]
            {
                row.SaleDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                CsvField.Text(row.InvoiceNumber),
                CsvField.Text(row.BrandName),
                CsvField.Text(row.GenericName),
                CsvField.Text(row.FormattedQuantity),
                CsvField.Text(row.HasReturns ? row.FormattedQuantityReturned : null),
                CsvField.Text(row.PatientName),
                CsvField.Text(row.PatientPhone),
                CsvField.Text(row.DoctorName),
                CsvField.Text(row.PrescriptionNumber),
                row.PrescriptionDate?.ToString("yyyy-MM-dd") ?? string.Empty,

                // Only meaningful under Required. Under Off nothing was captured and under
                // Optional it was captured if somebody had it, so "No" there is not a finding.
                row.HasPrescription ? (row.PrescriptionVerified ? "Yes" : "No") : string.Empty,
                CsvField.Text(row.CashierName),
            }));
        }

        await writer.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Total antibiotic quantity dispensed in a month, net of returns and excluding cancelled
    /// sales. The dashboard card.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AntibioticMonthlySummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlySummary(
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetMonthlySummaryQuery(month, year), cancellationToken));
}
