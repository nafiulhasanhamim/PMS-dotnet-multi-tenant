using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Persistence.Configurations;

public sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> builder)
    {
        builder.ToTable("SaleLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.SaleId).IsRequired();
        builder.Property(l => l.ProductId).IsRequired();
        builder.Property(l => l.BatchId).IsRequired();
        builder.Property(l => l.QuantityInBaseUnits).IsRequired();
        builder.Property(l => l.UnitSold).IsRequired().HasConversion<int>();

        // The price snapshot, at 18,4 rather than the 18,2 used on the sale's totals. It is a
        // copy of the product's price, and a product's price per base unit can be genuinely
        // fractional when it was set from a pack — a strip of three at 10 taka. Rounding the
        // snapshot would make a historical invoice disagree with the price that was actually
        // charged, which is the one thing this column exists to prevent.
        builder.Property(l => l.UnitSalePrice).IsRequired().HasPrecision(18, 4);

        // Cash figures, so 18,2: these are what the customer paid and what a refund comes off.
        builder.Property(l => l.LineTotal).IsRequired().HasPrecision(18, 2);
        builder.Property(l => l.DiscountShare).IsRequired().HasPrecision(18, 2);
        builder.Property(l => l.NetLineTotal).IsRequired().HasPrecision(18, 2);

        builder.HasOne(l => l.Product)
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict on both of these. Deleting a product or a batch out from under a sale line
        // would erase the record of what was sold and where it came from — and the batch
        // reference is what a return reverses, so losing it would leave stock with nowhere to
        // go back to. Products are soft-deleted anyway.
        builder.HasOne(l => l.Batch)
            .WithMany()
            .HasForeignKey(l => l.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Returns)
            .WithOne(r => r.SaleLine)
            .HasForeignKey(r => r.SaleLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(SaleLine.Returns))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Reading a sale reads its lines, always.
        builder.HasIndex(l => new { l.TenantId, l.SaleId })
            .HasDatabaseName("IX_SaleLines_Tenant_Sale");

        // "What did we sell of this product, and out of which batch" — Module 8's profit
        // reporting joins lines to batches for the purchase cost, and the expiry work in
        // Module 6 asks the same question the other way round.
        builder.HasIndex(l => new { l.TenantId, l.ProductId })
            .HasDatabaseName("IX_SaleLines_Tenant_Product");

        builder.HasIndex(l => new { l.TenantId, l.BatchId })
            .HasDatabaseName("IX_SaleLines_Tenant_Batch");
    }
}
