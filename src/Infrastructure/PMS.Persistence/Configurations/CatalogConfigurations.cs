using PMS.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Catalog configuration.
//
// Note what is absent: no HasQueryFilter anywhere in this file. These entities do not
// implement ITenantEntity, so ApplicationDbContext's reflection-driven filter pass skips them
// and the TenantEntityInterceptor never stamps them. That is the intended behaviour — the
// catalog is shared platform data — and FilterInventoryTests asserts it stays that way, so a
// filter added here by accident fails a test rather than quietly hiding every medicine from
// every pharmacy.
// ═══════════════════════════════════════════════════════════════════════════════════════════

public sealed class CatalogManufacturerConfiguration : IEntityTypeConfiguration<CatalogManufacturer>
{
    public void Configure(EntityTypeBuilder<CatalogManufacturer> builder)
    {
        builder.ToTable("CatalogManufacturers");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).IsRequired().HasMaxLength(300);

        // Case-insensitive in practice: the database collation is case-insensitive, so this
        // index also enforces that "Beximco" and "BEXIMCO" cannot both exist. The importer
        // normalises before lookup regardless, so it never depends on collation to dedupe.
        builder.HasIndex(m => m.Name).IsUnique();
    }
}

public sealed class CatalogDrugClassConfiguration : IEntityTypeConfiguration<CatalogDrugClass>
{
    public void Configure(EntityTypeBuilder<CatalogDrugClass> builder)
    {
        builder.ToTable("CatalogDrugClasses");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(300);
        builder.Property(c => c.IsAntibioticClass).IsRequired();

        builder.HasIndex(c => c.Name).IsUnique();
    }
}

public sealed class CatalogDosageFormConfiguration : IEntityTypeConfiguration<CatalogDosageForm>
{
    public void Configure(EntityTypeBuilder<CatalogDosageForm> builder)
    {
        builder.ToTable("CatalogDosageForms");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);

        builder.HasIndex(f => f.Name).IsUnique();
    }
}

public sealed class CatalogGenericConfiguration : IEntityTypeConfiguration<CatalogGeneric>
{
    public void Configure(EntityTypeBuilder<CatalogGeneric> builder)
    {
        builder.ToTable("CatalogGenerics");
        builder.HasKey(g => g.Id);

        // 300 is generous: the longest source name is a four-ingredient combination.
        builder.Property(g => g.Name).IsRequired().HasMaxLength(500);
        builder.Property(g => g.MonographUrl).HasMaxLength(1000);
        builder.Property(g => g.IndicationSummary);
        builder.Property(g => g.IsAntibiotic).IsRequired();
        builder.Property(g => g.AntibioticSignal).IsRequired().HasConversion<int>();

        builder.HasIndex(g => g.Name).IsUnique();

        // Tenants search the catalog by generic as well as by brand.
        builder.HasIndex(g => g.IsAntibiotic);

        builder.HasOne(g => g.DrugClass)
            .WithMany()
            .HasForeignKey(g => g.DrugClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CatalogMedicineConfiguration : IEntityTypeConfiguration<CatalogMedicine>
{
    public void Configure(EntityTypeBuilder<CatalogMedicine> builder)
    {
        builder.ToTable("CatalogMedicines");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.SourceBrandId).IsRequired();
        builder.Property(m => m.BrandName).IsRequired().HasMaxLength(300);
        builder.Property(m => m.Strength).HasMaxLength(300);
        builder.Property(m => m.MedicineType).HasMaxLength(50);
        builder.Property(m => m.PackageInfo).HasMaxLength(1000);
        builder.Property(m => m.SourceUnitPrice).HasPrecision(18, 4);
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.ImportedAt).IsRequired();
        builder.Property(m => m.LastUpdatedAt).IsRequired();

        // The importer's upsert key. Unique, so a second run can only update.
        builder.HasIndex(m => m.SourceBrandId)
            .IsUnique()
            .HasDatabaseName("UX_CatalogMedicines_SourceBrandId");

        // The search a tenant runs constantly while importing their catalog: type "nap", get
        // Napa. A plain index serves LIKE 'nap%' as a seek; it cannot serve '%nap%'.
        builder.HasIndex(m => m.BrandName)
            .HasDatabaseName("IX_CatalogMedicines_BrandName");

        // Covers browsing by ingredient, and the antibiotic register's joins.
        builder.HasIndex(m => m.GenericId);

        builder.HasOne(m => m.Generic)
            .WithMany()
            .HasForeignKey(m => m.GenericId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Manufacturer)
            .WithMany()
            .HasForeignKey(m => m.ManufacturerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.DosageForm)
            .WithMany()
            .HasForeignKey(m => m.DosageFormId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
