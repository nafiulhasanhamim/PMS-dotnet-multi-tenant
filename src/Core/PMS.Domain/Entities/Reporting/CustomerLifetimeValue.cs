namespace PMS.Domain.Entities.Reporting;

/// <summary>
/// Customer lifetime value (CLV) metrics for analytics and segmentation.
/// Stores calculated customer value metrics, updated periodically.
/// Only used by ReportingDbContext.
/// </summary>
public class CustomerLifetimeValue
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Reference to the customer.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Customer email snapshot.
    /// </summary>
    public string Email { get; private set; } = null!;

    /// <summary>
    /// Customer full name snapshot.
    /// </summary>
    public string CustomerName { get; private set; } = null!;

    /// <summary>
    /// Total amount spent by this customer.
    /// </summary>
    public decimal TotalSpent { get; private set; }

    /// <summary>
    /// Currency code.
    /// </summary>
    public string Currency { get; private set; } = "USD";

    /// <summary>
    /// Total number of orders.
    /// </summary>
    public int TotalOrders { get; private set; }

    /// <summary>
    /// Total items purchased.
    /// </summary>
    public int TotalItemsPurchased { get; private set; }

    /// <summary>
    /// Average order value.
    /// </summary>
    public decimal AverageOrderValue { get; private set; }

    /// <summary>
    /// Average days between orders.
    /// </summary>
    public decimal? AverageDaysBetweenOrders { get; private set; }

    /// <summary>
    /// Customer's first order date.
    /// </summary>
    public DateTime? FirstOrderDateUtc { get; private set; }

    /// <summary>
    /// Customer's most recent order date.
    /// </summary>
    public DateTime? LastOrderDateUtc { get; private set; }

    /// <summary>
    /// Days since the last order.
    /// </summary>
    public int? DaysSinceLastOrder { get; private set; }

    /// <summary>
    /// Customer segment based on RFM analysis.
    /// </summary>
    public CustomerSegment Segment { get; private set; }

    /// <summary>
    /// Recency score (1-5, higher = more recent).
    /// </summary>
    public int RecencyScore { get; private set; }

    /// <summary>
    /// Frequency score (1-5, higher = more frequent).
    /// </summary>
    public int FrequencyScore { get; private set; }

    /// <summary>
    /// Monetary score (1-5, higher = more spending).
    /// </summary>
    public int MonetaryScore { get; private set; }

    /// <summary>
    /// When this record was last calculated.
    /// </summary>
    public DateTime LastCalculatedUtc { get; private set; }

    private CustomerLifetimeValue() { } // EF Core

    public static CustomerLifetimeValue Create(
        Guid customerId,
        string email,
        string customerName,
        string currency = "USD")
    {
        return new CustomerLifetimeValue
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Email = email,
            CustomerName = customerName,
            Currency = currency,
            Segment = CustomerSegment.New,
            LastCalculatedUtc = DateTime.UtcNow
        };
    }

    public void UpdateMetrics(
        decimal totalSpent,
        int totalOrders,
        int totalItemsPurchased,
        DateTime? firstOrderDate,
        DateTime? lastOrderDate,
        decimal? avgDaysBetweenOrders)
    {
        TotalSpent = totalSpent;
        TotalOrders = totalOrders;
        TotalItemsPurchased = totalItemsPurchased;
        AverageOrderValue = totalOrders > 0 ? totalSpent / totalOrders : 0;
        FirstOrderDateUtc = firstOrderDate;
        LastOrderDateUtc = lastOrderDate;
        AverageDaysBetweenOrders = avgDaysBetweenOrders;
        DaysSinceLastOrder = lastOrderDate.HasValue
            ? (int)(DateTime.UtcNow - lastOrderDate.Value).TotalDays
            : null;
        LastCalculatedUtc = DateTime.UtcNow;
    }

    public void UpdateRfmScores(int recency, int frequency, int monetary)
    {
        RecencyScore = Math.Clamp(recency, 1, 5);
        FrequencyScore = Math.Clamp(frequency, 1, 5);
        MonetaryScore = Math.Clamp(monetary, 1, 5);
        Segment = CalculateSegment(RecencyScore, FrequencyScore, MonetaryScore);
        LastCalculatedUtc = DateTime.UtcNow;
    }

    private static CustomerSegment CalculateSegment(int r, int f, int m)
    {
        var avgScore = (r + f + m) / 3.0;

        return (r, f, m, avgScore) switch
        {
            ( >= 4, >= 4, >= 4, _) => CustomerSegment.Champion,
            ( >= 4, >= 3, >= 3, _) => CustomerSegment.Loyal,
            ( >= 3, >= 1, >= 3, _) => CustomerSegment.BigSpender,
            ( >= 4, <= 2, _, _) => CustomerSegment.NewCustomer,
            ( >= 3, >= 3, >= 2, _) => CustomerSegment.Promising,
            (_, >= 4, >= 2, _) => CustomerSegment.FrequentBuyer,
            ( <= 2, >= 3, >= 3, _) => CustomerSegment.AtRisk,
            ( <= 2, >= 2, >= 2, _) => CustomerSegment.NeedAttention,
            ( <= 2, <= 2, >= 3, _) => CustomerSegment.CantLoseThem,
            ( <= 2, <= 2, <= 2, _) => CustomerSegment.Lost,
            _ => CustomerSegment.Other
        };
    }
}

/// <summary>
/// Customer segment based on RFM (Recency, Frequency, Monetary) analysis.
/// </summary>
public enum CustomerSegment
{
    /// <summary>New customer with no order history.</summary>
    New = 0,

    /// <summary>Best customers - bought recently, buy often, spend the most.</summary>
    Champion = 1,

    /// <summary>Loyal customers with consistent purchasing behavior.</summary>
    Loyal = 2,

    /// <summary>High spending but less frequent.</summary>
    BigSpender = 3,

    /// <summary>Recent first-time buyers.</summary>
    NewCustomer = 4,

    /// <summary>Recent customers with average frequency and monetary.</summary>
    Promising = 5,

    /// <summary>Buy frequently but lower monetary value.</summary>
    FrequentBuyer = 6,

    /// <summary>Used to be good customers, haven't purchased recently.</summary>
    AtRisk = 7,

    /// <summary>Below average in recency, frequency, and monetary.</summary>
    NeedAttention = 8,

    /// <summary>Made big purchases but long time ago.</summary>
    CantLoseThem = 9,

    /// <summary>Lowest recency, frequency, and monetary scores.</summary>
    Lost = 10,

    /// <summary>Doesn't fit other categories.</summary>
    Other = 99
}
