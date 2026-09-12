using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Persistence.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 4: suppliers, purchases, payments and purchase returns.
//
// None of these declares HasQueryFilter. Every one implements ITenantEntity, so
// ApplicationDbContext's convention pass adds the tenant filter and TenantEntityInterceptor
// stamps TenantId on insert. A filter written here would REPLACE the convention's rather than
// combine with it — the trap Module 2 documented.
//
// Money precision follows the rule the rest of the system uses: DECIMAL(18,2) for cash that
// changed hands, DECIMAL(18,4) for a per-base-unit price derived from a pack, which is genuinely
// fractional and would misstate a total if rounded at rest.
// ═══════════════════════════════════════════════════════════════════════════════════════════

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Phone).IsRequired().HasMaxLength(40);
        builder.Property(s => s.ContactPerson).HasMaxLength(200);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.Company).HasMaxLength(200);
        builder.Property(s => s.IsActive).IsRequired();

        // Not unique. Two distributors can genuinely share a name, and a pharmacy that has
        // recorded the same supplier twice by accident has a data-tidying problem, not a
        // constraint violation — refusing the second would leave them unable to record a real
        // delivery. The list screen's search is what surfaces the duplicate.
        builder.HasIndex(s => new { s.TenantId, s.Name })
            .HasDatabaseName("IX_Suppliers_Tenant_Name");

        // Phone is how a pharmacy actually looks a supplier up when the trading name on the
        // delivery note is not the name they filed them under.
        builder.HasIndex(s => new { s.TenantId, s.Phone })
            .HasDatabaseName("IX_Suppliers_Tenant_Phone");
    }
}

public sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PurchaseNumber).IsRequired().HasMaxLength(40);
        builder.Property(p => p.PurchaseDate).IsRequired().HasColumnType("date");
        builder.Property(p => p.CreatedByUserId).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.Property(p => p.TotalAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(p => p.AmountPaid).IsRequired().HasPrecision(18, 2);

        builder.HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            // Restrict, not Cascade. A supplier is soft-deleted, never removed, so this should
            // never fire — and if something ever tried, taking a pharmacy's purchase history with
            // it is the worst available outcome.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Lines)
            .WithOne(l => l.Purchase)
            .HasForeignKey(l => l.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // The collection is IReadOnlyCollection over a private List, so EF must be told to go
        // through the backing field. Without it, lines appended by Purchase.AddLine are never
        // discovered and the purchase saves with no lines and nothing complains — the same trap
        // SaleConfiguration documents.
        builder.Metadata
            .FindNavigation(nameof(Purchase.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Per pharmacy. Two pharmacies both have a PUR-000001; what this forbids is one pharmacy
        // issuing the same number twice, and it is the backstop under PurchaseNumberGenerator's
        // atomic allocation.
        builder.HasIndex(p => new { p.TenantId, p.PurchaseNumber })
            .IsUnique()
            .HasDatabaseName("UX_Purchases_Tenant_PurchaseNumber");

        // The two filters the purchases list offers, and the join the balance service makes.
        builder.HasIndex(p => new { p.TenantId, p.PurchaseDate })
            .HasDatabaseName("IX_Purchases_Tenant_PurchaseDate");

        builder.HasIndex(p => new { p.TenantId, p.SupplierId })
            .HasDatabaseName("IX_Purchases_Tenant_Supplier");
    }
}

public sealed class PurchaseLineConfiguration : IEntityTypeConfiguration<PurchaseLine>
{
    public void Configure(EntityTypeBuilder<PurchaseLine> builder)
    {
        builder.ToTable("PurchaseLines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.QuantityInBaseUnits).IsRequired();
        builder.Property(l => l.PurchasePricePerBaseUnit).IsRequired().HasPrecision(18, 4);
        builder.Property(l => l.LineTotal).IsRequired().HasPrecision(18, 2);

        builder.HasOne(l => l.Product)
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Batch)
            .WithMany()
            .HasForeignKey(l => l.BatchId)
            // Restrict, deliberately. Deleting a batch out from under a purchase line would
            // erase the record of what was delivered and invoiced — the same reasoning that keeps
            // SaleLine's batch reference restricted.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Returns)
            .WithOne(r => r.PurchaseLine)
            .HasForeignKey(r => r.PurchaseLineId)
            .OnDelete(DeleteBehavior.Restrict);

        // IReadOnlyCollection over a private List, so EF must go through the backing field.
        builder.Metadata
            .FindNavigation(nameof(PurchaseLine.Returns))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Module 6's retrofit asks "did this batch come from a purchase?" for every row on the
        // expired-stock page. Without this index that is a scan of every purchase line the
        // pharmacy has ever recorded, once per expired batch.
        builder.HasIndex(l => new { l.TenantId, l.BatchId })
            .HasDatabaseName("IX_PurchaseLines_Tenant_Batch");

        builder.HasIndex(l => new { l.TenantId, l.ProductId })
            .HasDatabaseName("IX_PurchaseLines_Tenant_Product");
    }
}

public sealed class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> builder)
    {
        builder.ToTable("SupplierPayments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).IsRequired().HasPrecision(18, 2);

        // Stored as an int, defaulting to Payment — so every row written before refunds existed
        // keeps its meaning without a data migration.
        builder.Property(p => p.Direction).IsRequired().HasConversion<int>();

        builder.Property(p => p.PaymentDate).IsRequired().HasColumnType("date");
        builder.Property(p => p.PaymentMethod).IsRequired().HasMaxLength(40);
        builder.Property(p => p.Notes).HasMaxLength(1000);
        builder.Property(p => p.RecordedByUserId).IsRequired();

        builder.HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Purchase)
            .WithMany()
            .HasForeignKey(p => p.PurchaseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.TenantId, p.SupplierId })
            .HasDatabaseName("IX_SupplierPayments_Tenant_Supplier");

        // The balance service groups by direction on every supplier page and every dues report.
        builder.HasIndex(p => new { p.TenantId, p.SupplierId, p.Direction })
            .HasDatabaseName("IX_SupplierPayments_Tenant_Supplier_Direction");

        builder.HasIndex(p => new { p.TenantId, p.PurchaseId })
            .HasDatabaseName("IX_SupplierPayments_Tenant_Purchase");
    }
}

public sealed class PurchaseReturnConfiguration : IEntityTypeConfiguration<PurchaseReturn>
{
    public void Configure(EntityTypeBuilder<PurchaseReturn> builder)
    {
        builder.ToTable("PurchaseReturns");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.QuantityInBaseUnits).IsRequired();
        builder.Property(r => r.ReturnAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(r => r.Reason).IsRequired().HasMaxLength(500);
        builder.Property(r => r.ReturnedByUserId).IsRequired();

        // The PurchaseLine side of this relationship is configured on PurchaseLine.Returns.

        builder.HasOne(r => r.Batch)
            .WithMany()
            .HasForeignKey(r => r.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Both the returnable-quantity cap and the balance calculation group by line, and the
        // cap runs once per line every time somebody opens a return screen.
        builder.HasIndex(r => new { r.TenantId, r.PurchaseLineId })
            .HasDatabaseName("IX_PurchaseReturns_Tenant_PurchaseLine");
    }
}
