using PMS.Application.Common.DTOs;
using PMS.Application.Common.Products;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Products.Commands.BulkImportProducts;

public sealed class BulkImportProductsCommandHandler
    : IRequestHandler<BulkImportProductsCommand, Result<BulkImportResultDto>>
{
    private readonly ICatalogSearchQueries _catalog;
    private readonly IProductQueries _products;
    private readonly IRepository<Product, IApplicationDbContext> _repository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<BulkImportProductsCommandHandler> _logger;

    /// <summary>
    /// The per-row validator, built once. It applies <c>ProductWriteRules</c> — the same unit
    /// pairing and medicine-field rules the single-product form uses — and nothing else, so a
    /// row can legitimately arrive with no prices.
    /// </summary>
    private static readonly BulkImportRowValidator RowValidator = new();

    public BulkImportProductsCommandHandler(
        ICatalogSearchQueries catalog,
        IProductQueries products,
        IRepository<Product, IApplicationDbContext> repository,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<BulkImportProductsCommandHandler> logger)
    {
        _catalog = catalog;
        _products = products;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<BulkImportResultDto>> Handle(
        BulkImportProductsCommand request, CancellationToken cancellationToken)
    {
        var items = request.Items;

        // ── Three batch reads, not three per row ────────────────────────────────────────
        //
        // Two hundred rows each needing a catalogue lookup, a duplicate check and an
        // already-imported check would be six hundred round trips. These are three, and the
        // sets they return are small: a pharmacy holds hundreds of products, not millions.
        var catalogEntries = await _catalog.FindManyAsync(
            items.Select(item => item.CatalogMedicineId).ToList(), cancellationToken);

        var existingKeys = await _products.GetIdentityKeysAsync(cancellationToken);
        var alreadyImported = await _products.GetImportedCatalogIdsAsync(cancellationToken);

        // Identity keys claimed by earlier rows of this same request. Without it two rows
        // would both pass the database check and then lose to the unique index at insert,
        // reporting a race that was really a duplicate selection.
        //
        // Since migration 010 the key includes the dosage form, so two rows collide here only
        // when they are genuinely the same product. Before that, selecting a cream and a
        // lotion of the same brand and strength was refused - the reference catalogue has 548
        // brand+strength groups holding more than one entry, and Nyclobate 0.05% alone has
        // six. That refusal is what led to the identity being widened.
        var keysInThisBatch = new Dictionary<string, int>(StringComparer.Ordinal);

        var prepared = new List<PreparedRow>(items.Count);

        for (var row = 0; row < items.Count; row++)
        {
            prepared.Add(Prepare(
                row, items[row], catalogEntries, existingKeys, alreadyImported, keysInThisBatch));
        }

        var failures = prepared.Where(row => row.Errors.Count > 0).ToList();

        if (failures.Count > 0)
        {
            // ── All or nothing ─────────────────────────────────────────────────────────
            //
            // Nothing is written, including the rows that were individually fine. A partly
            // imported catalogue is the worst of the three possible outcomes: the pharmacy
            // cannot tell which of two hundred medicines arrived without checking each one,
            // and a second attempt at the whole selection then collides with whatever the
            // first attempt managed. Refusing the batch leaves one clear action - fix the
            // rows named below and send it again.
            _logger.LogWarning(
                "Bulk import refused: {FailedCount} of {TotalCount} rows failed validation, "
                + "so none were created. First failures: {Failures}",
                failures.Count,
                items.Count,
                string.Join("; ", failures
                    .Take(5)
                    .Select(row => $"row {row.Row}: {string.Join(", ", row.Errors.Keys)}")));

            return new BulkImportResultDto(
                Succeeded: false,
                Created: 0,
                Failed: failures.Count,
                Items: prepared.Select(row => row.ToResult(productId: null)).ToList());
        }

        foreach (var row in prepared)
        {
            await _repository.AddAsync(row.Product!, cancellationToken);
        }

        try
        {
            // One save for the whole batch. EF wraps a single SaveChanges in its own
            // transaction, so every insert lands or none does — which is the guarantee this
            // endpoint promises. An explicit transaction is not an option: the context enables
            // retry-on-failure, and the retrying execution strategy refuses one.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException ex)
        {
            // The pre-checks lost a race, or two clients imported the same selection at once.
            // The constraint is the only thing that can actually decide it, and the whole
            // batch has rolled back with it.
            _logger.LogWarning(
                ex,
                "Bulk import of {Count} products lost a race against {Constraint}; "
                + "nothing was created",
                items.Count, ex.ConstraintName ?? "a unique index");

            return Result.Failure<BulkImportResultDto>(Error.Conflict(
                "One of these medicines was added by someone else while this import was "
                + "being prepared, so nothing was imported. Reload the catalogue and try "
                + "again."));
        }

        var withoutPrices = prepared.Count(row => !row.Product!.IsSetupComplete);

        _logger.LogInformation(
            "Bulk import created {Count} products, {WithoutPrices} of them without prices "
            + "(setup incomplete). Antibiotics confirmed: {AntibioticCount}",
            prepared.Count,
            withoutPrices,
            prepared.Count(row => row.Product!.IsAntibiotic));

        return new BulkImportResultDto(
            Succeeded: true,
            Created: prepared.Count,
            Failed: 0,
            Items: prepared.Select(row => row.ToResult(row.Product!.Id)).ToList());
    }

    /// <summary>
    /// Validates one row and, if it is sound, builds the product it would create.
    ///
    /// <para>Building the entity here rather than in a second pass means the caller has
    /// everything it needs to either save the lot or report the lot, without re-deriving
    /// anything.</para>
    /// </summary>
    private static PreparedRow Prepare(
        int row,
        BulkImportItem item,
        IReadOnlyDictionary<int, CatalogMedicineSearchItemDto> catalogEntries,
        IReadOnlySet<string> existingKeys,
        IReadOnlySet<int> alreadyImported,
        Dictionary<string, int> keysInThisBatch)
    {
        var errors = new Dictionary<string, List<string>>();

        void Fail(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = [];
                errors[field] = messages;
            }

            messages.Add(message);
        }

        if (!catalogEntries.TryGetValue(item.CatalogMedicineId, out var entry))
        {
            // No entry means no brand name, so there is nothing further worth checking on
            // this row — every other rule needs the catalogue details.
            Fail(
                nameof(BulkImportItem.CatalogMedicineId),
                $"There is no medicine {item.CatalogMedicineId} in the reference catalogue.");

            return new PreparedRow(row, item.CatalogMedicineId, null, errors, null);
        }

        if (alreadyImported.Contains(item.CatalogMedicineId))
        {
            Fail(
                nameof(BulkImportItem.CatalogMedicineId),
                $"'{entry.BrandName}' has already been imported into your catalogue.");
        }

        // The details come from the catalogue row, never from the request. A client cannot
        // import under one catalogue id with another medicine's name.
        var writeRequest = new BulkImportRow(item, entry);

        var validation = RowValidator.Validate(writeRequest);

        foreach (var failure in validation.Errors)
        {
            Fail(failure.PropertyName, failure.ErrorMessage);
        }

        var key = ProductKeys.Identity(entry.BrandName, entry.Strength, entry.DosageForm);
        var identity = ProductKeys.Describe(entry.BrandName, entry.Strength, entry.DosageForm);

        if (existingKeys.Contains(key))
        {
            // Names all three parts, so "you already have this" is checkable against what is
            // on screen rather than something the person has to take on trust.
            Fail(
                nameof(BulkImportItem.CatalogMedicineId),
                $"You already have {identity} in your catalogue.");
        }
        else if (keysInThisBatch.TryGetValue(key, out var earlierRow))
        {
            // The same product twice, dosage form included - two catalogue entries that are
            // genuinely duplicates of each other. The catalogue does contain some: Milk of
            // Magnesia 400 mg/5 ml appears four times, all Oral Suspension.
            Fail(
                nameof(BulkImportItem.CatalogMedicineId),
                $"Row {earlierRow + 1} is the same product — {identity}. "
                + "Untick one of them.");
        }
        else
        {
            keysInThisBatch[key] = row;
        }

        if (errors.Count > 0)
        {
            return new PreparedRow(row, item.CatalogMedicineId, entry.BrandName, errors, null);
        }

        var product = new Product(
            ProductType.Medicine, entry.BrandName, item.BaseUnitName, item.PricePerBase);

        product.Update(
            ProductType.Medicine,
            entry.BrandName,
            entry.Manufacturer,
            item.Category,
            entry.GenericName,
            entry.Strength,
            entry.DosageForm,
            item.IsAntibiotic,
            item.BaseUnitName,
            item.MidUnitName,
            item.LargeUnitName,
            item.BasePerMid,
            item.MidPerLarge,
            item.PricePerBase,
            item.PricePerMid,
            item.PricePerLarge,
            item.ReorderLevel,
            item.ShelfLocation);

        product.LinkToCatalog(item.CatalogMedicineId);

        return new PreparedRow(row, item.CatalogMedicineId, entry.BrandName, errors, product);
    }

    /// <summary>One row, validated, with the product it would create.</summary>
    private sealed record PreparedRow(
        int Row,
        int CatalogMedicineId,
        string? BrandName,
        Dictionary<string, List<string>> Errors,
        Product? Product)
    {
        public BulkImportItemResultDto ToResult(Guid? productId) => new(
            Row,
            CatalogMedicineId,
            BrandName,
            Succeeded: Errors.Count == 0 && productId is not null,
            productId,
            Errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray()));
    }
}

