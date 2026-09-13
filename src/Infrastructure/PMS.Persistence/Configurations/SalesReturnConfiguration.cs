using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMS.Domain.Entities;

namespace PMS.Persistence.Configurations;

public sealed class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("SalesReturns", table =>
        {
            // A return of nothing is not a return. Enforced in the database as well as in the
            // factory method, because this is the row a refund is computed from.
            table.HasCheckConstraint(
                "CK_SalesReturns_QuantityPositive",
                "[QuantityReturnedInBaseUnits] > 0");

            table.HasCheckConstraint(
                "CK_SalesReturns_RefundNotNegative",
                "[RefundAmount] >= 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.SaleLineId).IsRequired();
        builder.Property(r => r.QuantityReturnedInBaseUnits).IsRequired();
        builder.Property(r => r.Reason).IsRequired().HasMaxLength(500);
        builder.Property(r => r.RefundAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(r => r.ReturnedByUserId).IsRequired();

        // Cannot be a database constraint. "The sum of returns against a line does not exceed
        // what the line sold" spans rows, which a CHECK cannot see; a trigger could, and would
        // put a business rule somewhere nobody reading the handler would look for it. It is
        // enforced in SalesReturn.Record, which is the only way one of these can be built.
        builder.HasIndex(r => new { r.TenantId, r.SaleLineId })
            .HasDatabaseName("IX_SalesReturns_Tenant_SaleLine");
    }
}
