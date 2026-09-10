using PMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        // No HasQueryFilter here, and none needed: Product implements ITenantEntity, so
        // ApplicationDbContext's convention pass adds the tenant filter (composed with soft
        // delete where applicable) and TenantEntityInterceptor stamps TenantId on insert.
        // Writing one by hand here would REPLACE the convention's filter, not add to it.

        builder.Property(p => p.ProductType).IsRequired().HasConversion<int>();
        builder.Property(p => p.BrandName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Company).HasMaxLength(200);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.IsActive).IsRequired();

        builder.Property(p => p.GenericName).HasMaxLength(300);
        builder.Property(p => p.Strength).HasMaxLength(100);
        builder.Property(p => p.DosageForm).HasMaxLength(100);
        builder.Property(p => p.IsAntibiotic).IsRequired();

        builder.Property(p => p.BaseUnitName).IsRequired().HasMaxLength(50);
        builder.Property(p => p.MidUnitName).HasMaxLength(50);
        builder.Property(p => p.LargeUnitName).HasMaxLength(50);

        // Money as decimal, never floating point. 18,4 leaves room for a per-base unit price
        // derived from a bulk pack, which is often not a round number of paisa.
        // Nullable: an unpriced product is a real state, created by bulk import.
        builder.Property(p => p.PricePerBase).HasPrecision(18, 4);
        builder.Property(p => p.PricePerMid).HasPrecision(18, 4);
        builder.Property(p => p.PricePerLarge).HasPrecision(18, 4);

        builder.Property(p => p.IsSetupComplete).IsRequired();

        builder.Property(p => p.ReorderLevel).IsRequired();
        builder.Property(p => p.ShelfLocation).HasMaxLength(100);

        // Not a navigation property, deliberately.
        //
        // A real FK constraint exists in the database (see script 007) because the reference
        // must be valid. But no navigation is mapped, because loading it would join a
        // tenant-scoped entity to an unfiltered platform table on every read, and the import
        // screen already has the catalogue data it needs. The column records provenance; it
        // is not a path to traverse.
        builder.Property(p => p.CatalogMedicineId);

        // The two searches every list does, both led by TenantId so the seek lands inside one
        // pharmacy's rows before it does anything else.
        builder.HasIndex(p => new { p.TenantId, p.BrandName })
            .HasDatabaseName("IX_Products_Tenant_BrandName");

        builder.HasIndex(p => new { p.TenantId, p.GenericName })
            .HasDatabaseName("IX_Products_Tenant_GenericName");

        // The incomplete-products filter and the banner count. Filtered so the index holds
        // only the rows it is ever used to find - the incomplete ones are a small and
        // shrinking set, while the complete ones are the whole catalogue.
        builder.HasIndex(p => new { p.TenantId, p.IsSetupComplete })
            .HasDatabaseName("IX_Products_Tenant_SetupIncomplete")
            .HasFilter("[IsSetupComplete] = 0");

        // Finding "have I already imported this?" on the catalogue import screen.
        builder.HasIndex(p => new { p.TenantId, p.CatalogMedicineId })
            .HasDatabaseName("IX_Products_Tenant_CatalogMedicineId");
    }
}
