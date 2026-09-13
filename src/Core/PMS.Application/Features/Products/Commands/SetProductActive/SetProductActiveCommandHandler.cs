using PMS.Application.Common.DTOs;
using PMS.Application.Common.Products;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Products.Commands.SetProductActive;

public sealed class SetProductActiveCommandHandler
    : IRequestHandler<SetProductActiveCommand, Result<ProductDto>>
{
    private readonly IRepository<Product, IApplicationDbContext> _repository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<SetProductActiveCommandHandler> _logger;

    public SetProductActiveCommandHandler(
        IRepository<Product, IApplicationDbContext> repository,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<SetProductActiveCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ProductDto>> Handle(
        SetProductActiveCommand request, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (product is null)
        {
            _logger.LogWarning(
                "Product activation not changed: {ProductId} not found in this pharmacy",
                request.Id);

            return Result.Failure<ProductDto>(Error.NotFound("Product", request.Id));
        }

        if (request.IsActive)
        {
            product.Reactivate();
        }
        else
        {
            product.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // A deactivation removes a product from every future sale and purchase, so it is the
        // change most likely to be queried later - "who took this off the list, and when?".
        _logger.LogInformation(
            "Product {Action} {ProductId} '{BrandName}'",
            request.IsActive ? "reactivated" : "deactivated",
            product.Id,
            product.BrandName);

        return ProductMapping.ToDto(product);
    }
}
