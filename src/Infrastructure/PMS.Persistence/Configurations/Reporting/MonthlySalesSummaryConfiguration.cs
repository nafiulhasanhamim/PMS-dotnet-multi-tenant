using PMS.Domain.Entities.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PMS.Persistence.Configurations.Reporting;

/// <summary>
/// EF Core configuration for MonthlySalesSummary entity.
/// </summary>
public class MonthlySalesSummaryConfiguration : IEntityTypeConfiguration<MonthlySalesSummary>
{
    public void Configure(EntityTypeBuilder<MonthlySalesSummary> builder)
    {
        builder.ToTable("MonthlySalesSummaries", "reporting");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Year)
            .IsRequired();

        builder.Property(m => m.Month)
            .IsRequired();

        builder.Property(m => m.TotalOrders)
            .IsRequired();

        builder.Property(m => m.CompletedOrders)
            .IsRequired();

        builder.Property(m => m.CancelledOrders)
            .IsRequired();

        builder.Property(m => m.TotalRevenue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(m => m.TotalItemsSold)
            .IsRequired();

        builder.Property(m => m.UniqueCustomers)
            .IsRequired();

        builder.Property(m => m.NewCustomers)
            .IsRequired();

        builder.Property(m => m.ReturningCustomers)
            .IsRequired();

        builder.Property(m => m.AverageOrderValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(m => m.RevenueGrowthPercent)
            .HasPrecision(10, 2);

        builder.Property(m => m.YearOverYearGrowthPercent)
            .HasPrecision(10, 2);

        builder.Property(m => m.LastCalculatedUtc)
            .IsRequired();

        // Indexes
        builder.HasIndex(m => new { m.Year, m.Month })
            .IsUnique();

        builder.HasIndex(m => m.Year);
    }
}
