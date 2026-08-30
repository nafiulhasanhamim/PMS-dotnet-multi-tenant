# PMS.Persistence

Entity Framework Core persistence layer implementation with support for multiple database contexts.

## Features

- **Multiple DbContext Support**: Type-safe repositories for different databases
- **Repository Pattern**: Using Ardalis.Specification for flexible querying
- **Unit of Work**: Transaction management across repositories
- **Soft Delete**: Automatic query filters for soft-deleted entities
- **Audit Fields**: Automatic population of CreatedOnUtc, ModifiedOnUtc, etc.
- **Domain Events**: Automatic dispatch after SaveChanges

## Registration

### Single Database (Simple)

```csharp
// In Program.cs
builder.Services.AddPersistence(builder.Configuration);
```

### Multiple Databases

```csharp
// Primary + Reporting databases
builder.Services.AddPersistenceWithReporting(builder.Configuration);

// Or add separately
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddReportingDatabase(builder.Configuration);
```

### Connection Strings (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=PMS;Trusted_Connection=True;",
    "ReportingConnection": "Server=read-replica;Database=PMS;Trusted_Connection=True;"
  }
}
```

## Usage Examples

### Single Database Pattern

For simple applications with one database:

```csharp
public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, Result<Guid>>
{
    private readonly IRepository<Customer> _customerRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerHandler(
        IRepository<Customer> customerRepo,
        IUnitOfWork unitOfWork)
    {
        _customerRepo = customerRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        var customer = new Customer(command.FirstName, command.LastName, command.Email);
        await _customerRepo.AddAsync(customer, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return customer.Id;
    }
}
```

### Multi-Database Pattern

For applications with multiple databases (e.g., primary + read replica):

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    // Write to primary database
    private readonly IRepository<Order, IApplicationDbContext> _orderRepo;
    private readonly IRepository<Product, IApplicationDbContext> _productRepo;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public CreateOrderHandler(
        IRepository<Order, IApplicationDbContext> orderRepo,
        IRepository<Product, IApplicationDbContext> productRepo,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var product = await _productRepo.GetByIdAsync(command.ProductId, ct);
            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Product", command.ProductId));

            var order = new Order(command.CustomerId, command.ShippingAddress);
            order.AddItem(product.Id, product.Name, product.Price, command.Quantity);

            await _orderRepo.AddAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return order.Id;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
```

### Read from Reporting Database

For read-heavy operations using a read replica:

```csharp
public class GetSalesReportHandler : IRequestHandler<GetSalesReportQuery, SalesReportDto>
{
    // Read from reporting database (read replica)
    private readonly IReadRepository<Order, IReportingDbContext> _reportingRepo;

    public GetSalesReportHandler(IReadRepository<Order, IReportingDbContext> reportingRepo)
    {
        _reportingRepo = reportingRepo;
    }

    public async Task<SalesReportDto> Handle(GetSalesReportQuery query, CancellationToken ct)
    {
        // Uses read replica - optimized for read-heavy operations
        var orders = await _reportingRepo.ListAsync(
            new OrdersByDateRangeSpec(query.StartDate, query.EndDate), ct);

        return MapToReport(orders);
    }
}
```

### Mixed Read/Write Operations

```csharp
public class ProcessOrderHandler : IRequestHandler<ProcessOrderCommand, Result>
{
    // Write operations use primary database
    private readonly IRepository<Order, IApplicationDbContext> _orderRepo;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    // Heavy reads can use reporting database
    private readonly IReadRepository<Customer, IReportingDbContext> _customerReadRepo;

    public ProcessOrderHandler(
        IRepository<Order, IApplicationDbContext> orderRepo,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        IReadRepository<Customer, IReportingDbContext> customerReadRepo)
    {
        _orderRepo = orderRepo;
        _unitOfWork = unitOfWork;
        _customerReadRepo = customerReadRepo;
    }

    public async Task<Result> Handle(ProcessOrderCommand command, CancellationToken ct)
    {
        // Read customer info from replica (offload reads)
        var customer = await _customerReadRepo.FirstOrDefaultAsync(
            new CustomerByIdSpec(command.CustomerId), ct);

        if (customer is null)
            return Result.Failure(Error.NotFound("Customer", command.CustomerId));

        // Write to primary database
        var order = await _orderRepo.GetByIdAsync(command.OrderId, ct);
        order.StartProcessing();

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

## Architecture

```
Persistence/
├── Contexts/
│   ├── IApplicationDbContext.cs     # Primary DB marker interface
│   ├── ApplicationDbContext.cs      # Primary DB implementation
│   ├── IReportingDbContext.cs       # Reporting/replica marker interface
│   └── ReportingDbContext.cs        # Read-optimized implementation
├── Configurations/
│   ├── CustomerConfiguration.cs
│   ├── ProductConfiguration.cs
│   ├── OrderConfiguration.cs
│   └── OrderItemConfiguration.cs
├── Repositories/
│   ├── Repository.cs                # Generic repository
│   ├── ReadRepository.cs            # Read-only repository
│   ├── ApplicationRepository.cs     # ApplicationDbContext-specific
│   └── ReportingRepository.cs       # ReportingDbContext-specific
├── Common/
│   ├── UnitOfWork.cs
│   └── DomainEventDispatcher.cs
├── Interceptors/
│   ├── AuditableEntityInterceptor.cs
│   └── DomainEventDispatcherInterceptor.cs
└── DependencyInjection.cs
```

## Key Interfaces

| Interface | Description |
|-----------|-------------|
| `IRepository<TEntity>` | Full CRUD for single-database scenarios |
| `IRepository<TEntity, TContext>` | Full CRUD with type-safe context |
| `IReadRepository<TEntity>` | Read-only for single-database scenarios |
| `IReadRepository<TEntity, TContext>` | Read-only with type-safe context |
| `IUnitOfWork` | Transaction management (single-database) |
| `IUnitOfWork<TContext>` | Transaction management (multi-database) |
| `IApplicationDbContext` | Marker for primary database |
| `IReportingDbContext` | Marker for reporting/read replica |
