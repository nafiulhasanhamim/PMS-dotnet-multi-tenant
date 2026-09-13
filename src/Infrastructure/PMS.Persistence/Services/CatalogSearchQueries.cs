using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace PMS.Persistence.Services;

/// <summary>
/// The database half of the two-stage catalogue search.
///
/// <para><b>Two different filtering regimes in one query, on purpose.</b> The catalogue tables
/// are platform-level and unfiltered, so these reads see all 21,714 medicines regardless of
/// which pharmacy is asking. The <c>Products</c> table they join to for the
/// "already imported" flag <em>is</em> tenant-filtered, so that half of the query is
/// automatically restricted to the caller's pharmacy. A foreign key across the tenant /
/// platform boundary is expected here, and it is not a reason to filter the catalogue.</para>
/// </summary>
public sealed class CatalogSearchQueries : ICatalogSearchQueries
{
    private readonly ApplicationDbContext _context;

    public CatalogSearchQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CatalogMedicineSearchItemDto>> DirectMatchAsync(
        string term, int take, CancellationToken cancellationToken = default)
    {
        var value = term.Trim();

        // Prefix first: LIKE 'nap%' is a seekable range on IX_CatalogMedicines_BrandName.
        // A leading wildcard cannot use the index at all and scans every row, so it is only
        // worth paying for when the prefix finds nothing.
        var byPrefix = await Project(
                _context.CatalogMedicines
                    .Where(m => m.IsActive && EF.Functions.Like(m.BrandName, $"{value}%"))
                    .OrderBy(m => m.BrandName)
                    .Take(take))
            .ToListAsync(cancellationToken);

        if (byPrefix.Count > 0)
        {
            return await WithImportFlags(byPrefix, cancellationToken);
        }

        // Generic prefix next — someone typing "paracetamol" wants every brand of it.
        var byGenericPrefix = await Project(
                _context.CatalogMedicines
                    .Where(m => m.IsActive && m.Generic != null
                                && EF.Functions.Like(m.Generic.Name, $"{value}%"))
                    .OrderBy(m => m.BrandName)
                    .Take(take))
            .ToListAsync(cancellationToken);

        if (byGenericPrefix.Count > 0)
        {
            return await WithImportFlags(byGenericPrefix, cancellationToken);
        }

        // Only now pay for the scan. A word in the middle of a brand name is a real search
        // ("handwash", "infant formula"), so this is worth having as a last direct attempt
        // before falling back to fuzzy scoring.
        var byContains = await Project(
                _context.CatalogMedicines
                    .Where(m => m.IsActive
                                && (EF.Functions.Like(m.BrandName, $"%{value}%")
                                    || (m.Generic != null
                                        && EF.Functions.Like(m.Generic.Name, $"%{value}%"))))
                    .OrderBy(m => m.BrandName)
                    .Take(take))
            .ToListAsync(cancellationToken);

        return await WithImportFlags(byContains, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogMedicineSearchItemDto>> FuzzyCandidatesAsync(
        string term, int maxCandidates, CancellationToken cancellationToken = default)
    {
        var value = term.Trim();

        if (value.Length < 2)
        {
            return [];
        }

        // ── The bounded candidate set ───────────────────────────────────────────────────
        //
        // Two cheap filters, both done in SQL, so the app server never sees the catalogue:
        //
        //   1. The first two characters must match. A typo that changes the opening pair is
        //      not something fuzzy matching would rescue anyway, and this alone cuts 21,714
        //      rows to tens or low hundreds.
        //   2. The length must be within 4 characters. "napaa" cannot plausibly be a 30-
        //      character combination product, and length is the cheapest possible proxy for
        //      edit distance.
        //
        // Then TAKE a hard ceiling, so even a pathological prefix like "pa" cannot turn into
        // an unbounded read. The ceiling is the guarantee; the filters are what make it rare
        // to reach.
        var prefix = value[..2];
        var length = value.Length;

        var candidates = await Project(
                _context.CatalogMedicines
                    .Where(m => m.IsActive
                                && EF.Functions.Like(m.BrandName, $"{prefix}%")
                                && m.BrandName.Length >= length - 4
                                && m.BrandName.Length <= length + 4)
                    .OrderBy(m => m.BrandName)
                    .Take(maxCandidates))
            .ToListAsync(cancellationToken);

        return await WithImportFlags(candidates, cancellationToken);
    }

    public async Task<CatalogMedicineSearchItemDto?> FindAsync(
        int catalogMedicineId, CancellationToken cancellationToken = default)
    {
        var entry = await Project(
                _context.CatalogMedicines.Where(m => m.Id == catalogMedicineId))
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
        {
            return null;
        }

        var flagged = await WithImportFlags([entry], cancellationToken);

        return flagged[0];
    }

    public async Task<IReadOnlyDictionary<int, CatalogMedicineSearchItemDto>> FindManyAsync(
        IReadOnlyCollection<int> catalogMedicineIds,
        CancellationToken cancellationToken = default)
    {
        if (catalogMedicineIds.Count == 0)
        {
            return new Dictionary<int, CatalogMedicineSearchItemDto>();
        }

        // One query for the whole selection. The bulk import can carry two hundred ids, and
        // two hundred calls to FindAsync would be two hundred round trips - each of which also
        // runs its own already-imported check.
        var ids = catalogMedicineIds.Distinct().ToList();

        var entries = await Project(
                _context.CatalogMedicines.Where(m => ids.Contains(m.Id)))
            .ToListAsync(cancellationToken);

        var flagged = await WithImportFlags(entries, cancellationToken);

        // Keyed by id; an id that does not exist is simply missing from the result. The caller
        // reports that per row rather than failing the whole call, because one bad id in a
        // batch of two hundred should say which one.
        return flagged.ToDictionary(entry => entry.Id);
    }

    /// <summary>
    /// The shared projection. Score is filled in later by the fuzzy stage; a direct match has
    /// no score to report and leaves it at zero.
    ///
    /// <para><b>Apply this last.</b> Ordering or paging a query that has already been
    /// projected into a record cannot be translated to SQL — EF has no way to reach
    /// <c>BrandName</c> through a constructor call — so every caller sorts and takes on the
    /// entity query and projects at the end.</para>
    /// </summary>
    private static IQueryable<CatalogMedicineSearchItemDto> Project(
        IQueryable<Domain.Entities.Catalog.CatalogMedicine> source) =>
        source.AsNoTracking().Select(m => new CatalogMedicineSearchItemDto(
            m.Id,
            m.BrandName,
            m.Generic != null ? m.Generic.Name : null,
            m.Manufacturer != null ? m.Manufacturer.Name : null,
            m.Strength,
            m.DosageForm != null ? m.DosageForm.Name : null,
            m.Generic != null && m.Generic.IsAntibiotic,
            m.SourceUnitPrice,
            false,
            null,
            0));

    /// <summary>
    /// Marks which of these entries this pharmacy has already imported.
    ///
    /// <para>A second, small query rather than a join in the projection above. The join would
    /// mix a filtered table into an unfiltered one inside a single SELECT, which works but
    /// reads as though the catalogue were tenant-scoped. Keeping it separate makes the two
    /// regimes obvious, and it costs one indexed lookup over at most 20 ids.</para>
    ///
    /// <para>Entries are <b>flagged, not hidden</b>. Hiding them would leave someone searching
    /// for Napa, finding nothing, and concluding the catalogue is missing it — when in fact
    /// their earlier import succeeded. The flag carries the existing product's id so the UI
    /// can link straight to it.</para>
    /// </summary>
    private async Task<IReadOnlyList<CatalogMedicineSearchItemDto>> WithImportFlags(
        IReadOnlyList<CatalogMedicineSearchItemDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var ids = items.Select(i => i.Id).ToList();

        // Products is tenant-filtered, so this only ever sees the caller's own catalogue.
        var imported = await _context.Products
            .AsNoTracking()
            .Where(p => p.CatalogMedicineId != null && ids.Contains(p.CatalogMedicineId.Value))
            .Select(p => new { CatalogId = p.CatalogMedicineId!.Value, p.Id })
            .ToListAsync(cancellationToken);

        if (imported.Count == 0)
        {
            return items;
        }

        var map = imported
            .GroupBy(x => x.CatalogId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        return items
            .Select(item => map.TryGetValue(item.Id, out var productId)
                ? item with { AlreadyImported = true, ExistingProductId = productId }
                : item)
            .ToList();
    }
}
