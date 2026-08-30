using Ardalis.GuardClauses;
using PMS.Application.Features.Products.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.DeleteProduct;

/// <summary>
/// Handler for DeleteProductCommand.
/// </summary>
public sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IRepository<Product, IApplicationDbContext> _productRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public DeleteProductCommandHandler(
        IRepository<Product, IApplicationDbContext> productRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _productRepository = Guard.Against.Null(productRepository);
        _unitOfWork = Guard.Against.Null(unitOfWork);
    }

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.FirstOrDefaultAsync(
            new ProductByIdSpec(request.Id), cancellationToken);

        if (product is null)
        {
            return Result.Failure(
                Error.NotFound("Product", request.Id));
        }

        await _productRepository.DeleteAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
