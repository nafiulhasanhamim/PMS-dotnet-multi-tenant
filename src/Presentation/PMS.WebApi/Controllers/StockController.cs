using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Application.Features.Stock.Commands.AdjustBatch;
using PMS.Application.Features.Stock.Commands.CreateBatch;
using PMS.Application.Features.Stock.Commands.UpdateBatch;
using PMS.Application.Features.Stock.Queries.GetBatch;
using PMS.Application.Features.Stock.Queries.GetBatchAdjustments;
using PMS.Application.Features.Stock.Queries.GetProductStock;
using PMS.Application.Features.Stock.Queries.GetStock;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Interfaces;
using PMS.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>
/// A pharmacy's physical stock: batches, their quantities, and the record of every change.
///
/// <para>Every action is tenant-scoped and none of them mentions a tenant. The commands and
/// queries carry <c>ITenantScopedRequest</c>, so the pipeline refuses to run one without a
/// resolved pharmacy, and the global query filter supplies the WHERE — including inside the
/// aggregates behind the stock list.</para>
///
/// <para><b>Access.</b> Admin and Pharmacist are identical here: a pharmacist is the person
/// who takes a delivery in and who counts a shelf, so a module they could only read would be
/// unusable. An Employee may look and may not touch, and one thing is withheld from them
/// outright — what the pharmacy paid.</para>
/// </summary>
[Route("api/stock")]
[Authorize(Policy = AuthenticationExtensions.TenantUserPolicy)]
public class StockController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public StockController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// Whether the caller may see what stock cost.
    ///
    /// <para>An Employee may not, and this is the only place that decides it. The flag is
    /// passed into the query, whose projection then never reads the column — so the figure
    /// does not cross the wire. Sending it and asking the UI to hide a column would not be a
    /// control at all: anyone can open the network tab.</para>
    ///
    /// <para>Note this is stricter than the product module, where an Employee sees the sale
    /// price on a detail page because somebody at the counter has to answer "how much is
    /// this?". Nobody at the counter needs the cost, and the margin it reveals is the
    /// pharmacy's own business.</para>
    /// </summary>
    private bool CallerMaySeePurchasePrices =>
        _currentUser.TenantRole() is UserRole.Admin or UserRole.Pharmacist;

    /// <summary>
    /// One page of the stock list: a row per product, aggregated across its batches.
    ///
    /// <para>No prices of any kind appear in this result, so there is nothing here to withhold
    /// by role.</para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GridResult<StockListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStock(
        [FromQuery] string? search = null,
        [FromQuery] StockStatusFilter stockStatus = StockStatusFilter.All,
        [FromQuery] ExpiryStatusFilter expiryStatus = ExpiryStatusFilter.All,
        [FromQuery] StockProductTypeFilter productType = StockProductTypeFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(
            new GetStockQuery(search, stockStatus, expiryStatus, productType, page, pageSize),
            cancellationToken));

    /// <summary>
    /// One product's stock: summary, live batches in FEFO order, and a page of depleted ones.
    /// </summary>
    [HttpGet("product/{productId:guid}")]
    [ProducesResponseType(typeof(ProductStockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductStock(
        Guid productId,
        [FromQuery] int depletedPage = 1,
        [FromQuery] int depletedPageSize = 10,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetProductStockQuery(
                productId, CallerMaySeePurchasePrices, depletedPage, depletedPageSize),
            cancellationToken));

    /// <summary>
    /// One batch. 404 for another pharmacy's id — behind the query filter the row genuinely is
    /// not there, and a 404 also reveals nothing about whether it exists elsewhere.
    /// </summary>
    [HttpGet("batch/{batchId:guid}")]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBatch(
        Guid batchId, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetBatchQuery(batchId, CallerMaySeePurchasePrices), cancellationToken));

    /// <summary>
    /// A batch's adjustment history.
    ///
    /// <b>Admin or Pharmacist.</b> The one read in this module an Employee cannot make: the
    /// history is who wrote off what and why, which is staff-conduct information rather than
    /// stock information.
    /// </summary>
    [HttpGet("batch/{batchId:guid}/adjustments")]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(GridResult<StockAdjustmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatchAdjustments(
        Guid batchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(
            new GetBatchAdjustmentsQuery(batchId, page, pageSize), cancellationToken));

    /// <summary>
    /// Records a delivery. Admin or Pharmacist.
    ///
    /// <para>Quantity and price arrive in whatever unit the person was holding, with the level
    /// alongside; the handler converts through Module 2's helpers before persisting. A cost
    /// above the product's sale price comes back as a warning flag rather than a rejection —
    /// see <c>BatchCreatedDto</c>.</para>
    /// </summary>
    [HttpPost("batches")]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(BatchCreatedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBatch(
        [FromBody] CreateBatchCommand command, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleCreatedResult(result, nameof(GetBatch), b => new { batchId = b.Batch.Id });
    }

    /// <summary>
    /// Corrects a batch's details. Admin or Pharmacist.
    ///
    /// <para>Quantity is not editable here, and an attempt to change it is refused rather than
    /// ignored — a silently dropped field would let a client believe it had worked.</para>
    /// </summary>
    [HttpPut("batches/{id:guid}")]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(BatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBatch(
        Guid id,
        [FromBody] UpdateBatchRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new UpdateBatchCommand(
                id,
                request.BatchNumber,
                request.ExpiryDate,
                request.ManufactureDate,
                request.PurchasePrice,
                request.PurchasePriceUnit,
                request.SupplierId,
                request.SupplierNameText,
                request.Notes,
                request.QuantityInBaseUnits),
            cancellationToken));

    /// <summary>
    /// Changes a batch's quantity, with a reason. Admin or Pharmacist.
    ///
    /// <para>One transaction writes the new quantity and the adjustment row that explains it;
    /// neither happens without the other.</para>
    /// </summary>
    [HttpPost("batches/{id:guid}/adjust")]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(BatchAdjustedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdjustBatch(
        Guid id,
        [FromBody] AdjustBatchRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new AdjustBatchCommand(
                id,
                request.AdjustmentType,
                request.Quantity,
                request.QuantityUnit,
                request.Reason,
                request.AcknowledgeExpiringBatch),
            cancellationToken));
}

/// <summary>
/// The edit body. The id comes from the route, so it is not repeated — two ids that could
/// disagree is a bug waiting to be written.
/// </summary>
/// <param name="QuantityInBaseUnits">
/// Optional, and present only so that sending a <em>different</em> quantity can be refused
/// with a message pointing at the adjustment endpoint. Sending the current value, as a client
/// echoing the whole object would, is accepted as the no-op it is.
/// </param>
public sealed record UpdateBatchRequest(
    string BatchNumber,
    DateOnly? ExpiryDate,
    DateOnly? ManufactureDate,
    decimal PurchasePrice,
    UnitLevel PurchasePriceUnit,
    Guid? SupplierId,
    string? SupplierNameText,
    string? Notes,
    int? QuantityInBaseUnits);

/// <param name="Quantity">
/// For Add and Remove, how much to move. For Correction, <b>the true total</b> — the server
/// works out the difference, because somebody who has just counted a shelf knows what is on
/// it, not how far out the record was.
/// </param>
/// <param name="AcknowledgeExpiringBatch">
/// Required to add stock to a batch at or near its expiry date. See the command for the
/// mistake this catches.
/// </param>
public sealed record AdjustBatchRequest(
    AdjustmentType AdjustmentType,
    decimal Quantity,
    UnitLevel QuantityUnit,
    string Reason,
    bool AcknowledgeExpiringBatch = false);
