namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 2 wire contracts, declared locally like everything else in this project.
//
// Enum values must match the server's NUMERIC values — the API serialises enums as integers,
// which is the convention Module 1 established and which every client here relies on.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// What kind of thing a product is.
///
/// Medicine is one type among several, not the only one: pharmacies here also sell saline,
/// syringes, diapers, formula, handwash and supplements, and a system that only understands
/// medicines gets diapers entered as medicines with invented strengths.
/// </summary>
public enum ProductType
{
    Medicine = 0,
    MedicalSupply = 1,
    BabyCare = 2,
    PersonalCare = 3,
    Supplement = 4,
    Other = 5,
}

/// <summary>Which of the two list screens a request is for. Both read the same table.</summary>
public enum ProductListType
{
    Medicine = 0,
    Other = 1,
}

public enum ProductStatusFilter
{
    Active = 0,
    Inactive = 1,
    All = 2,

    /// <summary>Active products still missing a price for one of their unit levels.</summary>
    SetupIncomplete = 3,
}

/// <summary>How a set of catalogue results was arrived at. Drives the heading, not just a hint.</summary>
public enum CatalogMatchType
{
    /// <summary>Direct indexed match. Show plainly.</summary>
    Exact = 0,

    /// <summary>Fuzzy fallback — must be labelled "did you mean", never presented as a match.</summary>
    Suggestion = 1,
}

/// <summary>
/// The API's paged envelope (<c>GridResult&lt;T&gt;</c> server-side).
///
/// Unlike Module 1's lists, these endpoints really do page in the database, so this carries
/// the server's own totals rather than slicing locally.
/// </summary>
public sealed record ApiPage<T>(
    IReadOnlyList<T> Data,
    int Total,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage)
{
    public static ApiPage<T> Empty { get; } = new([], 0, 1, 25, 1, false, false);

    public bool IsEmpty => Total == 0;

    /// <summary>1-based index of the first row on this page, or 0 when there are none.</summary>
    public int FirstRow => Total == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastRow => Math.Min(Page * PageSize, Total);
}

/// <param name="PricePerBase">
/// Null for an Employee — the API's projection never reads it for that role, so no price
/// crosses the wire. The absent value is the control; hiding a column would not be.
/// </param>
public sealed record ProductListItem(
    Guid Id,
    ProductType ProductType,
    string BrandName,
    string? GenericName,
    string? Company,
    string? Strength,
    string? DosageForm,
    string? Category,
    bool IsAntibiotic,
    bool IsActive,
    string BaseUnitName,
    decimal? PricePerBase,
    bool ImportedFromCatalog,
    bool IsSetupComplete);

public sealed record ProductModel(
    Guid Id,
    ProductType ProductType,
    string BrandName,
    string? GenericName,
    string? Company,
    string? Strength,
    string? DosageForm,
    string? Category,
    bool IsAntibiotic,
    bool IsActive,
    int? CatalogMedicineId,
    string BaseUnitName,
    string? MidUnitName,
    string? LargeUnitName,
    int? BasePerMid,
    int? MidPerLarge,
    int? BaseUnitsPerLarge,
    string PackingSummary,

    // Nullable, and it must stay nullable here. Declared as decimal, a missing price would
    // deserialise to 0 and render as a product that sells for nothing - the exact sentinel
    // confusion the nullable column exists to prevent, reappearing on the client.
    decimal? PricePerBase,
    decimal? PricePerMid,
    decimal? PricePerLarge,
    int ReorderLevel,
    string? ShelfLocation,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    bool ImportedFromCatalog,
    bool IsSetupComplete);

/// <param name="AlreadyImported">
/// True when this pharmacy already has a product from this catalogue row. Flagged rather than
/// hidden: someone searching for Napa and finding nothing would conclude the catalogue lacks
/// it, when in fact their own earlier import succeeded.
/// </param>
public sealed record CatalogMedicineSearchItem(
    int Id,
    string BrandName,
    string? GenericName,
    string? Manufacturer,
    string? Strength,
    string? DosageForm,
    bool IsAntibiotic,
    decimal? SourceUnitPrice,
    bool AlreadyImported,
    Guid? ExistingProductId,
    int Score);

/// <param name="Total">
/// Matches found up to the search ceiling, not the size of the catalogue. The page says so:
/// stage 2 of the search scores candidates in memory and cannot be offset, so results are
/// gathered to a cap and paged from there.
/// </param>
public sealed record CatalogSearchResult(
    CatalogMatchType MatchType,
    string Term,
    IReadOnlyList<CatalogMedicineSearchItem> Items,
    int Total,
    int Page,
    int PageSize,
    bool Capped)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 1;

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public int FirstRow => Total == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastRow => Math.Min(Page * PageSize, Total);
}

// --- requests ---

public sealed record CreateProductRequest(
    ProductType ProductType,
    string BrandName,
    string? Company,
    string? Category,
    string? GenericName,
    string? Strength,
    string? DosageForm,
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
    string? ShelfLocation,
    int? CatalogMedicineId);

/// <summary>
/// The edit body. No <c>CatalogMedicineId</c>: the link records where a product came from, and
/// an edit does not rewrite that history.
/// </summary>
public sealed record UpdateProductRequest(
    ProductType ProductType,
    string BrandName,
    string? Company,
    string? Category,
    string? GenericName,
    string? Strength,
    string? DosageForm,
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
    string? ShelfLocation);
