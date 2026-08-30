using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using PMS.Application.Features.Customers.Commands.CreateCustomer;
using PMS.Application.Features.Customers.Queries.GetCustomerById;
using PMS.Application.Features.Customers.Queries.GetCustomers;
using PMS.Application.Features.Products.Commands.CreateProduct;
using PMS.Application.Features.Products.Queries.GetProductById;
using PMS.Application.Features.Products.Queries.GetProducts;
using PMS.PerformanceTests.Common;

namespace PMS.PerformanceTests.Benchmarks;

/// <summary>
/// Benchmarks for MediatR handler performance.
/// Tests command and query handler execution times.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class HandlerBenchmarks : BenchmarkBase
{
    private int _customerCounter;
    private int _productCounter;
    private Guid _existingCustomerId;
    private Guid _existingProductId;

    [Params(100, 500)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        ClearData();
        SeedCustomers(DataSize);
        SeedProducts(DataSize);
        SeedOrders(DataSize / 10);

        // Get existing IDs for read operations
        _existingCustomerId = DbContext.Customers.First().Id;
        _existingProductId = DbContext.Products.First().Id;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        ClearData();
    }

    [IterationSetup(Target = nameof(CreateCustomer_Command))]
    public void ResetCustomerCounter()
    {
        _customerCounter = DataSize + 1000; // Start after seeded data
    }

    [IterationSetup(Target = nameof(CreateProduct_Command))]
    public void ResetProductCounter()
    {
        _productCounter = DataSize + 1000; // Start after seeded data
    }

    #region Customer Query Benchmarks

    [Benchmark(Description = "GetCustomers Query (page 1)")]
    public async Task<int> GetCustomers_Query_Page1()
    {
        var query = new GetCustomersQuery(Page: 1, PageSize: 20);
        var result = await Mediator.Send(query);
        return result.Items.Count;
    }

    [Benchmark(Description = "GetCustomers Query (with search)")]
    public async Task<int> GetCustomers_Query_WithSearch()
    {
        var query = new GetCustomersQuery(SearchTerm: "Last1", Page: 1, PageSize: 20);
        var result = await Mediator.Send(query);
        return result.Items.Count;
    }

    [Benchmark(Description = "GetCustomerById Query")]
    public async Task<bool> GetCustomerById_Query()
    {
        var query = new GetCustomerByIdQuery(_existingCustomerId);
        var result = await Mediator.Send(query);
        return result.IsSuccess;
    }

    #endregion

    #region Customer Command Benchmarks

    [Benchmark(Description = "CreateCustomer Command")]
    public async Task<bool> CreateCustomer_Command()
    {
        var counter = Interlocked.Increment(ref _customerCounter);
        var command = new CreateCustomerCommand(
            $"First{counter}",
            $"Last{counter}",
            $"customer{counter}@benchmark.com",
            "1234567890");

        var result = await Mediator.Send(command);
        return result.IsSuccess;
    }

    #endregion

    #region Product Query Benchmarks

    [Benchmark(Description = "GetProducts Query (page 1)")]
    public async Task<int> GetProducts_Query_Page1()
    {
        var query = new GetProductsQuery(Page: 1, PageSize: 20);
        var result = await Mediator.Send(query);
        return result.Items.Count;
    }

    [Benchmark(Description = "GetProducts Query (with price filter)")]
    public async Task<int> GetProducts_Query_WithPriceFilter()
    {
        var query = new GetProductsQuery(MinPrice: 15.00m, MaxPrice: 50.00m, Page: 1, PageSize: 20);
        var result = await Mediator.Send(query);
        return result.Items.Count;
    }

    [Benchmark(Description = "GetProductById Query")]
    public async Task<bool> GetProductById_Query()
    {
        var query = new GetProductByIdQuery(_existingProductId);
        var result = await Mediator.Send(query);
        return result.IsSuccess;
    }

    #endregion

    #region Product Command Benchmarks

    [Benchmark(Description = "CreateProduct Command")]
    public async Task<bool> CreateProduct_Command()
    {
        var counter = Interlocked.Increment(ref _productCounter);
        var command = new CreateProductCommand(
            $"Product {counter}",
            $"SKU-BENCH-{counter}",
            19.99m,
            "USD",
            $"Description for benchmark product {counter}");

        var result = await Mediator.Send(command);
        return result.IsSuccess;
    }

    #endregion

    #region Combined Operations Benchmarks

    [Benchmark(Description = "Create and Read Customer")]
    public async Task<bool> CreateAndReadCustomer()
    {
        var counter = Interlocked.Increment(ref _customerCounter);
        var createCommand = new CreateCustomerCommand(
            $"First{counter}",
            $"Last{counter}",
            $"newcustomer{counter}@benchmark.com");

        var createResult = await Mediator.Send(createCommand);
        if (!createResult.IsSuccess) return false;

        var readQuery = new GetCustomerByIdQuery(createResult.Value.Id);
        var readResult = await Mediator.Send(readQuery);
        return readResult.IsSuccess;
    }

    [Benchmark(Description = "Create and Read Product")]
    public async Task<bool> CreateAndReadProduct()
    {
        var counter = Interlocked.Increment(ref _productCounter);
        var createCommand = new CreateProductCommand(
            $"Product {counter}",
            $"SKU-CR-{counter}",
            29.99m,
            "USD");

        var createResult = await Mediator.Send(createCommand);
        if (!createResult.IsSuccess) return false;

        var readQuery = new GetProductByIdQuery(createResult.Value.Id);
        var readResult = await Mediator.Send(readQuery);
        return readResult.IsSuccess;
    }

    #endregion

    #region Pagination Performance Benchmarks

    [Benchmark(Description = "GetCustomers - Deep pagination (page 10)")]
    public async Task<int> GetCustomers_DeepPagination()
    {
        var query = new GetCustomersQuery(Page: 10, PageSize: 20);
        var result = await Mediator.Send(query);
        return result.TotalCount;
    }

    [Benchmark(Description = "GetProducts - Deep pagination (page 10)")]
    public async Task<int> GetProducts_DeepPagination()
    {
        var query = new GetProductsQuery(Page: 10, PageSize: 20);
        var result = await Mediator.Send(query);
        return result.TotalCount;
    }

    #endregion
}
