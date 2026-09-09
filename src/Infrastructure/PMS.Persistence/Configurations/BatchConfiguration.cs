using PMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

public sealed class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("Batches");
        builder.HasKey(b => b.Id);

        // No HasQueryFilter here. Batch implements ITenantEntity, so ApplicationDbContext's
        // convention pass adds the tenant filter and TenantEntityInterceptor stamps TenantId
        // on insert. One written here would REPLACE the convention's filter, not add to it.

        builder.Property(b => b.BatchNumber).IsRequired().HasMaxLength(100);

        // DateOnly, mapped to SQL Server 'date'. A batch expires on a day, not at an instant,
        // and a datetime column here would invite a time component that no pack prints and
        // that a time-zone conversion could then shift across a day boundary.
        builder.Property(b => b.ExpiryDate).HasColumnType("date");
        builder.Property(b => b.ManufactureDate).HasColumnType("date");

        // 18,4 to match the product's prices. A per-base-unit cost derived from a bulk pack
        // is frequently not a round number of paisa, and rounding it at rest would misstate
        // margin on every sale from the batch.
        builder.Property(b => b.PurchasePricePerBaseUnit).IsRequired().HasPrecision(18, 4);

        builder.Property(b => b.QuantityInBaseUnits).IsRequired();
        builder.Property(b => b.InitialQuantityInBaseUnits).IsRequired();
        builder.Property(b => b.IsActive).IsRequired();

        builder.Property(b => b.SupplierNameText).HasMaxLength(200);
        builder.Property(b => b.Notes).HasMaxLength(1000);

        // No navigation and no constraint: the Supplier entity arrives in Module 4. The column
        // exists now so that migration adds a foreign key to existing data rather than adding
        // a column to a table already full of rows that cannot populate it.
        builder.Property(b => b.SupplierId);

        builder.HasOne(b => b.Product)
            .WithMany()
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, not Cascade. Deleting a product out from under its stock would erase the
        // record of what was bought and sold; the product's own delete is a soft one anyway.

        builder.HasMany(b => b.Adjustments)
            .WithOne(a => a.Batch)
            .HasForeignKey(a => a.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // The collection is exposed as IReadOnlyCollection over a private List, so EF has to
        // be told to go through the field. Without this it tries to add to the read-only
        // property and the adjustment appended by Batch.Adjust would never be discovered.
        builder.Metadata
            .FindNavigation(nameof(Batch.Adjustments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Uniqueness is per pharmacy and per product: two pharmacies may hold the same
        // manufacturer's batch, and one pharmacy may hold batch "A1" of two different
        // products. What it forbids is the same number twice for one product, which is what
        // makes "recall batch B-100 of Napa" name exactly one row. See the duplicate-number
        // policy in docs/03-batches-and-stock.md.
        builder.HasIndex(b => new { b.TenantId, b.ProductId, b.BatchNumber })
            .IsUnique()
            .HasDatabaseName("UX_Batches_Tenant_Product_BatchNumber");

        // The FEFO index. Leads with TenantId so the seek lands inside one pharmacy, then
        // ProductId, then expiry — which is the order every stock read asks for.
        //
        // It does not remove the sort: the ordering puts nulls last with an explicit key, and
        // an index cannot be read in that order. It does not need to — a product holds a
        // handful of live batches, and what the index is actually for is finding those rows
        // without touching another product's, and answering the expiry-window filters on the
        // stock list without a scan of every batch in the pharmacy.
        builder.HasIndex(b => new { b.TenantId, b.ProductId, b.ExpiryDate })
            .HasDatabaseName("IX_Batches_Tenant_Product_ExpiryDate");
    }
}
