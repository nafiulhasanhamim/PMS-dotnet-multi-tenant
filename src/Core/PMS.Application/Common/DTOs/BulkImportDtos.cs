namespace PMS.Application.Common.DTOs;

/// <summary>
/// The outcome of a bulk import.
/// </summary>
/// <param name="Succeeded">
/// True only when every item was created. There is no partial success: see
/// <c>BulkImportProductsCommandHandler</c> for why a half-imported catalogue is the one
/// outcome worth ruling out.
/// </param>
/// <param name="Items">
/// One result per submitted row, <b>in the order submitted</b>, so a client can line the
/// results up against the rows on screen without matching on anything. Present on failure as
/// well as success — that is the point of it.
/// </param>
public sealed record BulkImportResultDto(
    bool Succeeded,
    int Created,
    int Failed,
    IReadOnlyList<BulkImportItemResultDto> Items);

/// <param name="Row">
/// Zero-based position in the submitted list. The only identifier a client can rely on: a row
/// that failed validation may have no catalogue entry, no brand name and certainly no product
/// id, and "row 14" is what the person is looking at.
/// </param>
/// <param name="ProductId">
/// Set only when the whole import succeeded. On failure every id is null, including for the
/// rows that were individually fine — nothing was written.
/// </param>
/// <param name="Errors">
/// Field name to messages, matching the shape the single-product endpoints return, so a form
/// can attach them to inputs the same way.
/// </param>
public sealed record BulkImportItemResultDto(
    int Row,
    int CatalogMedicineId,
    string? BrandName,
    bool Succeeded,
    Guid? ProductId,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool HasErrors => Errors.Count > 0;
}
