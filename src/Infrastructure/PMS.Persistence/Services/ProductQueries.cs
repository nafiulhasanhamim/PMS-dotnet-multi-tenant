using PMS.Application.Common.DTOs;
using PMS.Application.Common.Products;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Grid;
using Microsoft.EntityFrameworkCore;
using DomainProductType = PMS.Domain.Enums.ProductType;

namespace PMS.Persistence.Services;

/// <summary>
/// Read-side projections over the current pharmacy's products.
///
/// <para><b>There is no WHERE on TenantId anywhere in this file, and that is correct.</b>
/// <c>Product</c> implements <c>ITenantEntity</c>, so the global query filter adds it to every
/// query below. Adding one by hand would be harmless duplication today and misleading
/// tomorrow — it would suggest the filter is not doing its job.</para>
///
/// <para>Everything projects straight to a DTO rather than materialising entities. A 25-row
/// grid does not need change tracking, and the projection is also how prices are withheld
/// from an Employee: the SELECT simply does not read them.</para>
/// </summary>
public sealed class ProductQueries : IProductQueries
{
    private readonly ApplicationDbContext _context;

    public ProductQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GridResult<ProductListItemDto>> ListAsync(
        ProductListType listType,
        string? search,
        ProductStatusFilter status,
        bool antibioticOnly,
        DomainProductType? productType,
        bool includePrices,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsNoTracking();

        // The two screens over one table.
        query = listType == ProductListType.Medicine
            ? query.Where(p => p.ProductType == DomainProductType.Medicine)
            : query.Where(p => p.ProductType != DomainProductType.Medicine);

        // Only meaningful on the "other items" screen, where the type dropdown appears.
        if (productType is not null)
        {
            query = query.Where(p => p.ProductType == productType.Value);
        }

        query = status switch
        {
            ProductStatusFilter.Active => query.Where(p => p.IsActive),
            ProductStatusFilter.Inactive => query.Where(p => !p.IsActive),

            // Active *and* unpriced. A deactivated product with no price is not something
            // anybody needs to act on - it is not for sale either way - so including it would
            // pad the list the pharmacy is working through.
            ProductStatusFilter.SetupIncomplete =>
                query.Where(p => p.IsActive && !p.IsSetupComplete),

            _ => query,
        };

        if (antibioticOnly)
        {
            query = query.Where(p => p.IsAntibiotic);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            // Contains, not a prefix: someone searching "paracetamol" expects to find
            // "Napa 500" through its generic, and someone searching "handwash" expects
            // "Savlon Handwash 500ml" where the word is in the middle of the name. A pharmacy
            // holds hundreds of products, not the catalogue's 21,714, so the scan is cheap —
            // this is the opposite trade-off from the catalogue search, on purpose.
            query = query.Where(p =>
                EF.Functions.Like(p.BrandName, $"%{term}%")
                || (p.GenericName != null && EF.Functions.Like(p.GenericName, $"%{term}%")));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.BrandName)
            .ThenBy(p => p.Strength)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.ProductType,
                p.BrandName,
                p.GenericName,
                p.Company,
                p.Strength,
                p.DosageForm,
                p.Category,
                p.IsAntibiotic,
                p.IsActive,
                p.BaseUnitName,
                // The price-hiding, done here in the projection. For an Employee the column
                // is never read, so it never crosses the wire — as opposed to sending it and
                // trusting the UI to omit a column, which is not a control at all.
                includePrices ? p.PricePerBase : null,
                p.CatalogMedicineId != null,
                p.IsSetupComplete))
            .ToListAsync(cancellationToken);

        return GridResult<ProductListItemDto>.Create(items, total, page, pageSize);
    }

    public async Task<ProductDto?> FindAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // Another pharmacy's id returns null here rather than a forbidden row: the filter
        // removed it before this query ran, so it is genuinely not there.
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return product is null
            ? null
            : PMS.Application.Common.Products.ProductMapping.ToDto(product);
    }

    public Task<bool> ExistsWithIdentityAsync(
        string brandName,
        string? strength,
        string? dosageForm,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        var name = brandName.Trim();
        var normalizedStrength = Blank(strength);
        var normalizedForm = Blank(dosageForm);

        // Case-insensitive through the database collation, which is what the unique index in
        // migration 010 relies on too — so the check and the constraint agree.
        //
        // Written as three column comparisons rather than against the computed IdentityKey
        // column: that column is not mapped in the EF model, and comparing against a
        // constructed string would stop the index being seekable anyway. The columns are the
        // leading keys of IX_Products_Tenant_Brand_Strength.
        var query = _context.Products.AsNoTracking()
            .Where(p => p.BrandName == name);

        query = normalizedStrength is null
            ? query.Where(p => p.Strength == null)
            : query.Where(p => p.Strength == normalizedStrength);

        query = normalizedForm is null
            ? query.Where(p => p.DosageForm == null)
            : query.Where(p => p.DosageForm == normalizedForm);

        if (excludingId is not null)
        {
            // So an edit can re-save itself without colliding with its own row.
            query = query.Where(p => p.Id != excludingId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// The brand + strength keys this pharmacy already holds, normalised the way the bulk
    /// import compares them.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetIdentityKeysAsync(
        CancellationToken cancellationToken = default)
    {
        // Inactive products included, and that matters: the unique index does not exempt them,
        // so a key check that skipped them would report no clash and then lose to the
        // constraint at insert time.
        var keys = await _context.Products
            .AsNoTracking()
            .Select(p => new { p.BrandName, p.Strength, p.DosageForm })
            .ToListAsync(cancellationToken);

        return keys
            .Select(key => ProductKeys.Identity(key.BrandName, key.Strength, key.DosageForm))
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<int>> GetImportedCatalogIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var ids = await _context.Products
            .AsNoTracking()
            .Where(p => p.CatalogMedicineId != null)
            .Select(p => p.CatalogMedicineId!.Value)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public Task<int> CountSetupIncompleteAsync(CancellationToken cancellationToken = default) =>
        // Active only. A deactivated product with no price is not something anybody needs to
        // be nagged about - it is not for sale either way.
        _context.Products
            .AsNoTracking()
            .CountAsync(p => p.IsActive && !p.IsSetupComplete, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetForPriceUpdateAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
        // Tracked, deliberately: the caller is about to set prices on these and save. The
        // tenant filter still applies, so another pharmacy's id simply does not come back and
        // the caller reports it as not found.
        => await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
}
