using PMS.Application.Common.DTOs;
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
                p.CatalogMedicineId != null))
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

    public Task<bool> ExistsWithBrandAndStrengthAsync(
        string brandName,
        string? strength,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        var name = brandName.Trim();
        var normalizedStrength = string.IsNullOrWhiteSpace(strength) ? null : strength.Trim();

        // Case-insensitive through the database collation, which is what the unique index in
        // script 007 relies on too — so the check and the constraint agree.
        var query = _context.Products.AsNoTracking()
            .Where(p => p.BrandName == name);

        query = normalizedStrength is null
            ? query.Where(p => p.Strength == null)
            : query.Where(p => p.Strength == normalizedStrength);

        if (excludingId is not null)
        {
            // So an edit can re-save itself without colliding with its own row.
            query = query.Where(p => p.Id != excludingId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }
}
