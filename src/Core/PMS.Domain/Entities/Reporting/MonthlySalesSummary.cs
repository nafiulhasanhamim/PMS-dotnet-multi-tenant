namespace PMS.Domain.Entities.Reporting;

/// <summary>
/// Monthly sales summary for trend analysis and year-over-year comparisons.
/// Only used by ReportingDbContext.
/// </summary>
public class MonthlySalesSummary
{
    public Guid Id { get; private set; }

    /// <summary>
    /// The year for this summary.
    /// </summary>
    public int Year { get; private set; }

    /// <summary>
    /// The month for this summary (1-12).
    /// </summary>
    public int Month { get; private set; }

    /// <summary>
    /// Total number of orders.
    /// </summary>
    public int TotalOrders { get; private set; }

    /// <summary>
    /// Number of completed orders.
    /// </summary>
    public int CompletedOrders { get; private set; }

    /// <summary>
    /// Number of cancelled orders.
    /// </summary>
    public int CancelledOrders { get; private set; }

    /// <summary>
    /// Total revenue for the month.
    /// </summary>
    public decimal TotalRevenue { get; private set; }

    /// <summary>
    /// Currency code.
    /// </summary>
    public string Currency { get; private set; } = "USD";

    /// <summary>
    /// Total items sold.
    /// </summary>
    public int TotalItemsSold { get; private set; }

    /// <summary>
    /// Number of unique customers.
    /// </summary>
    public int UniqueCustomers { get; private set; }

    /// <summary>
    /// Number of new customers registered this month.
    /// </summary>
    public int NewCustomers { get; private set; }

    /// <summary>
    /// Number of returning customers (ordered before).
    /// </summary>
    public int ReturningCustomers { get; private set; }

    /// <summary>
    /// Average order value.
    /// </summary>
    public decimal AverageOrderValue { get; private set; }

    /// <summary>
    /// Revenue compared to previous month (percentage).
    /// </summary>
    public decimal? RevenueGrowthPercent { get; private set; }

    /// <summary>
    /// Revenue compared to same month last year (percentage).
    /// </summary>
    public decimal? YearOverYearGrowthPercent { get; private set; }

    /// <summary>
    /// When this summary was last calculated.
    /// </summary>
    public DateTime LastCalculatedUtc { get; private set; }

    private MonthlySalesSummary() { } // EF Core

    public static MonthlySalesSummary Create(int year, int month, string currency = "USD")
    {
        return new MonthlySalesSummary
        {
            Id = Guid.NewGuid(),
            Year = year,
            Month = month,
            Currency = currency,
            LastCalculatedUtc = DateTime.UtcNow
        };
    }

    public void UpdateMetrics(
        int totalOrders,
        int completedOrders,
        int cancelledOrders,
        decimal totalRevenue,
        int totalItemsSold,
        int uniqueCustomers,
        int newCustomers,
        int returningCustomers)
    {
        TotalOrders = totalOrders;
        CompletedOrders = completedOrders;
        CancelledOrders = cancelledOrders;
        TotalRevenue = totalRevenue;
        TotalItemsSold = totalItemsSold;
        UniqueCustomers = uniqueCustomers;
        NewCustomers = newCustomers;
        ReturningCustomers = returningCustomers;
        AverageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;
        LastCalculatedUtc = DateTime.UtcNow;
    }

    public void UpdateGrowthMetrics(decimal? monthOverMonth, decimal? yearOverYear)
    {
        RevenueGrowthPercent = monthOverMonth;
        YearOverYearGrowthPercent = yearOverYear;
        LastCalculatedUtc = DateTime.UtcNow;
    }
}
