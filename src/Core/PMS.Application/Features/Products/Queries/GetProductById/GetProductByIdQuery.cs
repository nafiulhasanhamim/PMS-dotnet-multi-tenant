using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Queries.GetProductById;

/// <summary>
/// Query to get a product by ID.
/// </summary>
public sealed record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductDto>>;