/// <summary>
/// One bulk row seen as a product write, so <c>ProductWriteRules</c> can validate it.
///
/// <para>The adapter exists so the unit-pairing and medicine-field rules are the ones the
/// single-product form uses, rather than a second implementation that would drift. The
/// catalogue supplies the identity fields; the request supplies the pharmacy's own decisions.
/// </para>
/// </summary>
internal sealed class BulkImportRow : IProductWriteRequest
{
    private readonly BulkImportItem _item;
    private readonly CatalogMedicineSearchItemDto _entry;

    public BulkImportRow(BulkImportItem item, CatalogMedicineSearchItemDto entry)
    {
        _item = item;
        _entry = entry;
    }

    // Every bulk-imported row is a medicine: the reference catalogue holds nothing else.
    public ProductType ProductType => ProductType.Medicine;

    public string BrandName => _entry.BrandName;

    public string? Company => _entry.Manufacturer;

    public string? Category => _item.Category;

    public string? GenericName => _entry.GenericName;

    public string? Strength => _entry.Strength;

    public string? DosageForm => _entry.DosageForm;

    public bool IsAntibiotic => _item.IsAntibiotic;

    public string BaseUnitName => _item.BaseUnitName;

    public string? MidUnitName => _item.MidUnitName;

    public string? LargeUnitName => _item.LargeUnitName;

    public int? BasePerMid => _item.BasePerMid;

    public int? MidPerLarge => _item.MidPerLarge;

    public decimal? PricePerBase => _item.PricePerBase;

    public decimal? PricePerMid => _item.PricePerMid;

    public decimal? PricePerLarge => _item.PricePerLarge;

    public int ReorderLevel => _item.ReorderLevel;

    public string? ShelfLocation => _item.ShelfLocation;
}

/// <summary>
/// The shared rules, with no price requirement added. Prices are optional here — that is the
/// whole point of the feature — and their presence is what decides <c>IsSetupComplete</c>.
/// </summary>
internal sealed class BulkImportRowValidator : AbstractValidator<BulkImportRow>
{
    public BulkImportRowValidator() => ProductWriteRules.ApplyTo(this);
}
