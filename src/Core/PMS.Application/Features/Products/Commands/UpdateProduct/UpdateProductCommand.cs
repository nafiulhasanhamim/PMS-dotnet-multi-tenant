using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.UpdateProduct;

/// <summary>
/// Command to update an existing product.
/// </summary>
public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    string Currency = "USD",
    string? Description = null) : IRequest<Result<ProductDto>>;
