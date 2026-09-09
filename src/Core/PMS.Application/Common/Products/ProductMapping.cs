using PMS.Application.Common.DTOs;
using PMS.Application.Common.Units;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Products;

/// <summary>
/// Turns a tracked <see cref="Product"/> into its detail DTO.
///
/// Used by the write handlers, which already hold the entity and would otherwise re-query it
/// just to return one. List reads project straight from the database instead - see
/// IProductQueries - because materialising entities for a 25-row grid is wasted work.
/// </summary>
public static class ProductMapping
{
    public static ProductDto ToDto(Product product) => new(
        product.Id,
        product.ProductType,
        product.BrandName,
        product.GenericName,
        product.Company,
        product.Strength,
        product.DosageForm,
        product.Category,
        product.IsAntibiotic,
        product.IsActive,
        product.CatalogMedicineId,
        product.BaseUnitName,
        product.MidUnitName,
        product.LargeUnitName,
        product.BasePerMid,
        product.MidPerLarge,
        product.BaseUnitsPerLarge,
        UnitConversion.DescribePacking(product),
        product.PricePerBase,
        product.PricePerMid,
        product.PricePerLarge,
        product.ReorderLevel,
        product.ShelfLocation,
        product.CreatedOnUtc,
        product.ModifiedOnUtc);
}
