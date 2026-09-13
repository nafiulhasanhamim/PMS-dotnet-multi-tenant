using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Queries.GetProduct;

public sealed record GetProductQuery(Guid Id)
    : IRequest<Result<ProductDto>>, ITenantScopedRequest;
