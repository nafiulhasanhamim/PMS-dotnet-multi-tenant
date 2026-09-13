using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.BulkImportProducts;

/// <summary>
/// Imports many catalogue medicines into this pharmacy's own catalogue at once.
///
/// <para>Onboarding is the reason this exists. A pharmacy joining the platform has to enter
/// two hundred or more products before the system is usable at all, and doing that one form at
/// a time is the difference between an afternoon and a fortnight.</para>
/// </summary>
public sealed record BulkImportProductsCommand(IReadOnlyList<BulkImportItem> Items)
    : IRequest<Result<BulkImportResultDto>>, ITenantScopedRequest;

/// <summary>
/// One row of a bulk import.
/// </summary>
/// <param name="CatalogMedicineId">
/// Where the brand name, generic name, strength and dosage form come from. <b>Not sent by the
/// client</b> — the handler reads them from the catalogue row, so a caller cannot import a
/// product under one catalogue id with another medicine's details.
/// </param>
/// <param name="IsAntibiotic">
/// Sent, and deliberately not taken from the catalogue. The catalogue's flag is machine-derived
/// and provisional; this is a pharmacist confirming or correcting it, which is the moment a
/// guess becomes a decision. The bulk screen pre-ticks it from the catalogue and tints those
/// rows so the eye is drawn to exactly the classifications that need a human.
/// </param>
/// <param name="PricePerBase">
/// Optional, unlike the single-product form. Omitting prices creates the product with
/// <c>IsSetupComplete = false</c>, which is a deliberate offer rather than a validation
/// failure — see the docs.
/// </param>
public sealed record BulkImportItem(
    int CatalogMedicineId,
    bool IsAntibiotic,
    string BaseUnitName,
    string? MidUnitName,
    string? LargeUnitName,
    int? BasePerMid,
    int? MidPerLarge,
    decimal? PricePerBase,
    decimal? PricePerMid,
    decimal? PricePerLarge,
    int ReorderLevel,
    string? Category,
    string? ShelfLocation);
