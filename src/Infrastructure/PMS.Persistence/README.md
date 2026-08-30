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
    private readonly IRepository<Supplier> _supplierRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerHandler(
        IRepository<Supplier> supplierRepo,
        IUnitOfWork unitOfWork)
    {
        _supplierRepo = supplierRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateCustomerCommand command, CancellationToken ct)
    {
        var supplier = new Supplier(command.FirstName, command.LastName, command.Email);
        await _supplierRepo.AddAsync(supplier, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return supplier.Id;
    }
}
```

### Multi-Database Pattern

For applications with multiple databases (e.g., primary + read replica):

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    // Write to primary database
    private readonly IRepository<Sale, IApplicationDbContext> _saleRepo;
    private readonly IRepository<Medicine, IApplicationDbContext> _medicineRepo;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public CreateOrderHandler(
        IRepository<Sale, IApplicationDbContext> saleRepo,
        IRepository<Medicine, IApplicationDbContext> medicineRepo,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _saleRepo = saleRepo;
        _medicineRepo = medicineRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var medicine = await _medicineRepo.GetByIdAsync(command.MedicineId, ct);
            if (medicine is null)
                return Result.Failure<Guid>(Error.NotFound("Medicine", command.MedicineId));

            var sale = new Sale(command.SupplierId, command.ShippingAddress);
            sale.AddItem(medicine.Id, medicine.Name, medicine.Price, command.Quantity);

            await _saleRepo.AddAsync(sale, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return sale.Id;
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
    private readonly IReadRepository<Sale, IReportingDbContext> _reportingRepo;

    public GetSalesReportHandler(IReadRepository<Sale, IReportingDbContext> reportingRepo)
    {
        _reportingRepo = reportingRepo;
    }

    public async Task<SalesReportDto> Handle(GetSalesReportQuery query, CancellationToken ct)
    {
        // Uses read replica - optimized for read-heavy operations
        var sales = await _reportingRepo.ListAsync(
            new OrdersByDateRangeSpec(query.StartDate, query.EndDate), ct);

        return MapToReport(sales);
    }
}
```

### Mixed Read/Write Operations

```csharp
public class ProcessOrderHandler : IRequestHandler<ProcessOrderCommand, Result>
{
    // Write operations use primary database
    private readonly IRepository<Sale, IApplicationDbContext> _saleRepo;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    // Heavy reads can use reporting database
    private readonly IReadRepository<Supplier, IReportingDbContext> _customerReadRepo;

    public ProcessOrderHandler(
        IRepository<Sale, IApplicationDbContext> saleRepo,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        IReadRepository<Supplier, IReportingDbContext> customerReadRepo)
    {
        _saleRepo = saleRepo;
        _unitOfWork = unitOfWork;
        _customerReadRepo = customerReadRepo;
    }

    public async Task<Result> Handle(ProcessOrderCommand command, CancellationToken ct)
    {
        // Read supplier info from replica (offload reads)
        var supplier = await _customerReadRepo.FirstOrDefaultAsync(
            new CustomerByIdSpec(command.SupplierId), ct);

        if (supplier is null)
            return Result.Failure(Error.NotFound("Supplier", command.SupplierId));

        // Write to primary database
        var sale = await _saleRepo.GetByIdAsync(command.SaleId, ct);
        sale.StartProcessing();

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
