using PMS.Application;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using PMS.Persistence.Contexts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PMS.PerformanceTests.Common;

/// <summary>
/// Base class for benchmarks providing common setup and test data seeding.
/// </summary>
public abstract class BenchmarkBase : IDisposable
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly ApplicationDbContext DbContext;
    protected readonly IMediator Mediator;

    private readonly string _databaseName;

    protected BenchmarkBase()
    {
        _databaseName = $"Benchmark_{Guid.NewGuid():N}";

        var services = new ServiceCollection();

        // Add DbContext with in-memory database
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase(_databaseName);
        });

        // Register IApplicationDbContext
        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // Add Application services (MediatR, validators, etc.)
        services.AddApplicationServices();

        ServiceProvider = services.BuildServiceProvider();

        DbContext = ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Ensure database is created
        DbContext.Database.EnsureCreated();
    }

    /// <summary>
    /// Seeds test customers into the database.
    /// </summary>
    protected void SeedCustomers(int count)
    {
        var customers = new List<Customer>();

        for (int i = 0; i < count; i++)
        {
            var email = Email.Create($"customer{i}@benchmark.com").Value;
            var customer = new Customer($"First{i}", $"Last{i}", email);
            customers.Add(customer);
        }

        DbContext.Customers.AddRange(customers);
        DbContext.SaveChanges();
    }

    /// <summary>
    /// Seeds test products into the database.
    /// </summary>
    protected void SeedProducts(int count)
    {
        var products = new List<Product>();

        for (int i = 0; i < count; i++)
        {
            var price = Money.Create(10.00m + i, "USD").Value;
            var product = new Product(
                $"Product {i}",
                $"SKU-{i:D6}",
                price,
                $"Description for product {i}");
            products.Add(product);
        }

        DbContext.Products.AddRange(products);
        DbContext.SaveChanges();
    }

    /// <summary>
    /// Seeds test orders into the database.
    /// Requires customers and products to be seeded first.
    /// </summary>
    protected void SeedOrders(int count)
    {
        var customers = DbContext.Customers.ToList();
        var products = DbContext.Products.ToList();

        if (!customers.Any() || !products.Any())
        {
            throw new InvalidOperationException("Seed customers and products before orders");
        }

        var orders = new List<Order>();
        var random = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < count; i++)
        {
            var customer = customers[i % customers.Count];
            var address = Address.Create(
                $"Street {i}",
                "City",
                "State",
                "12345",
                "USA").Value;

            var order = new Order(customer.Id, address);

            // Add 1-5 items per order
            var itemCount = random.Next(1, 6);
            for (int j = 0; j < itemCount; j++)
            {
                var product = products[random.Next(products.Count)];
                var quantity = random.Next(1, 10);
                order.AddItem(product.Id, product.Name, product.Price, quantity);
            }

            orders.Add(order);
        }

        DbContext.Orders.AddRange(orders);
        DbContext.SaveChanges();
    }

    /// <summary>
    /// Clears all data from the database.
    /// </summary>
    protected void ClearData()
    {
        DbContext.OrderItems.RemoveRange(DbContext.OrderItems.IgnoreQueryFilters());
        DbContext.Orders.RemoveRange(DbContext.Orders.IgnoreQueryFilters());
        DbContext.Customers.RemoveRange(DbContext.Customers.IgnoreQueryFilters());
        DbContext.Products.RemoveRange(DbContext.Products.IgnoreQueryFilters());
        DbContext.SaveChanges();
    }

    public void Dispose()
    {
        DbContext.Database.EnsureDeleted();
        DbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
