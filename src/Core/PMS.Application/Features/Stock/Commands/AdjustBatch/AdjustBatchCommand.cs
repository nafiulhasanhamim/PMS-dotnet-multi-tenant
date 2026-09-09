using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Stock.Commands.AdjustBatch;

/// <summary>
/// Changes a batch's quantity, with the reason recorded in the same transaction.
///
/// <para>The only route to a quantity change outside of a sale.</para>
/// </summary>
/// <param name="Quantity">
/// What this means depends on <paramref name="AdjustmentType"/>, and the difference is the
/// point:
/// <list type="bullet">
/// <item><description><see cref="AdjustmentType.Add"/> / <see cref="AdjustmentType.Remove"/> —
/// how much to add or take away.</description></item>
/// <item><description><see cref="AdjustmentType.Correction"/> — <b>the true total</b>. Someone
/// who has just counted the shelf knows "there are 175", not "we are five out"; making them
/// subtract is asking them to do arithmetic that the server can do without error.</description></item>
/// </list>
/// </param>
/// <param name="AcknowledgeExpiringBatch">
/// Required to add stock to a batch that has expired or is close to it.
///
/// <para>The guard exists because of one specific, common and expensive mistake: fresh stock
/// arrives, and instead of creating a batch somebody adds the quantity to the batch already on
/// the screen. The new stock then silently inherits the old batch's expiry date and cost — it
/// will be flagged for disposal months early, or sold as expired, and the margin on it will be
/// wrong either way.</para>
///
/// <para>Enforced here and not only in the page, because a warning that lives in one client is
/// not a guard. A caller that means it says so; one that did not intend it gets told to create
/// a batch instead.</para>
/// </param>
public sealed record AdjustBatchCommand(
    Guid BatchId,
    AdjustmentType AdjustmentType,
    decimal Quantity,
    UnitLevel QuantityUnit,
    string Reason,
    bool AcknowledgeExpiringBatch = false)
    : IRequest<Result<BatchAdjustedDto>>, ITenantScopedRequest;
