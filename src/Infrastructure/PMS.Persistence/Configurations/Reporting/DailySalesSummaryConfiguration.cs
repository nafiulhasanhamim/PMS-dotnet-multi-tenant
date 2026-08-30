using PMS.Domain.Entities.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations.Reporting;

/// <summary>
/// EF Core configuration for DailySalesSummary entity.
/// </summary>
public class DailySalesSummaryConfiguration : IEntityTypeConfiguration<DailySalesSummary>
{
    public void Configure(EntityTypeBuilder<DailySalesSummary> builder)
    {
        builder.ToTable("DailySalesSummaries", "reporting");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.SalesDate)
            .IsRequired();

        builder.Property(d => d.TotalOrders)
            .IsRequired();

        builder.Property(d => d.CompletedOrders)
            .IsRequired();

        builder.Property(d => d.CancelledOrders)
            .IsRequired();

        builder.Property(d => d.TotalRevenue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(d => d.TotalItemsSold)
            .IsRequired();

        builder.Property(d => d.UniqueCustomers)
            .IsRequired();

        builder.Property(d => d.NewCustomers)
            .IsRequired();

        builder.Property(d => d.AverageOrderValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.LastCalculatedUtc)
            .IsRequired();

        // Indexes
        builder.HasIndex(d => d.SalesDate)
            .IsUnique();

        builder.HasIndex(d => new { d.SalesDate, d.TotalRevenue });
    }
}
