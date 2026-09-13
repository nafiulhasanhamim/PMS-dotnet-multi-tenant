using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Purchasing;
using PMS.Application.Features.Suppliers.Commands.CreateSupplier;
using PMS.Application.Features.Suppliers.Commands.RecordPayment;
using PMS.Application.Features.Suppliers.Commands.SetSupplierStatus;
using PMS.Application.Features.Suppliers.Commands.UpdateSupplier;
using PMS.Application.Features.Suppliers.Queries.GetSupplier;
using PMS.Application.Features.Suppliers.Queries.GetSupplierOptions;
using PMS.Application.Features.Suppliers.Queries.GetSupplierPayments;
using PMS.Application.Features.Suppliers.Queries.GetSupplierPurchases;
using PMS.Application.Features.Suppliers.Queries.GetSuppliers;
using PMS.Application.Features.Suppliers.Queries.GetUnsettledPurchases;
using PMS.SharedKernel.Grid;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Who the pharmacy buys from, what they are owed, and what has been paid.
///
/// <para><b>Admin and Pharmacist. An Employee has no access to anything here, at all.</b> That is
/// stricter than most of this system: a cashier can look at stock, at alerts, even at the products
/// they sell — but what the pharmacy pays for its goods, and to whom it owes money, is not counter
/// information. There is no read-only variant of this controller for that reason.</para>
///
/// <para><b>One action is stricter still.</b> Recording a payment is <c>TenantAdminPolicy</c>:
/// financial settlement stays with the owner. A Pharmacist may add a supplier and record a
/// delivery — both are things they are standing there doing — but deciding that money has left the
/// till is not theirs. Deactivating a supplier is Admin too, because it changes what everyone else
/// can buy against.</para>
///
/// <para>No action takes a tenant id. The queries and commands carry <c>ITenantScopedRequest</c>
/// and the global query filter supplies the pharmacy — which is what makes a supplier created in
/// one pharmacy invisible in another, even by direct id.</para>
/// </summary>
[Route("api/suppliers")]
[Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
public class SuppliersController : ApiControllerBase
{
    /// <summary>
    /// One page of suppliers, each with what is still owed to them.
    ///
    /// <para>Search matches name or phone — a pharmacy looks a distributor up by whichever they
    /// can remember, and the number is often the one written on the delivery note.</para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GridResult<SupplierListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search = null,
        [FromQuery] SupplierStatusFilter status = SupplierStatusFilter.Active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SupplierPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSuppliersQuery(search, status, page, pageSize), cancellationToken));

    /// <summary>
    /// Active suppliers for a dropdown.
    ///
    /// <para>Used by the new-purchase picker and, since this module's retrofit, by the Add Stock
    /// form's supplier field — which was free text until Supplier existed.</para>
    /// </summary>
    [HttpGet("options")]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierOptions(
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetSupplierOptionsQuery(), cancellationToken));

    /// <summary>
    /// One supplier, with total purchased, paid, returned and outstanding.
    ///
    /// <para>The outstanding figure comes from <c>ISupplierBalanceQueries</c> — the same service
    /// Module 8's dues report calls, which is what makes the two agree by construction rather
    /// than by luck.</para>
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplier(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetSupplierQuery(id), cancellationToken));

    /// <summary>One page of this supplier's bills, each with its own due and status.</summary>
    [HttpGet("{id:guid}/purchases")]
    [ProducesResponseType(typeof(GridResult<SupplierPurchaseRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierPurchases(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SupplierPaging.DefaultHistoryPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSupplierPurchasesQuery(id, page, pageSize), cancellationToken));

    /// <summary>One page of this supplier's payments, newest first.</summary>
    [HttpGet("{id:guid}/payments")]
    [ProducesResponseType(typeof(GridResult<SupplierPaymentRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierPayments(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SupplierPaging.DefaultHistoryPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSupplierPaymentsQuery(id, page, pageSize), cancellationToken));

    /// <summary>
    /// Bills with something still owed, for the payment form's "link to a purchase" dropdown.
    /// Settled bills are omitted — paying one is an overpayment in the least visible place.
    /// </summary>
    [HttpGet("{id:guid}/unsettled-purchases")]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierPurchaseRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnsettledPurchases(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetUnsettledPurchasesQuery(id), cancellationToken));

    /// <summary>
    /// Adds a supplier. Admin or Pharmacist.
    ///
    /// <para>A pharmacist taking a delivery from a distributor nobody has recorded yet should be
    /// able to record it and book the stock in. Making them wait for an Admin would push them to
    /// the standalone Add Stock screen, and the purchase would never be captured at all.</para>
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SupplierDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] CreateSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetSupplier), new { id = result.Value!.Id }, result.Value)
            : HandleResult(result);
    }

    /// <summary>
    /// Corrects a supplier's details. Admin or Pharmacist.
    ///
    /// <para>Restates no history: purchases reference the supplier by id, so fixing a misspelled
    /// name updates every screen that names them and changes nothing about what was bought.</para>
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplier(
        Guid id,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new UpdateSupplierCommand(
                id, request.Name, request.Phone, request.ContactPerson,
                request.Email, request.Address, request.Company),
            cancellationToken));

    /// <summary>
    /// Stops buying from a supplier. <b>Admin only</b>, and a soft delete.
    ///
    /// <para>Hides them from the pickers. Hides nothing that was already bought — the purchases,
    /// the payments and the balance stay exactly where they were, because deactivating a supplier
    /// the pharmacy still owes money to must not make that debt disappear.</para>
    /// </summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(SupplierDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeactivateSupplier(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new SetSupplierStatusCommand(id, IsActive: false), cancellationToken));

    /// <summary>Resumes buying from a supplier. Admin only.</summary>
    [HttpPatch("{id:guid}/reactivate")]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(SupplierDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReactivateSupplier(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new SetSupplierStatusCommand(id, IsActive: true), cancellationToken));

    /// <summary>
    /// Records money paid to a supplier. <b>Admin only.</b>
    ///
    /// <para>With a <c>purchaseId</c> the payment advances that bill's <c>AmountPaid</c> in the
    /// same transaction. Without one it is a general payment: it reduces what the supplier is
    /// owed overall and modifies no individual bill, because nobody knows which one it settled
    /// and inventing an allocation would turn a display convention into a stored fact.</para>
    ///
    /// <para><b>Money can go either way.</b> <c>direction</c> defaults to <c>Payment</c> — money
    /// out. <c>Refund</c> records a supplier handing a credit back in cash, and <c>WriteOff</c>
    /// gives a credit up with no money moving. Neither of those may name a purchase: a credit
    /// belongs to the account, and settling it against one bill would mean rewriting that bill's
    /// <c>AmountPaid</c>.</para>
    ///
    /// <para><b>An amount that overshoots is warned about, never refused</b>, in both directions.
    /// Overpaying happens — a rounded cash settlement, an advance on the next delivery, a payment
    /// entered against the wrong bill — and blocking it would leave somebody unable to record
    /// money that has genuinely gone. A refund larger than the credit is the same thing mirrored.
    /// The response carries a flag and a sentence saying which situation it left behind.</para>
    /// </summary>
    [HttpPost("{supplierId:guid}/payments")]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(PaymentRecordedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordPayment(
        Guid supplierId,
        [FromBody] RecordPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new RecordSupplierPaymentCommand(
                supplierId, request.PurchaseId, request.Amount,
                request.PaymentDate, request.PaymentMethod, request.Notes, request.Direction),
            cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : HandleResult(result);
    }

    /// <summary>The body of a supplier update; the id comes from the route.</summary>
    public sealed record UpdateSupplierRequest(
        string Name,
        string Phone,
        string? ContactPerson,
        string? Email,
        string? Address,
        string? Company);

    /// <summary>The body of a payment; the supplier comes from the route.</summary>
    /// <param name="Direction">
    /// Omitted means <c>Payment</c>, so a caller written before refunds existed is unchanged.
    /// </param>
    public sealed record RecordPaymentRequest(
        Guid? PurchaseId,
        decimal Amount,
        DateOnly? PaymentDate,
        string? PaymentMethod,
        string? Notes,
        PMS.Domain.Enums.SupplierPaymentDirection Direction
            = PMS.Domain.Enums.SupplierPaymentDirection.Payment);
}
