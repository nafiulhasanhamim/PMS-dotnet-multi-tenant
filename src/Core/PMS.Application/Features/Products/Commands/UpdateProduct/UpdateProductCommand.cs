using PMS.Application.Common.DTOs;
using PMS.Application.Common.Products;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.UpdateProduct;

/// <summary>
/// Edits a product.
///
/// <para>Note what is absent: CatalogMedicineId. The link to the reference catalogue records
/// where a product came from, and an edit does not change that history - re-pointing it at a
/// different catalogue row would make the provenance a lie.</para>
/// </summary>
public sealed record UpdateProductCommand(
    Guid Id,
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
    decimal PricePerBase,
    decimal? PricePerMid,
    decimal? PricePerLarge,
    int ReorderLevel,
    string? ShelfLocation) : IRequest<Result<ProductDto>>, ITenantScopedRequest, IProductWriteRequest;
