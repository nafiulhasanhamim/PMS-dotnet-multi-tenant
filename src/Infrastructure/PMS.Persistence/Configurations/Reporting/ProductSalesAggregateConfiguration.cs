using PMS.Domain.Entities.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations.Reporting;

/// <summary>
/// EF Core configuration for ProductSalesAggregate entity.
/// </summary>
public class ProductSalesAggregateConfiguration : IEntityTypeConfiguration<ProductSalesAggregate>
{
    public void Configure(EntityTypeBuilder<ProductSalesAggregate> builder)
    {
        builder.ToTable("ProductSalesAggregates", "reporting");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProductId)
            .IsRequired();

        builder.Property(p => p.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.TotalQuantitySold)
            .IsRequired();

        builder.Property(p => p.TotalRevenue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(p => p.OrderCount)
            .IsRequired();

        builder.Property(p => p.UniqueCustomers)
            .IsRequired();

        builder.Property(p => p.AverageQuantityPerOrder)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(p => p.AverageSellingPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.LastCalculatedUtc)
            .IsRequired();

        // Indexes
        builder.HasIndex(p => p.ProductId)
            .IsUnique();

        builder.HasIndex(p => p.Sku);

        builder.HasIndex(p => p.TotalRevenue);

        builder.HasIndex(p => p.TotalQuantitySold);
    }
}
