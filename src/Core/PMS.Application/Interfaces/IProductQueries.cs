using PMS.Application.Common.DTOs;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Interfaces;

/// <summary>
/// Reads over the current pharmacy's product catalogue.
///
/// <para>No method takes a tenant id, and none may. <c>Product</c> implements
/// <c>ITenantEntity</c>, so the global query filter supplies the pharmacy; a parameter here
/// would be an opportunity to pass the wrong one.</para>
/// </summary>
public interface IProductQueries
{
    Task<GridResult<ProductListItemDto>> ListAsync(
        ProductListType listType,
        string? search,
        ProductStatusFilter status,
        bool antibioticOnly,
        ProductType? productType,
        bool includePrices,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ProductDto?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this pharmacy already has a product with that brand and strength.
    ///
    /// <paramref name="excludingId"/> lets an edit re-save itself without colliding with its
    /// own row.
    /// </summary>
    Task<bool> ExistsWithBrandAndStrengthAsync(
        string brandName,
        string? strength,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The two-stage search over the platform medicine catalogue.
///
/// <para>The catalogue is shared platform data with no tenant, but these reads also need to
/// know which entries the <em>current</em> pharmacy has already imported — so they join a
/// tenant-scoped table to an unfiltered one. That is expected: a foreign key across the
/// tenant/platform boundary is correct, and it is not a reason to filter the catalogue.</para>
/// </summary>
public interface ICatalogSearchQueries
{
    /// <summary>
    /// Stage 1: indexed match on brand name, then generic name. Prefix first so the index is
    /// seekable, falling back to contains only if prefix finds nothing.
    /// </summary>
    Task<IReadOnlyList<CatalogMedicineSearchItemDto>> DirectMatchAsync(
        string term, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stage 2: a <b>bounded</b> candidate set for fuzzy scoring in the application layer.
    ///
    /// <para>Bounded is the whole point. The catalogue holds 21,714 rows and loading it into
    /// memory on every typo would be both slow and a memory spike on a shared server. The
    /// implementation narrows by first characters and name length before it reads anything.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<CatalogMedicineSearchItemDto>> FuzzyCandidatesAsync(
        string term, int maxCandidates, CancellationToken cancellationToken = default);

    /// <summary>One catalogue entry, for pre-filling the import form.</summary>
    Task<CatalogMedicineSearchItemDto?> FindAsync(
        int catalogMedicineId, CancellationToken cancellationToken = default);
}
