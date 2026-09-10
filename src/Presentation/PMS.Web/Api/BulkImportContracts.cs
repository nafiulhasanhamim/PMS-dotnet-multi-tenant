namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Bulk import and bulk price setting.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <param name="Succeeded">
/// True only when every row was created. There is no partial success — see the backend docs
/// for why a half-imported catalogue is the outcome worth ruling out.
/// </param>
/// <param name="Items">
/// One result per submitted row, in submission order, so the review grid can line results up
/// against the rows on screen without matching on anything.
/// </param>
public sealed record BulkImportResult(
    bool Succeeded,
    int Created,
    int Failed,
    IReadOnlyList<BulkImportItemResult> Items)
{
    /// <summary>
    /// The rows to fix, for the error summary and the in-place markers.
    ///
    /// <para>Keyed on having errors, <b>not</b> on <c>Succeeded</c>. Those differ on exactly
    /// the case that matters: when any row fails, nothing is written, so every row reports
    /// <c>Succeeded = false</c> — including the ones that were perfectly fine. Marking all of
    /// them would tell somebody to fix rows that need no fixing.</para>
    /// </summary>
    public IReadOnlyList<BulkImportItemResult> Failures =>
        Items.Where(item => item.Errors.Count > 0).ToList();
}

/// <param name="Row">
/// Zero-based position in the submitted list — the only identifier a failed row is guaranteed
/// to have, since a row rejected for a bad catalogue id has no name and no product.
/// </param>
/// <param name="Succeeded">
/// Whether this row produced a product. False for every row when the batch was refused, which
/// is why the grid marks rows by <see cref="Errors"/> instead.
/// </param>
public sealed record BulkImportItemResult(
    int Row,
    int CatalogMedicineId,
    string? BrandName,
    bool Succeeded,
    Guid? ProductId,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool HasErrors => Errors.Count > 0;

    /// <summary>Every message for this row, flattened for display.</summary>
    public IReadOnlyList<string> Messages =>
        Errors.SelectMany(entry => entry.Value).ToList();
}

public sealed record BulkImportRequest(IReadOnlyList<BulkImportItemRequest> Items);

/// <param name="IsAntibiotic">
/// Sent rather than taken from the catalogue: the catalogue's flag is machine-derived and
/// provisional, and this is a pharmacist confirming it.
/// </param>
/// <param name="PricePerBase">
/// Optional. Omitting prices creates the product with setup incomplete, which is what the
/// "Save without prices" button does.
/// </param>
public sealed record BulkImportItemRequest(
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

public sealed record SetPricesRequest(IReadOnlyList<ProductPriceUpdateRequest> Items);

public sealed record ProductPriceUpdateRequest(
    Guid ProductId,
    decimal PricePerBase,
    decimal? PricePerMid,
    decimal? PricePerLarge);
