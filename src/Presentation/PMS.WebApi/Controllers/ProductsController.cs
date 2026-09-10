using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Application.Features.Products.Commands.BulkImportProducts;
using PMS.Application.Features.Products.Commands.CreateProduct;
using PMS.Application.Features.Products.Commands.SetProductPrices;
using PMS.Application.Features.Products.Commands.SetProductActive;
using PMS.Application.Features.Products.Commands.UpdateProduct;
using PMS.Application.Features.Products.Queries.GetProduct;
using PMS.Application.Features.Products.Queries.GetProducts;
using PMS.Application.Features.Sales.Queries.GetSellableProducts;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Interfaces;
using PMS.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>
/// A pharmacy's own product catalogue.
///
/// <para>Every action is tenant-scoped and none of them mentions a tenant. The commands and
/// queries carry <c>ITenantScopedRequest</c>, so the pipeline refuses to run one without a
/// resolved pharmacy, and the global query filter supplies the WHERE.</para>
///
/// <para>Access differs by exactly one action. Admin and Pharmacist may both add and edit —
/// the Pharmacist is the person who knows what the pharmacy stocks. Only an Admin may
/// deactivate, because that changes what everyone else can sell. An Employee may only
/// look.</para>
/// </summary>
[Route("api/products")]
[Authorize(Policy = AuthenticationExtensions.TenantUserPolicy)]
public class ProductsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public ProductsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// Whether the caller may see prices in a list.
    ///
    /// <para>An Employee may not. Decided here from the token's role and passed into the
    /// query, which then never reads the price column — so the value does not cross the wire
    /// at all. Sending it and asking the UI to hide a column would not be a control: anyone
    /// can open the network tab.</para>
    ///
    /// <para>Detail pages are different and deliberately so: an Employee at the counter needs
    /// to tell a customer what something costs. What they must not have is the whole price
    /// list in one download.</para>
    /// </summary>
    private bool CallerMaySeeListPrices =>
        _currentUser.TenantRole() is UserRole.Admin or UserRole.Pharmacist;

    /// <summary>One page of products, for either of the two list screens.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(GridResult<ProductListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] ProductListType type,
        [FromQuery] string? search = null,
        [FromQuery] ProductStatusFilter status = ProductStatusFilter.Active,
        [FromQuery] bool antibioticOnly = false,
        [FromQuery] ProductType? productType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(
            new GetProductsQuery(
                type, search, status, antibioticOnly, productType,
                CallerMaySeeListPrices, page, pageSize),
            cancellationToken));

    /// <summary>
    /// The billing screen type-ahead: products that can be sold, and the reason for the ones
    /// that cannot.
    ///
    /// <para><b>Unsellable products are returned with a reason rather than filtered out.</b>
    /// A cashier who types "napa" and sees nothing tells the customer the pharmacy does not
    /// stock it. One who sees it greyed with "needs prices set" fetches whoever can fix that in
    /// fifteen seconds, and one who sees "requires a pharmacist" calls a pharmacist over.
    /// Silence is the only outcome that loses the sale and teaches nobody anything. The single
    /// exception is a deactivated product, which is absent: deactivating one is how a pharmacy
    /// says it does not sell the thing at all.</para>
    ///
    /// <para>Returns each product's unit configuration and current prices alongside, so a cart
    /// row can be priced and its unit dropdown built without a second request. Those prices are
    /// the live ones — the snapshot that matters is taken by the sale, not by this.</para>
    ///
    /// <para>Antibiotics are flagged rather than hidden for an Employee, and the caller cannot
    /// ask to be treated otherwise: the role comes from the token.</para>
    /// </summary>
    [HttpGet("sellable")]
    [ProducesResponseType(typeof(IReadOnlyList<SellableProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSellable(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 0,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSellableProductsQuery(search, limit), cancellationToken));

    /// <summary>
    /// One product in full. 404 for another pharmacy's id — it is genuinely absent behind the
    /// query filter, and a 404 also reveals nothing about whether that id exists elsewhere.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetProductQuery(id), cancellationToken));

    /// <summary>
    /// Adds a product. Admin or Pharmacist.
    ///
    /// One endpoint for both paths: a manual entry sends no CatalogMedicineId, an import from
    /// the reference catalogue sends one. Same validation, same write path.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleCreatedResult(result, nameof(GetProduct), p => new { id = p.Id });
    }

    /// <summary>
    /// Imports many catalogue medicines at once. Admin or Pharmacist.
    ///
    /// <para><b>All or nothing.</b> Every row is validated independently and, if any fails,
    /// nothing is written — the response carries a result per row so a review grid can say
    /// which ones to fix. A half-imported catalogue is the outcome this rules out: the
    /// pharmacy would have no way to tell which of two hundred medicines arrived, and a second
    /// attempt would collide with whatever the first one managed.</para>
    ///
    /// <para>Rows may omit prices, which creates the product with <c>IsSetupComplete</c>
    /// false. That is a deliberate offer for onboarding, not a validation gap — such a product
    /// cannot be sold until its prices are set.</para>
    ///
    /// <para>200 rows per request. Over that is a 400 naming the cap.</para>
    /// </summary>
    [HttpPost("bulk-import")]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(BulkImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BulkImport(
        [FromBody] BulkImportProductsCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return HandleResult(result);
        }

        // 200 rather than 201 even on success, and 200 with Succeeded=false when rows failed.
        //
        // A 201 would have to name a location, and there is no single resource here - two
        // hundred were created. And a per-row failure is not a malformed request: the client
        // sent something well-formed that the pharmacy's own data refused, and the body is the
        // useful part of the answer. A 400 would invite clients to discard it.
        return Ok(result.Value);
    }

    /// <summary>
    /// Sets prices on products that already exist, several at a time. Admin or Pharmacist.
    ///
    /// <para>Behind the complete-setup screen. Touches prices and nothing else — see the
    /// command for why this is not UpdateProduct in a loop.</para>
    /// </summary>
    [HttpPost("prices")]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(BulkImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetPrices(
        [FromBody] SetProductPricesCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleResult(result);
    }

    /// <summary>Edits a product. Admin or Pharmacist.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthenticationExtensions.TenantWriterPolicy)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new UpdateProductCommand(
                id,
                request.ProductType,
                request.BrandName,
                request.Company,
                request.Category,
                request.GenericName,
                request.Strength,
                request.DosageForm,
                request.IsAntibiotic,
                request.BaseUnitName,
                request.MidUnitName,
                request.LargeUnitName,
                request.BasePerMid,
                request.MidPerLarge,
                request.PricePerBase,
                request.PricePerMid,
                request.PricePerLarge,
                request.ReorderLevel,
                request.ShelfLocation),
            cancellationToken));

    /// <summary>
    /// Soft-deletes a product. <b>Admin only.</b>
    ///
    /// Nothing is removed: existing stock, past purchases and past sales all still point at
    /// this row. It only leaves default lists and new transactions.
    /// </summary>
    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new SetProductActiveCommand(id, false), cancellationToken));

    /// <summary>Brings a product back. Admin only.</summary>
    [HttpPatch("{id:guid}/reactivate")]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new SetProductActiveCommand(id, true), cancellationToken));
}

/// <summary>
/// The update body. The id comes from the route, so it is not repeated here — two ids that
/// could disagree is a bug waiting to be written.
///
/// CatalogMedicineId is also absent: the link records where a product came from, and an edit
/// does not rewrite that history.
/// </summary>
public sealed record UpdateProductRequest(
    ProductType ProductType,
    string BrandName,
    string? Company,
    string? Category,
    string? GenericName,
    string? Strength,
    string? DosageForm,
    bool IsAntibiotic,
    string BaseUnitName,
    string? MidUnitName,
    string? LargeUnitName,
    int? BasePerMid,
    int? MidPerLarge,
    decimal? PricePerBase,
    decimal? PricePerMid,
    decimal? PricePerLarge,
    int ReorderLevel,
    string? ShelfLocation);
