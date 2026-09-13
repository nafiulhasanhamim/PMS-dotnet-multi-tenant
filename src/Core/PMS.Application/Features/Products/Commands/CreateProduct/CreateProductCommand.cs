using PMS.Application.Common.DTOs;
using PMS.Application.Common.Products;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.CreateProduct;

/// <summary>
/// Adds a product to this pharmacy's catalogue.
///
/// <para>One command serves both paths. Importing from the reference catalogue is not a
/// separate operation — the import screen pre-fills a form from a catalogue row and posts
/// this with <see cref="CatalogMedicineId"/> set. That keeps one set of validation rules and
/// one write path, and makes an imported product indistinguishable from a hand-entered one
/// except for the link it carries.</para>
/// </summary>
public sealed record CreateProductCommand(
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
    int? CatalogMedicineId) : IRequest<Result<ProductDto>>, ITenantScopedRequest, IProductWriteRequest;
