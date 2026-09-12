using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Application.Common.Purchasing;
using PMS.Application.Features.Purchases.Commands.CreatePurchase;
using PMS.Application.Features.Purchases.Commands.CreatePurchaseReturn;
using PMS.Application.Features.Purchases.Queries.GetPurchase;
using PMS.Application.Features.Purchases.Queries.GetPurchaseOrigins;
using PMS.Application.Features.Purchases.Queries.GetPurchases;
using PMS.Application.Features.Purchases.Queries.GetReturnableLines;
using PMS.SharedKernel.Grid;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Deliveries against a supplier's bill, and the goods that go back.
///
/// <para><b>Admin and Pharmacist; an Employee has no access.</b> Same reasoning as
/// <c>SuppliersController</c> — what stock cost is not counter information. Unlike payments,
/// recording a purchase and recording a return are both open to a Pharmacist: each is a stock
/// event they are standing there doing, and making them find an Admin first is how stock records
/// stop matching the shelf.</para>
///
/// <para><b>Recording a purchase creates batches.</b> It does so by sending Module 3's
/// <c>CreateBatchCommand</c> once per line, so a delivery booked in here and one entered through
/// Add Stock produce identical stock and obey identical rules — including the duplicate batch
/// number policy, which is the acceptance criterion that proves the two share an
/// implementation.</para>
///
/// <para><b>There is no edit and no delete.</b> A purchase states what a supplier delivered and
/// invoiced, and both the stock and the balance have already moved on that basis. The corrective
/// paths are a purchase return, for goods going back, and a stock adjustment, for a counting
/// error — both of which leave a record rather than quietly replacing the original.</para>
/// </summary>
[Route("api/purchases")]
[Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
public class PurchasesController : ApiControllerBase
{
    /// <summary>
    /// One page of purchases.
    ///
    /// <para>The payment-status filter is applied after the due is computed, because a purchase's
    /// status is not a column — it depends on returns booked against its lines. See
    /// <c>PurchaseQueries</c> for what that costs and why storing the status would be worse.</para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GridResult<PurchaseListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchases(
        [FromQuery] Guid? supplierId = null,
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] PurchaseStatusFilter status = PurchaseStatusFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SupplierPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetPurchasesQuery(supplierId, dateFrom, dateTo, status, page, pageSize),
            cancellationToken));

    /// <summary>One purchase with its lines and the payments recorded against it.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PurchaseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPurchase(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetPurchaseQuery(id), cancellationToken));

    /// <summary>
    /// What can still be sent back from each line.
    ///
    /// <para><b>Capped by two things, and the second is the one that gets forgotten:</b> the bill
    /// limits it — you cannot return more than was delivered, less what has already gone — and so
    /// does the shelf. If the stock was sold or written off it is not there to hand over, whatever
    /// the invoice said.</para>
    ///
    /// <para>Lines with nothing returnable are included rather than filtered out, so the screen
    /// can show them disabled with the reason instead of leaving somebody hunting for a product
    /// they can plainly see on the purchase.</para>
    /// </summary>
    [HttpGet("{id:guid}/returnable-lines")]
    [ProducesResponseType(typeof(ReturnablePurchaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReturnableLines(
        Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetReturnableLinesQuery(id), cancellationToken));

    /// <summary>
    /// Which of these batches arrived on a recorded purchase.
    ///
    /// <para>Module 6's retrofit: the expired-stock page uses this to decide between "Return to
    /// supplier" and "Adjust stock". Batches with no purchase behind them are simply absent from
    /// the response rather than present with nulls.</para>
    /// </summary>
    [HttpGet("origins")]
    [ProducesResponseType(
        typeof(IReadOnlyDictionary<Guid, PurchaseOriginDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrigins(
        [FromQuery(Name = "batchId")] Guid[]? batchId = null,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetPurchaseOriginsQuery(batchId ?? []), cancellationToken));

    /// <summary>
    /// Records a delivery: one purchase, one batch per line, in a single transaction.
    ///
    /// <para>Quantity and price arrive in whatever unit the person was holding — 20 strips at ৳8 a
    /// strip, 2 cartons at ৳4,320 a carton — and Module 3 converts both to base units. A client
    /// doing its own packing arithmetic would be a second implementation of the one calculation in
    /// this system most likely to be got wrong.</para>
    ///
    /// <para><b>Any failure rolls back everything.</b> A bad third line takes the first two
    /// batches, the purchase and its allocated number with it: no orphan stock, no gap in the
    /// sequence.</para>
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PurchaseCreatedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePurchase(
        [FromBody] CreatePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(
                nameof(GetPurchase), new { id = result.Value!.PurchaseId }, result.Value)
            : HandleResult(result);
    }

    /// <summary>
    /// Sends goods back against one line, in a single transaction.
    ///
    /// <para>Three things happen together: the return is recorded, a stock adjustment is written
    /// with its reason auto-tagged "Purchase return: …", and the batch quantity comes down. The
    /// adjustment is not optional — a batch's quantity can only be changed by the domain method
    /// that produces one.</para>
    ///
    /// <para><b>What does not change is the bill's <c>AmountPaid</c>.</b> A return does not un-pay
    /// money already handed over; it reduces what is still owed. A bill paid in full and then
    /// returned against goes to a negative balance, which is the truthful answer.</para>
    /// </summary>
    [HttpPost("{purchaseId:guid}/returns")]
    [ProducesResponseType(typeof(PurchaseReturnedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReturn(
        Guid purchaseId,
        [FromBody] CreatePurchaseReturnRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new CreatePurchaseReturnCommand(
                purchaseId, request.PurchaseLineId, request.Quantity,
                request.QuantityUnit, request.Reason),
            cancellationToken));

    /// <summary>The body of a purchase return; the purchase comes from the route.</summary>
    public sealed record CreatePurchaseReturnRequest(
        Guid PurchaseLineId,
        decimal Quantity,
        PMS.Domain.Enums.UnitLevel QuantityUnit,
        string Reason);
}
