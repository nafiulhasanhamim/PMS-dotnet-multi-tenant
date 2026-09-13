using PMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("StockAdjustments");
        builder.HasKey(a => a.Id);

        // Tenant filter and stamping by convention — StockAdjustment implements ITenantEntity.

        builder.Property(a => a.AdjustmentType).IsRequired().HasConversion<int>();
        builder.Property(a => a.QuantityChangeInBaseUnits).IsRequired();
        builder.Property(a => a.QuantityAfterInBaseUnits).IsRequired();
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(500);
        builder.Property(a => a.AdjustedByUserId).IsRequired();

        // The relationship is configured from the Batch side, where the backing-field access
        // mode also has to be set. Declaring it again here would be a second, competing
        // definition of the same foreign key.

        // No navigation to User, and no constraint here either.
        //
        // Users is a global table with no TenantId — a person can work at two pharmacies — so
        // a navigation from this tenant-scoped row would join filtered data to unfiltered data
        // on every read of the history. The name is resolved by an explicit join in the query
        // instead, which keeps that crossing visible rather than implicit.
        builder.Property(a => a.AdjustedByUserId);

        // The history query: one batch, newest first. CreatedOnUtc is in the index so the
        // ordering is served by it rather than sorted afterwards.
        builder.HasIndex(a => new { a.TenantId, a.BatchId, a.CreatedOnUtc })
            .HasDatabaseName("IX_StockAdjustments_Tenant_Batch_CreatedOnUtc");
    }
}
