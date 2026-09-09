using PMS.Domain.Entities;
using PMS.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Domain;

/// <summary>
/// The invariants of a batch.
///
/// <para>Most of these guard rules that are also enforced in validation and again by a CHECK
/// constraint. That is not redundancy for its own sake: the validator produces a message on a
/// form, the constraint is the last line against a bug in a future deduction path, and this
/// layer is what makes the rule impossible to bypass by calling the entity directly — which
/// is exactly what Module 5 will be doing.</para>
/// </summary>
public class BatchTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Batch Batch(int quantity = 200, DateOnly? expiry = null) =>
        new(ProductId, "B-100", expiry, manufactureDate: null,
            purchasePricePerBaseUnit: 0.80m, quantityInBaseUnits: quantity,
            supplierId: null, supplierNameText: null, notes: null);

    // ── InitialQuantityInBaseUnits ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsInitialQuantityToTheArrivingQuantity()
    {
        var batch = Batch(quantity: 200);

        batch.QuantityInBaseUnits.Should().Be(200);
        batch.InitialQuantityInBaseUnits.Should().Be(200);
    }

    [Fact]
    public void Adjust_NeverChangesInitialQuantity()
    {
        var batch = Batch(quantity: 200);

        batch.Adjust(AdjustmentType.Remove, -20, "damage in transit", UserId);
        batch.Adjust(AdjustmentType.Add, 5, "found behind the counter", UserId);
        batch.Adjust(AdjustmentType.Correction, -30, "counted 155", UserId);

        // The denominator for every turnover and wastage figure. If an adjustment could move
        // it, "how much of this delivery did we actually sell" would become unanswerable and
        // no error would ever be raised.
        batch.InitialQuantityInBaseUnits.Should().Be(200);
        batch.QuantityInBaseUnits.Should().Be(155);
    }

    [Fact]
    public void UpdateDetails_DoesNotTouchEitherQuantity()
    {
        var batch = Batch(quantity: 200);
        batch.Adjust(AdjustmentType.Remove, -25, "breakage", UserId);

        batch.UpdateDetails(
            "B-100-REV", new DateOnly(2027, 1, 31), new DateOnly(2025, 1, 31),
            0.95m, supplierId: null, supplierNameText: "Square", notes: "relabelled");

        batch.QuantityInBaseUnits.Should().Be(175);
        batch.InitialQuantityInBaseUnits.Should().Be(200);
        batch.BatchNumber.Should().Be("B-100-REV");
    }

    // ── The quantity floor ──────────────────────────────────────────────────────────────

    [Fact]
    public void Adjust_RefusesToTakeQuantityBelowZero()
    {
        var batch = Batch(quantity: 10);

        var act = () => batch.Adjust(AdjustmentType.Remove, -11, "wishful thinking", UserId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot*negative*");

        // And nothing moved. A guard that threw after mutating would leave the entity holding
        // an impossible quantity for whatever the caller did next.
        batch.QuantityInBaseUnits.Should().Be(10);
    }

    [Fact]
    public void Adjust_AllowsQuantityToReachExactlyZero()
    {
        var batch = Batch(quantity: 10);

        batch.Adjust(AdjustmentType.Remove, -10, "expired disposal", UserId);

        batch.QuantityInBaseUnits.Should().Be(0);
        batch.IsDepleted.Should().BeTrue();

        // Depleted, but still active and still visible. The flag is for a row entered in
        // error, not for one that sold out.
        batch.IsActive.Should().BeTrue();
    }

    // ── The audit row cannot be skipped ─────────────────────────────────────────────────

    [Fact]
    public void Adjust_ReturnsTheAdjustmentThatExplainsTheChange()
    {
        var batch = Batch(quantity: 200);

        var adjustment = batch.Adjust(AdjustmentType.Remove, -20, "damage in transit", UserId);

        adjustment.BatchId.Should().Be(batch.Id);
        adjustment.AdjustmentType.Should().Be(AdjustmentType.Remove);
        adjustment.QuantityChangeInBaseUnits.Should().Be(-20);
        adjustment.QuantityBeforeInBaseUnits.Should().Be(200);
        adjustment.QuantityAfterInBaseUnits.Should().Be(180);
        adjustment.Reason.Should().Be("damage in transit");
        adjustment.AdjustedByUserId.Should().Be(UserId);
    }

    [Fact]
    public void Adjust_RecordsEveryChangeInTheBatchHistory()
    {
        var batch = Batch(quantity: 200);

        batch.Adjust(AdjustmentType.Remove, -20, "damage", UserId);
        batch.Adjust(AdjustmentType.Correction, -5, "counted 175", UserId);

        // Two changes, two rows. There is no method on Batch that moves the quantity without
        // adding to this collection, which is what makes "a silent quantity change" not a
        // mistake a handler can make.
        batch.Adjustments.Should().HaveCount(2);
        batch.Adjustments.Select(a => a.QuantityAfterInBaseUnits).Should().Equal(180, 175);
    }

    [Fact]
    public void Adjust_TrimsTheReason()
    {
        var batch = Batch();

        var adjustment = batch.Adjust(AdjustmentType.Remove, -1, "   breakage   ", UserId);

        adjustment.Reason.Should().Be("breakage");
    }

    // ── Expiry ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10, false)]   // expires in ten days
    [InlineData(0, false)]    // expires today: good until the end of the printed day
    [InlineData(-1, true)]    // yesterday
    public void IsExpiredAsOf_TreatsTheExpiryDayItselfAsGood(int daysFromToday, bool expected)
    {
        var today = new DateOnly(2026, 6, 15);
        var batch = Batch(expiry: today.AddDays(daysFromToday));

        batch.IsExpiredAsOf(today).Should().Be(expected);
    }

    [Fact]
    public void IsExpiredAsOf_IsNeverTrueForStockThatDoesNotExpire()
    {
        var batch = Batch(expiry: null);

        batch.IsExpiredAsOf(new DateOnly(2099, 1, 1)).Should().BeFalse();
        batch.DaysUntilExpiry(new DateOnly(2099, 1, 1)).Should().BeNull();
    }

    [Fact]
    public void DaysUntilExpiry_GoesNegativeOnceExpired()
    {
        var today = new DateOnly(2026, 6, 15);
        var batch = Batch(expiry: today.AddDays(-30));

        // Negative rather than clamped at zero, so one comparison against the alert window
        // serves both "expiring soon" and "already expired".
        batch.DaysUntilExpiry(today).Should().Be(-30);
    }

    // ── Trimming and blanking, matching Product ─────────────────────────────────────────

    [Fact]
    public void Constructor_TrimsTheBatchNumberAndBlanksEmptyText()
    {
        var batch = new Batch(
            ProductId, "  B-100  ", expiryDate: null, manufactureDate: null,
            purchasePricePerBaseUnit: 1m, quantityInBaseUnits: 1,
            supplierId: null, supplierNameText: "   ", notes: "");

        batch.BatchNumber.Should().Be("B-100");
        batch.SupplierNameText.Should().BeNull();
        batch.Notes.Should().BeNull();
    }
}
