namespace PMS.Domain.Entities.Reporting;

/// <summary>
/// Materialized/denormalized daily sales summary for fast reporting queries.
/// This entity is only used by ReportingDbContext and is populated by scheduled jobs or triggers.
/// </summary>
public class DailySalesSummary
{
    public Guid Id { get; private set; }

    /// <summary>
    /// The date for this summary (date only, no time component).
    /// </summary>
    public DateOnly SalesDate { get; private set; }

    /// <summary>
    /// Total number of orders placed on this date.
    /// </summary>
    public int TotalOrders { get; private set; }

    /// <summary>
    /// Number of completed/delivered orders.
    /// </summary>
    public int CompletedOrders { get; private set; }

    /// <summary>
    /// Number of cancelled orders.
    /// </summary>
    public int CancelledOrders { get; private set; }

    /// <summary>
    /// Total revenue for the day.
    /// </summary>
    public decimal TotalRevenue { get; private set; }

    /// <summary>
    /// Currency code for the revenue.
    /// </summary>
    public string Currency { get; private set; } = "USD";

    /// <summary>
    /// Total number of items sold.
    /// </summary>
    public int TotalItemsSold { get; private set; }

    /// <summary>
    /// Number of unique customers who placed orders.
    /// </summary>
    public int UniqueCustomers { get; private set; }

    /// <summary>
    /// Number of new customers registered on this date.
    /// </summary>
    public int NewCustomers { get; private set; }

    /// <summary>
    /// Average order value for the day.
    /// </summary>
    public decimal AverageOrderValue { get; private set; }

    /// <summary>
    /// When this summary was last calculated/updated.
    /// </summary>
    public DateTime LastCalculatedUtc { get; private set; }

    private DailySalesSummary() { } // EF Core

    public static DailySalesSummary Create(
        DateOnly salesDate,
        int totalOrders,
        int completedOrders,
        int cancelledOrders,
        decimal totalRevenue,
        string currency,
        int totalItemsSold,
        int uniqueCustomers,
        int newCustomers)
    {
        return new DailySalesSummary
        {
            Id = Guid.NewGuid(),
            SalesDate = salesDate,
            TotalOrders = totalOrders,
            CompletedOrders = completedOrders,
            CancelledOrders = cancelledOrders,
            TotalRevenue = totalRevenue,
            Currency = currency,
            TotalItemsSold = totalItemsSold,
            UniqueCustomers = uniqueCustomers,
            NewCustomers = newCustomers,
            AverageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0,
            LastCalculatedUtc = DateTime.UtcNow
        };
    }

    public void Update(
        int totalOrders,
        int completedOrders,
        int cancelledOrders,
        decimal totalRevenue,
        int totalItemsSold,
        int uniqueCustomers,
        int newCustomers)
    {
        TotalOrders = totalOrders;
        CompletedOrders = completedOrders;
        CancelledOrders = cancelledOrders;
        TotalRevenue = totalRevenue;
        TotalItemsSold = totalItemsSold;
        UniqueCustomers = uniqueCustomers;
        NewCustomers = newCustomers;
        AverageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;
        LastCalculatedUtc = DateTime.UtcNow;
    }
}
