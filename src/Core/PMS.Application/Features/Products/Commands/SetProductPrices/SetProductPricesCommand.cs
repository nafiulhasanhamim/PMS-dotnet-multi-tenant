using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.SetProductPrices;

/// <summary>
/// Sets prices on products that already exist, several at a time.
///
/// <para><b>Why this exists rather than reusing UpdateProduct.</b> The complete-setup screen is
/// a grid of products missing prices, saved in one action. Doing that through UpdateProduct
/// would be one request per row, each carrying the product's entire definition — every unit
/// name, count, category and shelf location — round-tripped for the sake of one number. Any of
/// those fields could be corrupted by a stale form in the process, and a partial failure would
/// leave the grid in a state nobody could read.</para>
///
/// <para>It touches prices and nothing else, which is also what makes it safe to send from a
/// screen that never loaded the rest of the product.</para>
/// </summary>
public sealed record SetProductPricesCommand(IReadOnlyList<ProductPriceUpdate> Items)
    : IRequest<Result<BulkImportResultDto>>, ITenantScopedRequest;

/// <param name="PricePerBase">
/// Required here, unlike in the bulk import. This command's entire purpose is to complete a
/// setup, so a null would be a request to do nothing — and the screen that sends it will not
/// enable its save button until every field is filled.
/// </param>
public sealed record ProductPriceUpdate(
    Guid ProductId,
    decimal PricePerBase,
    decimal? PricePerMid,
    decimal? PricePerLarge);
