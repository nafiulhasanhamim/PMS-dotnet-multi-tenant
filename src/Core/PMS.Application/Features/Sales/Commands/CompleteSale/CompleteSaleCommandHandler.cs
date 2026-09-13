using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.Billing;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Application.Common.Settings;
using PMS.Application.Common.Units;
using PMS.Application.Features.Sales.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Billing;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Commands.CompleteSale;

/// <summary>
/// The sale. Everything this module exists for happens in one method, and it happens in one
/// transaction.
///
/// <para><b>The order of the work is the design.</b> Products are read and every blocking rule
/// checked; quantities are converted and priced from the product; FEFO decides which batches
/// pay for each item; the discount is split across the resulting lines; the cash settles it.
/// Only then is an invoice number allocated and the whole thing written. Any refusal before the
/// write leaves the shelves and the ledger exactly as they were.</para>
///
/// <para><b>Why the transaction is real here, unlike elsewhere in the codebase.</b> Module 3
/// gets its atomicity from a single <c>SaveChanges</c>, which is enough when there are two rows
/// to write. A sale allocates an invoice number with its own statement, so one save is not
/// enough — and half a sale is the worst possible outcome: stock deducted with no invoice, or an
/// invoice number burned with no sale. <c>IUnitOfWork.ExecuteInTransactionAsync</c> wraps the
/// block in the context's execution strategy, which is what makes a genuine transaction
/// possible while retry-on-failure is enabled.</para>
/// </summary>
public sealed class CompleteSaleCommandHandler
    : IRequestHandler<CompleteSaleCommand, Result<SaleCompletedDto>>
{
    private readonly IRepository<Product, IApplicationDbContext> _products;
    private readonly IRepository<Sale, IApplicationDbContext> _sales;
    private readonly IStockQueries _stock;
    private readonly ISettingsService _settings;
    private readonly IInvoiceNumberGenerator _invoiceNumbers;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ILogger<CompleteSaleCommandHandler> _logger;

    public CompleteSaleCommandHandler(
        IRepository<Product, IApplicationDbContext> products,
        IRepository<Sale, IApplicationDbContext> sales,
        IStockQueries stock,
        ISettingsService settings,
        IInvoiceNumberGenerator invoiceNumbers,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ICurrentUserService currentUser,
        IDateTime clock,
        ILogger<CompleteSaleCommandHandler> logger)
    {
        _products = products;
        _sales = sales;
        _stock = stock;
        _settings = settings;
        _invoiceNumbers = invoiceNumbers;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<SaleCompletedDto>> Handle(
        CompleteSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;
        var role = _currentUser.TenantRole();

        if (userId is null || role is null)
        {
            // A sale with no cashier is a sale nobody can be asked about, and the cashier is
            // the first column an owner reads when the till does not balance.
            _logger.LogError(
                "Sale refused: the request authenticated but carries no user id or no tenant "
                + "role claim (user {UserId}, role {Role})",
                _currentUser.UserId, _currentUser.TenantRoleName);

            return Result.Failure<SaleCompletedDto>(Error.Unauthorized("Not signed in."));
        }

        // Everything from here runs inside one transaction, and the delegate loads all of its
        // own state — the execution strategy may run it more than once and clears the change
        // tracker before each attempt. See IUnitOfWork.ExecuteInTransactionAsync.
        return await _unitOfWork.ExecuteInTransactionAsync(
            ct => CompleteAsync(request, userId.Value, role.Value, ct),
            cancellationToken);
    }

    private async Task<Result<SaleCompletedDto>> CompleteAsync(
        CompleteSaleCommand request, Guid cashierUserId, UserRole role, CancellationToken ct)
    {
        // ── 0. How strictly this pharmacy handles antibiotics ───────────────────────────
        //
        // Read from the pharmacy, once, and never assumed. Module 7 made both antibiotic rules
        // below conditional on it, and a handler that defaulted to Required would block a
        // pharmacy that had deliberately chosen otherwise; one that defaulted to Off would
        // quietly let an Employee dispense at a Model Pharmacy. ISettingsService caches it for
        // the life of the request, so asking per cart item costs one query.
        var mode = await _settings.GetEnumAsync<AntibioticPrescriptionMode>(
            SettingKeys.AntibioticPrescriptionMode, ct);

        // ── 1. The products, and every reason one cannot be sold ────────────────────────
        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _products.ListAsync(
            new ProductsByIdsSpec(productIds), ct);

        var byId = products.ToDictionary(product => product.Id);

        foreach (var id in productIds)
        {
            if (!byId.TryGetValue(id, out var product))
            {
                // Also what another pharmacy's product id looks like from here: the global
                // query filter simply does not return it.
                _logger.LogWarning(
                    "Sale refused: product {ProductId} is not in this pharmacy", id);

                return Result.Failure<SaleCompletedDto>(
                    Error.NotFound(nameof(Product), id));
            }

            if (Blocked(product, role, cashierUserId, mode) is { } failure)
            {
                return failure;
            }
        }

        // ── 2. Prescription, if the mode asks for one ───────────────────────────────────
        //
        // Only Required validates. Under Optional whatever arrived is kept as-is and nothing is
        // refused; under Off nothing is sent and nothing is stored. See
        // AntibioticPrescriptionMode for why the loosest setting is the default.
        var antibiotics = byId.Values.Where(product => product.IsAntibiotic).ToList();

        if (antibiotics.Count > 0 && mode == AntibioticPrescriptionMode.Required)
        {
            var prescriptionFailure = ValidatePrescription(request.Prescription, antibiotics);

            if (prescriptionFailure is not null)
            {
                return prescriptionFailure;
            }
        }

        // ── 3. Price and allocate every item ───────────────────────────────────────────
        //
        // FEFO batches are read once per product and reused across the cart items that share
        // it. Reading them per item would let a cart holding the same product twice allocate
        // the same batch stock twice: the second read would not see the first deduction,
        // because the first has not been saved yet.
        var fefoByProduct = new Dictionary<Guid, IReadOnlyList<Batch>>();

        foreach (var id in productIds)
        {
            fefoByProduct[id] = await _stock.GetActiveBatchesFefoAsync(id, ct);
        }

        var plan = new List<PlannedItem>();

        foreach (var item in request.Items)
        {
            var product = byId[item.ProductId];

            // The conversion comes first, and the order matters for the message. Asking for
            // "1 strip" of a product that has no strip is not a missing price, it is a pack
            // size that does not exist — and reporting it as a missing price sends the cashier
            // hunting for one. UnitConversion says it properly, in the product's own words:
            // "'X' has no middle unit, so a quantity cannot be expressed in one. It is sold in
            // bottles and cartons."
            int neededBaseUnits;

            try
            {
                neededBaseUnits = UnitConversion.ToBaseUnits(
                    item.Quantity, item.UnitLevel, product);
            }
            catch (Exception ex)
                when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                // Either the product does not define that level, or the quantity does not
                // divide into whole base units. Both are the client offering something the
                // product does not support.
                return Result.Failure<SaleCompletedDto>(Error.Validation(
                    nameof(CompleteSaleCommand.Items), ex.Message));
            }

            // Price for a level the product definitely has. Null is unreachable for a
            // setup-complete product, and is checked because the alternative to checking is a
            // NullReferenceException at a till.
            var unitPrice = PriceAt(product, item.UnitLevel);

            if (unitPrice is null)
            {
                _logger.LogError(
                    "Sale refused: '{BrandName}' ({ProductId}) is marked setup-complete but "
                    + "has no price at {UnitLevel}",
                    product.BrandName, product.Id, item.UnitLevel);

                return Result.Failure<SaleCompletedDto>(Error.Validation(
                    nameof(CompleteSaleCommand.Items),
                    $"'{product.BrandName}' has no price for that pack size."));
            }

            var fefo = fefoByProduct[product.Id];
            var allocation = FefoAllocator.Allocate(fefo, neededBaseUnits);

            if (!allocation.IsSatisfied)
            {
                _logger.LogWarning(
                    "Sale refused: '{BrandName}' ({ProductId}) needs {Needed} base units and "
                    + "has {Available} sellable across {BatchCount} batches",
                    product.BrandName, product.Id, neededBaseUnits,
                    allocation.AvailableInBaseUnits, fefo.Count);

                return Result.Failure<SaleCompletedDto>(Error.Validation(
                    nameof(CompleteSaleCommand.Items),
                    $"Only {allocation.AvailableInBaseUnits} "
                    + $"{UnitConversion.Describe(UnitLevel.Base, product)} of "
                    + $"'{product.BrandName}' available across all batches."));
            }

            // What the screen quoted: quantity times the unit price, rounded once. The split
            // across batches apportions this figure rather than re-deriving it per line, so
            // the invoice adds up to what the cashier read out. See SaleMath.SplitLineTotals.
            var itemTotal = SaleMath.Round(item.Quantity * unitPrice.Value);
            var lineTotals = SaleMath.SplitLineTotals(itemTotal, allocation.Quantities);

            plan.Add(new PlannedItem(product, item.UnitLevel, unitPrice.Value, allocation, lineTotals));

            // The allocation is applied to the in-memory batches immediately, so the next cart
            // item for this product sees what is actually left.
            for (var i = 0; i < allocation.Takes.Count; i++)
            {
                allocation.Takes[i].Batch.DeductForSale(allocation.Takes[i].QuantityInBaseUnits);
            }
        }

        // ── 4. Build the sale in the fixed order: lines, discount, cash ─────────────────
        var invoiceNumber = await _invoiceNumbers.NextAsync(ct);

        var sale = new Sale(
            invoiceNumber,
            cashierUserId,
            _clock.UtcNow,
            request.CustomerName,
            request.CustomerPhone);

        foreach (var planned in plan)
        {
            for (var i = 0; i < planned.Allocation.Takes.Count; i++)
            {
                var take = planned.Allocation.Takes[i];

                sale.AddLine(
                    planned.Product.Id,
                    take.Batch.Id,
                    take.QuantityInBaseUnits,
                    planned.UnitLevel,
                    planned.UnitPrice,
                    planned.LineTotals[i]);
            }
        }

        if (request.DiscountType is { } discountType && request.DiscountValue is { } discountValue)
        {
            var amount = SaleMath.DiscountAmountFor(sale.Subtotal, discountType, discountValue);

            // The pharmacy's own caps since Module 10. Read here rather than trusted from the
            // billing screen: the screen's helper text and its client-side check are a courtesy,
            // and this is the control.
            var caps = await DiscountCaps.FromSettingsAsync(_settings, ct);

            if (!BillingPolicy.IsDiscountAllowed(role, sale.Subtotal, amount, caps))
            {
                _logger.LogWarning(
                    "Sale refused: {Role} {UserId} attempted a discount of {Amount} on a "
                    + "subtotal of {Subtotal}, over the cap",
                    role, cashierUserId, amount, sale.Subtotal);

                return Result.Failure<SaleCompletedDto>(Error.Validation(
                    nameof(CompleteSaleCommand.DiscountValue),
                    BillingPolicy.DiscountRefusalMessage(role, sale.Subtotal, caps)));
            }

            sale.ApplyDiscount(discountType, discountValue);
        }

        try
        {
            sale.Settle(request.CashReceived);
        }
        catch (InvalidOperationException)
        {
            var owed = SaleMath.Round(sale.Subtotal - sale.DiscountAmount);

            return Result.Failure<SaleCompletedDto>(Error.Validation(
                nameof(CompleteSaleCommand.CashReceived),
                $"Cash received is less than the {owed:0.00} owed."));
        }

        // Stored whenever there is an antibiotic and the mode collects details at all. Under
        // Optional the fields may be partly filled, and a doctor's name with nothing else is
        // still worth more to a later inspection than a blank row — see Sale.SetPrescription,
        // which records rather than judges for exactly this reason.
        if (antibiotics.Count > 0
            && mode != AntibioticPrescriptionMode.Off
            && request.Prescription is { } prescription)
        {
            sale.SetPrescription(
                prescription.PatientName,
                prescription.PatientPhone,
                prescription.DoctorName,
                prescription.PrescriptionNumber,
                prescription.PrescriptionDate,
                prescription.PrescriptionVerified);
        }

        // ── 5. One save: the sale, its lines, and every batch deduction ────────────────
        //
        // The batches were loaded tracked by the FEFO query and mutated above, so their new
        // quantities are pending on this same context and go out with the insert.
        await _sales.AddAsync(sale, ct);

        _logger.LogInformation(
            "Sale completed {InvoiceNumber} ({SaleId}) by {Role} {UserId}: "
            + "{ItemCount} items over {LineCount} lines, subtotal {Subtotal}, "
            + "discount {DiscountAmount}, net {NetTotal}, cash {CashReceived}, "
            + "change {ChangeGiven}{Antibiotics}",
            sale.InvoiceNumber, sale.Id, role, cashierUserId,
            request.Items.Count, sale.Lines.Count, sale.Subtotal, sale.DiscountAmount,
            sale.NetTotal, sale.CashReceived, sale.ChangeGiven,
            antibiotics.Count > 0
                ? $", including {antibiotics.Count} antibiotic(s) under prescription mode "
                  + $"{mode}"
                  + (sale.PrescriptionNumber is { } number
                      ? $" against prescription {number}"
                      : " with no prescription recorded")
                : string.Empty);

        foreach (var planned in plan)
        {
            if (planned.Allocation.Takes.Count > 1)
            {
                // Worth a line of its own: a split means the soonest-expiring batch ran out
                // mid-item, which is both normal and the thing somebody reconciling a batch
                // will want an explanation for.
                _logger.LogInformation(
                    "Sale {InvoiceNumber} split '{BrandName}' across {BatchCount} batches: {Split}",
                    sale.InvoiceNumber, planned.Product.BrandName,
                    planned.Allocation.Takes.Count,
                    string.Join(", ", planned.Allocation.Takes.Select(
                        take => $"{take.QuantityInBaseUnits} from {take.Batch.BatchNumber}")));
            }
        }

        return new SaleCompletedDto(
            sale.Id,
            sale.InvoiceNumber,
            sale.Subtotal,
            sale.DiscountAmount,
            sale.NetTotal,
            sale.CashReceived,
            sale.ChangeGiven,
            sale.Lines.Count);
    }

    /// <summary>
    /// Part D, all five blocking rules, server-side.
    ///
    /// <para>The billing screen shows the same messages against the same products, and that is
    /// a convenience rather than the enforcement: a direct API call has to hit exactly the same
    /// wall, which is why this is here and not only there. The fifth rule — an inactive product
    /// never appearing in search at all — is in the query rather than here, because the point of
    /// it is absence; this is the backstop for an id somebody kept from before.</para>
    ///
    /// <para><b>One of the five is now conditional.</b> Module 7 made the Employee antibiotic
    /// block apply only under <see cref="AntibioticPrescriptionMode.Required"/>. Under Off and
    /// Optional an Employee dispenses an antibiotic like anything else — which is what most
    /// retail pharmacies actually do, and pretending otherwise produced invented patient names
    /// rather than compliance.</para>
    /// </summary>
    private Result<SaleCompletedDto>? Blocked(
        Product product, UserRole role, Guid cashierUserId, AntibioticPrescriptionMode mode)
    {
        if (!product.IsActive)
        {
            _logger.LogWarning(
                "Sale refused: {BrandName} ({ProductId}) is deactivated",
                product.BrandName, product.Id);

            return Result.Failure<SaleCompletedDto>(Error.Validation(
                nameof(CompleteSaleCommand.Items),
                $"'{product.BrandName}' is no longer sold by this pharmacy."));
        }

        if (!product.IsSetupComplete)
        {
            _logger.LogWarning(
                "Sale refused: {BrandName} ({ProductId}) has no prices set",
                product.BrandName, product.Id);

            return Result.Failure<SaleCompletedDto>(Error.Validation(
                nameof(CompleteSaleCommand.Items),
                $"'{product.BrandName}' needs prices set before it can be sold."));
        }

        // The rule lives in BillingPolicy, read by this handler, the sellable search and the
        // limits endpoint. Three inline copies of a rule with legal consequences would be three
        // chances to get it wrong differently.
        if (product.IsAntibiotic && !BillingPolicy.MaySellAntibiotics(role, mode))
        {
            _logger.LogWarning(
                "Sale refused: Employee {UserId} attempted to dispense the antibiotic "
                + "{BrandName} ({ProductId}) while the pharmacy is in {Mode} mode",
                cashierUserId, product.BrandName, product.Id, mode);

            // Forbidden rather than a validation error: a direct API call has to come back 403,
            // and the reason is the caller's role, not anything about the request.
            return Result.Failure<SaleCompletedDto>(Error.Forbidden(
                $"'{product.BrandName}' is an antibiotic. Antibiotics require a pharmacist. "
                + "Please call one over."));
        }

        // Out of stock and all-stock-expired are not checked here: both are answered by the
        // FEFO allocation below, which knows the quantity being asked for. Refusing on
        // "no sellable stock" before knowing how much was wanted would produce a worse message
        // than "only 12 available".
        return null;
    }

    /// <summary>
    /// Refuses an antibiotic sale whose prescription is missing or unverified.
    ///
    /// <para><b>Called only under Required mode</b> — the caller checks, because the rule is
    /// the pharmacy's rather than this method's. What is unconditional is that it runs
    /// server-side when it does run: the billing screen requires the same fields, but a client
    /// is a suggestion. Each field is named individually so the form can put the message under
    /// the input rather than in a banner.</para>
    /// </summary>
    private Result<SaleCompletedDto>? ValidatePrescription(
        PrescriptionRequest? prescription, IReadOnlyList<Product> antibiotics)
    {
        var names = string.Join(", ", antibiotics.Select(product => $"'{product.BrandName}'"));

        if (prescription is null)
        {
            _logger.LogWarning(
                "Sale refused: cart contains antibiotics ({Antibiotics}) and no prescription "
                + "details were sent",
                names);

            return Result.Failure<SaleCompletedDto>(Error.Validation(
                nameof(PrescriptionRequest.PrescriptionNumber),
                $"{names} require a prescription. Fill in the prescription details."));
        }

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(prescription.PatientName))
        {
            missing.Add(nameof(PrescriptionRequest.PatientName));
        }

        if (string.IsNullOrWhiteSpace(prescription.PatientPhone))
        {
            missing.Add(nameof(PrescriptionRequest.PatientPhone));
        }

        if (string.IsNullOrWhiteSpace(prescription.DoctorName))
        {
            missing.Add(nameof(PrescriptionRequest.DoctorName));
        }

        if (string.IsNullOrWhiteSpace(prescription.PrescriptionNumber))
        {
            missing.Add(nameof(PrescriptionRequest.PrescriptionNumber));
        }

        if (prescription.PrescriptionDate is null)
        {
            missing.Add(nameof(PrescriptionRequest.PrescriptionDate));
        }

        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "Sale refused: antibiotic sale ({Antibiotics}) missing prescription fields {Missing}",
                names, string.Join(", ", missing));

            return Result.Failure<SaleCompletedDto>(Error.Validation(
                missing[0],
                "Every prescription detail is required when the sale contains an antibiotic."));
        }

        if (!prescription.PrescriptionVerified)
        {
            _logger.LogWarning(
                "Sale refused: antibiotic sale ({Antibiotics}) not marked as verified", names);

            return Result.Failure<SaleCompletedDto>(Error.Validation(
                nameof(PrescriptionRequest.PrescriptionVerified),
                "Confirm that you have verified the prescription."));
        }

        return null;
    }

    /// <summary>The product's own price at one level, or null when it has none.</summary>
    private static decimal? PriceAt(Product product, UnitLevel level) => level switch
    {
        UnitLevel.Base => product.PricePerBase,
        UnitLevel.Mid => product.PricePerMid,
        UnitLevel.Large => product.PricePerLarge,
        _ => null,
    };

    /// <summary>One cart item, priced and allocated, ready to become sale lines.</summary>
    private sealed record PlannedItem(
        Product Product,
        UnitLevel UnitLevel,
        decimal UnitPrice,
        FefoAllocation Allocation,
        decimal[] LineTotals);
}
