using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Product entity.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        // Primary Key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.Sku)
            .HasMaxLength(50)
            .IsRequired();

        // Price value object - owned type stored as columns
        builder.OwnsOne(p => p.Price, price =>
        {
            price.Property(m => m.Amount)
                .HasColumnName("Price")
                .HasPrecision(18, 2)
                .IsRequired();

            price.Property(m => m.Currency)
                .HasColumnName("PriceCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(p => p.StockQuantity)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();

        // Audit fields
        builder.Property(p => p.CreatedOnUtc)
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .HasMaxLength(256);

        builder.Property(p => p.ModifiedBy)
            .HasMaxLength(256);

        // Soft delete fields
        builder.Property(p => p.DeletedBy)
            .HasMaxLength(256);

        // Indexes
        builder.HasIndex(p => p.Sku)
            .IsUnique();

        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => p.IsDeleted);
    }
}
