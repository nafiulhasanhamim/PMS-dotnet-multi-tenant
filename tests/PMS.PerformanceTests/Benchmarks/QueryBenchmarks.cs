using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using PMS.PerformanceTests.Common;
using Microsoft.EntityFrameworkCore;

namespace PMS.PerformanceTests.Benchmarks;

/// <summary>
/// Benchmarks for EF Core query performance.
/// Tests common query patterns to detect N+1 issues and slow queries.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class QueryBenchmarks : BenchmarkBase
{
    [Params(100, 500, 1000)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        ClearData();
        SeedCustomers(DataSize);
        SeedProducts(DataSize);
        SeedOrders(DataSize / 10); // 10% orders relative to data size
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        ClearData();
    }

    #region Customer Queries

    [Benchmark(Description = "Get all customers (no tracking)")]
    public async Task<int> GetAllCustomers_NoTracking()
    {
        var customers = await DbContext.Customers
            .AsNoTracking()
            .ToListAsync();

        return customers.Count;
    }

    [Benchmark(Description = "Get customers with pagination")]
    public async Task<int> GetCustomers_WithPagination()
    {
        var customers = await DbContext.Customers
            .AsNoTracking()
            .OrderBy(c => c.LastName)
            .Skip(0)
            .Take(20)
            .ToListAsync();

        return customers.Count;
    }

    [Benchmark(Description = "Get customers by search term")]
    public async Task<int> GetCustomers_BySearchTerm()
    {
        var customers = await DbContext.Customers
            .AsNoTracking()
            .Where(c => c.LastName.Contains("Last1"))
            .Take(20)
            .ToListAsync();

        return customers.Count;
    }

    [Benchmark(Description = "Get customer by ID")]
    public async Task<bool> GetCustomer_ById()
    {
        var firstCustomer = await DbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (firstCustomer == null) return false;

        var customer = await DbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == firstCustomer.Id);

        return customer != null;
    }

    [Benchmark(Description = "Get customer with orders (eager load)")]
    public async Task<int> GetCustomer_WithOrders_EagerLoad()
    {
        var customer = await DbContext.Customers
            .AsNoTracking()
            .Include(c => c.Orders)
            .FirstOrDefaultAsync();

        return customer?.Orders.Count ?? 0;
    }

    #endregion

    #region Product Queries

    [Benchmark(Description = "Get all products (no tracking)")]
    public async Task<int> GetAllProducts_NoTracking()
    {
        var products = await DbContext.Products
            .AsNoTracking()
            .ToListAsync();

        return products.Count;
    }

    [Benchmark(Description = "Get products with price filter")]
    public async Task<int> GetProducts_WithPriceFilter()
    {
        var products = await DbContext.Products
            .AsNoTracking()
            .Where(p => p.Price.Amount >= 15.00m && p.Price.Amount <= 50.00m)
            .ToListAsync();

        return products.Count;
    }

    [Benchmark(Description = "Get products by SKU prefix")]
    public async Task<int> GetProducts_BySkuPrefix()
    {
        var products = await DbContext.Products
            .AsNoTracking()
            .Where(p => p.Sku.StartsWith("SKU-0001"))
            .ToListAsync();

        return products.Count;
    }

    [Benchmark(Description = "Get active products only")]
    public async Task<int> GetProducts_ActiveOnly()
    {
        var products = await DbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync();

        return products.Count;
    }

    #endregion

    #region Order Queries

    [Benchmark(Description = "Get all orders (no tracking)")]
    public async Task<int> GetAllOrders_NoTracking()
    {
        var orders = await DbContext.Orders
            .AsNoTracking()
            .ToListAsync();

        return orders.Count;
    }

    [Benchmark(Description = "Get orders with items (eager load)")]
    public async Task<int> GetOrders_WithItems_EagerLoad()
    {
        var orders = await DbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .ToListAsync();

        return orders.Sum(o => o.Items.Count);
    }

    [Benchmark(Description = "Get orders with customer (eager load)")]
    public async Task<int> GetOrders_WithCustomer_EagerLoad()
    {
        var orders = await DbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Take(50)
            .ToListAsync();

        return orders.Count;
    }

    [Benchmark(Description = "Get orders with full graph")]
    public async Task<int> GetOrders_WithFullGraph()
    {
        var orders = await DbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Take(50)
            .ToListAsync();

        return orders.Count;
    }

    [Benchmark(Description = "Get orders by status")]
    public async Task<int> GetOrders_ByStatus()
    {
        var orders = await DbContext.Orders
            .AsNoTracking()
            .Where(o => o.Status == Domain.Enums.OrderStatus.Pending)
            .ToListAsync();

        return orders.Count;
    }

    #endregion

    #region Aggregate Queries

    [Benchmark(Description = "Count customers")]
    public async Task<int> CountCustomers()
    {
        return await DbContext.Customers.CountAsync();
    }

    [Benchmark(Description = "Count products by active status")]
    public async Task<int> CountProducts_ByActiveStatus()
    {
        return await DbContext.Products
            .Where(p => p.IsActive)
            .CountAsync();
    }

    [Benchmark(Description = "Check customer exists by email")]
    public async Task<bool> CustomerExists_ByEmail()
    {
        return await DbContext.Customers
            .AnyAsync(c => c.Email.Value == "customer50@benchmark.com");
    }

    #endregion
}
