using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Persistence.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(s => s.Id);

        // No HasQueryFilter here. Sale implements ITenantEntity, so ApplicationDbContext's
        // convention pass adds the tenant filter and TenantEntityInterceptor stamps TenantId on
        // insert. One written here would REPLACE the convention's filter rather than add to it.

        builder.Property(s => s.InvoiceNumber).IsRequired().HasMaxLength(40);
        builder.Property(s => s.SaleDate).IsRequired();
        builder.Property(s => s.CashierUserId).IsRequired();

        // 18,2 for every money column on a sale, and that is a different decision from the
        // 18,4 on a batch's purchase cost. A per-base-unit cost derived from a bulk pack is
        // genuinely fractional and rounding it at rest would misstate margin. These figures are
        // the opposite: they are what was charged, what was discounted and what was handed back
        // in cash. There is no such thing as a third of a paisa of change, and storing one
        // would let the columns stop summing to each other.
        builder.Property(s => s.Subtotal).IsRequired().HasPrecision(18, 2);
        builder.Property(s => s.DiscountValue).HasPrecision(18, 2);
        builder.Property(s => s.DiscountAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(s => s.NetTotal).IsRequired().HasPrecision(18, 2);
        builder.Property(s => s.CashReceived).IsRequired().HasPrecision(18, 2);
        builder.Property(s => s.ChangeGiven).IsRequired().HasPrecision(18, 2);

        builder.Property(s => s.DiscountType).HasConversion<int>();
        builder.Property(s => s.Status).IsRequired().HasConversion<int>();

        builder.Property(s => s.CancelledReason).HasMaxLength(500);

        builder.Property(s => s.CustomerName).HasMaxLength(200);
        builder.Property(s => s.CustomerPhone).HasMaxLength(40);

        builder.Property(s => s.PatientName).HasMaxLength(200);
        builder.Property(s => s.PatientPhone).HasMaxLength(40);
        builder.Property(s => s.DoctorName).HasMaxLength(200);
        builder.Property(s => s.PrescriptionNumber).HasMaxLength(100);
        builder.Property(s => s.PrescriptionDate).HasColumnType("date");
        builder.Property(s => s.PrescriptionVerified).IsRequired();

        builder.HasMany(s => s.Lines)
            .WithOne(l => l.Sale)
            .HasForeignKey(l => l.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        // The collection is exposed as IReadOnlyCollection over a private List, so EF has to be
        // told to go through the backing field. Without this it tries to add to the read-only
        // property and the lines appended by Sale.AddLine are never discovered — the sale saves
        // with no lines at all, and nothing complains.
        builder.Metadata
            .FindNavigation(nameof(Sale.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Per pharmacy, not global. Two pharmacies both have an INV-000001 and neither knows
        // about the other's; what this forbids is one pharmacy issuing the same number twice,
        // which is what would make a return ambiguous about which sale it reverses. It is also
        // the backstop under the invoice-number generator: if the atomic allocation were ever
        // replaced by something racy, this turns a silent duplicate into a failed insert.
        builder.HasIndex(s => new { s.TenantId, s.InvoiceNumber })
            .IsUnique()
            .HasDatabaseName("UX_Sales_Tenant_InvoiceNumber");

        // Module 8's reports query heavily on both of these, and the module brief asks for them
        // by name. Date first because every report is a date range; cashier because "who sold
        // what this month" is the other question, and because it is the index that makes an
        // Employee's own-sales-only list a seek rather than a scan of the pharmacy's sales.
        builder.HasIndex(s => new { s.TenantId, s.SaleDate })
            .HasDatabaseName("IX_Sales_Tenant_SaleDate");

        builder.HasIndex(s => new { s.TenantId, s.CashierUserId })
            .HasDatabaseName("IX_Sales_Tenant_Cashier");
    }
}
