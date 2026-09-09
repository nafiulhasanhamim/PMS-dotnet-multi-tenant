using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// One row of a product list.
///
/// <para><b>Prices are nullable because they are withheld from Employees.</b> The list
/// projection simply does not read them for that role, so a price an Employee may not see
/// never leaves the server. Razor omits the columns too, but that is cosmetic — the
/// guarantee is that the data is absent, not hidden.</para>
/// </summary>
public sealed record ProductListItemDto(
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
    bool ImportedFromCatalog);

/// <summary>A product in full, for the detail page. Prices are visible to every role here.</summary>
public sealed record ProductDto(
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
    decimal PricePerBase,
    decimal? PricePerMid,
    decimal? PricePerLarge,
    int ReorderLevel,
    string? ShelfLocation,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc)
{
    public bool ImportedFromCatalog => CatalogMedicineId is not null;
}

/// <summary>Which broad list a request is for. The two screens over one table.</summary>
public enum ProductListType
{
    /// <summary>ProductType == Medicine.</summary>
    Medicine = 0,

    /// <summary>Everything that is not a medicine.</summary>
    Other = 1,
}

public enum ProductStatusFilter
{
    Active = 0,
    Inactive = 1,
    All = 2,
}

/// <summary>
/// A hit from the platform medicine catalogue, for the import screen.
/// </summary>
/// <param name="AlreadyImported">
/// True when this pharmacy already has a product linked to this catalogue row. The entry is
/// still returned — flagged rather than hidden — so the person searching sees that their
/// earlier import worked instead of wondering why Napa vanished from the catalogue.
/// </param>
public sealed record CatalogMedicineSearchItemDto(
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

/// <summary>How a set of catalogue results was arrived at.</summary>
public enum CatalogMatchType
{
    /// <summary>Direct indexed match on brand or generic. Show these plainly.</summary>
    Exact = 0,

    /// <summary>
    /// Fuzzy fallback. The UI must label these as approximate — "Did you mean:" — because
    /// presenting a guess as a match is how someone imports the wrong medicine.
    /// </summary>
    Suggestion = 1,
}

/// <param name="MatchType">
/// Part of the API contract, not a hint: the UI changes its heading based on it.
/// </param>
public sealed record CatalogSearchResultDto(
    CatalogMatchType MatchType,
    string Term,
    IReadOnlyList<CatalogMedicineSearchItemDto> Items);
