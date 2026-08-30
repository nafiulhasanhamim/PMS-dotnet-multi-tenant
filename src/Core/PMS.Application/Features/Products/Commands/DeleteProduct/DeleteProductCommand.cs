using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.DeleteProduct;

/// <summary>
/// Command to delete a product.
/// </summary>
public sealed record DeleteProductCommand(Guid Id) : IRequest<Result>;
