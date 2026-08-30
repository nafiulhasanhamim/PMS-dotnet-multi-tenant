using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.CreateProduct;

/// <summary>
/// Command to create a new product.
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string Sku,
    decimal Price,
    string Currency = "USD",
    string? Description = null,
    int InitialStock = 0) : IRequest<Result<ProductDto>>;
