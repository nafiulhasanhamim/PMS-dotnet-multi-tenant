using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Products.Commands.SetProductPrices;

public sealed class SetProductPricesCommandHandler
    : IRequestHandler<SetProductPricesCommand, Result<BulkImportResultDto>>
{
    private readonly IProductQueries _products;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<SetProductPricesCommandHandler> _logger;

    public SetProductPricesCommandHandler(
        IProductQueries products,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<SetProductPricesCommandHandler> logger)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<BulkImportResultDto>> Handle(
        SetProductPricesCommand request, CancellationToken cancellationToken)
    {
        var items = request.Items;

        // Tracked, and filtered to this pharmacy by the query filter — so another tenant's id
        // simply does not come back and is reported below as not found.
        var products = await _products.GetForPriceUpdateAsync(
            items.Select(item => item.ProductId).Distinct().ToList(), cancellationToken);

        var byId = products.ToDictionary(product => product.Id);
        var results = new List<BulkImportItemResultDto>(items.Count);
        var failed = 0;

        for (var row = 0; row < items.Count; row++)
        {
            var item = items[row];
            var errors = new Dictionary<string, string[]>();

            if (!byId.TryGetValue(item.ProductId, out var product))
            {
                errors[nameof(ProductPriceUpdate.ProductId)] =
                    ["That product no longer exists in this pharmacy."];
            }
            else
            {
                // The pack-level prices are required exactly when the product defines that
                // level. Checked against the product rather than trusting the form, which may
                // have been rendered before somebody changed the unit configuration.
                if (product.HasMidUnit && item.PricePerMid is null)
                {
                    errors[nameof(ProductPriceUpdate.PricePerMid)] =
                        [$"Enter the price for one {product.MidUnitName}."];
                }

                if (product.HasLargeUnit && item.PricePerLarge is null)
                {
                    errors[nameof(ProductPriceUpdate.PricePerLarge)] =
                        [$"Enter the price for one {product.LargeUnitName}."];
                }
            }

            if (errors.Count > 0)
            {
                failed++;
            }

            results.Add(new BulkImportItemResultDto(
                row,
                CatalogMedicineId: 0,
                product?.BrandName,
                Succeeded: errors.Count == 0,
                errors.Count == 0 ? item.ProductId : null,
                errors));
        }

        if (failed > 0)
        {
            // All-or-nothing, as with the import. Saving the rows that were fine would leave
            // the grid half-applied, and the person would have to work out which of thirty
            // rows still needed attention.
            _logger.LogWarning(
                "Prices not set: {FailedCount} of {TotalCount} rows were rejected, so none "
                + "were applied",
                failed, items.Count);

            return new BulkImportResultDto(
                Succeeded: false, Created: 0, Failed: failed, Items: results);
        }

        foreach (var item in items)
        {
            var product = byId[item.ProductId];

            // SetPrices recomputes IsSetupComplete, which is the point of the whole screen:
            // filling the last price is what makes a product sellable.
            product.SetPrices(item.PricePerBase, item.PricePerMid, item.PricePerLarge);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var stillIncomplete = items.Count(item => !byId[item.ProductId].IsSetupComplete);

        _logger.LogInformation(
            "Prices set on {Count} products; {CompletedCount} are now ready to sell",
            items.Count, items.Count - stillIncomplete);

        if (stillIncomplete > 0)
        {
            // Should be unreachable: every level the product defines was required above. If it
            // happens, the rule and the entity disagree and that is worth knowing loudly.
            _logger.LogError(
                "{Count} products still report incomplete setup after their prices were set. "
                + "SetProductPricesCommandHandler and Product.RecomputeSetupComplete disagree "
                + "about what a complete product is",
                stillIncomplete);
        }

        return new BulkImportResultDto(
            Succeeded: true, Created: items.Count, Failed: 0, Items: results);
    }
}

public sealed class SetProductPricesCommandValidator : AbstractValidator<SetProductPricesCommand>
{
    /// <summary>Same cap as the import, for the same reasons.</summary>
    public const int MaxItems = 200;

    public SetProductPricesCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("There is nothing to save.");

        RuleFor(x => x.Items)
            .Must(items => items.Count <= MaxItems)
            .WithMessage($"At most {MaxItems} products can be saved in one go.")
            .When(x => x.Items is not null);

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ProductId).NotEmpty();

            item.RuleFor(x => x.PricePerBase)
                .GreaterThanOrEqualTo(0).WithMessage("A price cannot be negative.");

            item.RuleFor(x => x.PricePerMid)
                .GreaterThanOrEqualTo(0).WithMessage("A price cannot be negative.")
                .When(x => x.PricePerMid is not null);

            item.RuleFor(x => x.PricePerLarge)
                .GreaterThanOrEqualTo(0).WithMessage("A price cannot be negative.")
                .When(x => x.PricePerLarge is not null);
        });
    }
}
