# PMS - Enterprise Clean Architecture Template

A production-ready .NET 8 solution template implementing Clean Architecture with CQRS, Domain-Driven Design, and full observability support.

## Table of Contents

1. [Overview](#overview)
2. [Quick Start](#quick-start)
3. [Architecture](#architecture)
4. [Project Structure](#project-structure)
5. [Core Patterns](#core-patterns)
6. [Layer Guide](#layer-guide)
7. [CQRS Implementation](#cqrs-implementation)
8. [Domain-Driven Design](#domain-driven-design)
9. [Repository & Unit of Work](#repository--unit-of-work)
10. [Validation](#validation)
11. [Error Handling](#error-handling)
12. [Mapping](#mapping)
13. [Caching](#caching)
14. [Multi-Database Support](#multi-database-support)
15. [Observability & Aspire](#observability--aspire)
16. [Testing](#testing)
17. [Configuration](#configuration)
18. [Common Scenarios](#common-scenarios)
19. [Best Practices](#best-practices)
20. [Troubleshooting](#troubleshooting)

---

## Overview

### What is PMS?

PMS is an enterprise-grade solution template that provides:

| Feature | Description |
|---------|-------------|
| **Clean Architecture** | Clear separation of concerns with dependency inversion |
| **CQRS Pattern** | Command Query Responsibility Segregation via MediatR |
| **Domain-Driven Design** | Aggregates, Value Objects, Domain Events |
| **Repository Pattern** | Ardalis.Specification for flexible queries |
| **Result Pattern** | Railway-oriented programming for error handling |
| **Validation Pipeline** | FluentValidation integrated into MediatR |
| **Observability** | OpenTelemetry + .NET Aspire Dashboard |
| **Multi-Database** | Type-safe support for multiple databases |
| **Testing Suite** | Unit, Integration, Architecture, Performance tests |

### Key Benefits

- **Maintainability**: Clear boundaries between layers prevent spaghetti code
- **Testability**: Each layer can be tested in isolation
- **Scalability**: CQRS allows independent scaling of reads and writes
- **Flexibility**: Repository pattern allows swapping data stores
- **Observability**: Built-in tracing, metrics, and structured logging
- **Productivity**: Consistent patterns reduce cognitive load

---

## Quick Start

### Prerequisites

```bash
# .NET 8.0 SDK
dotnet --version  # Should be 8.0.x or higher

# Install Aspire workload (for observability dashboard)
dotnet workload install aspire

# Verify Aspire installation
dotnet workload list
```

### Run the Application

```bash
# Clone and navigate to project
cd PMS

# Restore packages
dotnet restore

# Run with Aspire Dashboard (recommended)
cd src/Aspire/PMS.AppHost
dotnet run

# Or run WebApi directly
cd src/Presentation/PMS.WebApi
dotnet run
```

### Access Points

| Endpoint | URL |
|----------|-----|
| Aspire Dashboard | http://localhost:15191 |
| Swagger UI | https://localhost:7001/swagger |
| Health Check | https://localhost:7001/health |

---

## Architecture

### Dependency Flow

```
┌──────────────────────────────────────────────────────────────────┐
│                         PRESENTATION                              │
│                      (PMS.WebApi)                       │
│           Controllers → ApiControllerBase → MediatR               │
└─────────────────────────────┬────────────────────────────────────┘
                              │ depends on
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                          APPLICATION                              │
│                   (PMS.Application)                     │
│        Commands/Queries → Handlers → DTOs → Validators            │
└─────────────────────────────┬────────────────────────────────────┘
                              │ depends on
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                            DOMAIN                                 │
│                     (PMS.Domain)                        │
│      Entities → Aggregates → Value Objects → Domain Events        │
└──────────────────────────────────────────────────────────────────┘
                              ▲
                              │ implements interfaces from
┌─────────────────────────────┴────────────────────────────────────┐
│                        INFRASTRUCTURE                             │
│  ┌─────────────────────┐    ┌─────────────────────────────────┐  │
│  │     PERSISTENCE     │    │        INFRASTRUCTURE           │  │
│  │  - DbContext        │    │  - Email Service                │  │
│  │  - Repositories     │    │  - Cache Service                │  │
│  │  - Unit of Work     │    │  - File Storage                 │  │
│  │  - Interceptors     │    │  - HTTP Clients                 │  │
│  └─────────────────────┘    └─────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
                              ▲
                              │ shared by all layers
┌─────────────────────────────┴────────────────────────────────────┐
│                        SHARED KERNEL                              │
│                   (PMS.SharedKernel)                    │
│     Base Classes → Interfaces → Behaviors → Result Pattern        │
└──────────────────────────────────────────────────────────────────┘
```

### The Dependency Rule

> **Dependencies only point inward. Nothing in an inner layer can know about an outer layer.**

- **Domain** knows nothing about Application, Infrastructure, or Presentation
- **Application** knows only about Domain (and SharedKernel)
- **Infrastructure** implements interfaces defined in inner layers
- **Presentation** orchestrates everything but contains no business logic

---

## Project Structure

```
PMS/
├── src/
│   ├── Core/                                    # Inner layers (pure business logic)
│   │   ├── PMS.SharedKernel/          # Shared base classes, interfaces
│   │   ├── PMS.Domain/                # Domain entities, value objects
│   │   └── PMS.Application/           # CQRS handlers, validators
│   │
│   ├── Infrastructure/                          # Outer layers (I/O, frameworks)
│   │   ├── PMS.Infrastructure/        # Services (email, cache, HTTP)
│   │   └── PMS.Persistence/           # EF Core, repositories
│   │
│   ├── Presentation/                            # Entry point
│   │   └── PMS.WebApi/                # ASP.NET Core API
│   │
│   └── Aspire/                                  # Observability
│       ├── PMS.ServiceDefaults/       # OpenTelemetry configuration
│       └── PMS.AppHost/               # Dashboard orchestration
│
├── tests/
│   ├── PMS.UnitTests/                 # Domain & handler tests
│   ├── PMS.IntegrationTests/          # API endpoint tests
│   ├── PMS.ArchitectureTests/         # NetArchTest rules
│   ├── PMS.SchemaTests/               # Database schema validation
│   └── PMS.PerformanceTests/          # Benchmarks & load tests
│
├── tools/
│   └── PMS.MigrationTools/            # EF Core migrations
│
├── docs/                                        # Documentation
│   ├── README.md                                # This file
│   ├── aspire-integration-guide.md              # Aspire setup guide
│   └── aspire-dashboard.md                      # Dashboard usage
│
├── Directory.Packages.props                     # Central package management
├── Directory.Build.props                        # Common build settings
└── PMS.sln                            # Solution file
```

---

## Core Patterns

### Pattern Summary

| Pattern | Purpose | Implementation |
|---------|---------|----------------|
| **CQRS** | Separate read/write models | MediatR Commands & Queries |
| **Repository** | Abstract data access | `IRepository<T, TContext>` |
| **Unit of Work** | Transaction management | `IUnitOfWork<TContext>` |
| **Specification** | Encapsulate query logic | Ardalis.Specification |
| **Domain Events** | Decouple side effects | `DomainEvent` + `INotification` |
| **Result Pattern** | Explicit error handling | `Result<T>` |
| **Value Objects** | Immutable domain concepts | `ValueObject` base class |
| **Aggregate Root** | Consistency boundary | `AggregateRoot<TId>` |
| **Soft Delete** | Logical deletion | `ISoftDelete` interface |
| **Audit Trail** | Track changes | `IAuditable` interface |

---

## Layer Guide

### SharedKernel

The foundation layer providing base classes and interfaces used across all layers.

#### Base Classes

```csharp
// Entity with identity and domain events
public abstract class BaseEntity<TId> : IEquatable<BaseEntity<TId>>
{
    public TId Id { get; protected set; }
    private readonly List<DomainEvent> _domainEvents = new();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

// Aggregate root with optimistic concurrency
public abstract class AggregateRoot<TId> : BaseEntity<TId>
{
    public byte[] RowVersion { get; protected set; } = Array.Empty<byte>();
}

// Value object compared by values
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj) =>
        obj is ValueObject other &&
        GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
}
```

#### Key Interfaces

```csharp
// Repository pattern with specification support
public interface IRepository<TEntity, TContext> : IReadRepository<TEntity, TContext>
    where TEntity : class
    where TContext : IDbContext
{
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(TEntity entity, CancellationToken ct = default);
}

// Read-only repository (no tracking)
public interface IReadRepository<TEntity, TContext>
    where TEntity : class
    where TContext : IDbContext
{
    Task<TEntity?> GetByIdAsync<TId>(TId id, CancellationToken ct = default);
    Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> spec, CancellationToken ct = default);
    Task<List<TEntity>> ListAsync(ISpecification<TEntity> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<TEntity> spec, CancellationToken ct = default);
    IQueryable<TEntity> Query();
}

// Unit of work for transaction management
public interface IUnitOfWork<TContext> where TContext : IDbContext
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}

// Audit trail markers
public interface IAuditable
{
    DateTime CreatedOnUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? ModifiedOnUtc { get; set; }
    string? ModifiedBy { get; set; }
}

// Soft delete marker
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedOnUtc { get; set; }
    string? DeletedBy { get; set; }
}
```

#### Lifetime Markers (Auto DI Registration)

```csharp
// Services implementing these interfaces are auto-registered
public interface ITransientService { }  // New instance per resolution
public interface IScopedService { }     // One per HTTP request
public interface ISingletonService { }  // One per application lifetime

// Example usage
public interface IEmailService : IScopedService
{
    Task SendAsync(string to, string subject, string body);
}

// Auto-registered as: services.AddScoped<IEmailService, EmailService>();
```

### Domain Layer

Pure business logic with no framework dependencies.

#### Entity Example

```csharp
// Domain/Entities/Customer.cs
public sealed class Customer : AggregateRoot<int>, IAuditable, ISoftDelete
{
    public string Name { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public CustomerStatus Status { get; private set; }

    // Audit fields (set by interceptor)
    public DateTime CreatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public string? ModifiedBy { get; set; }

    // Soft delete fields
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public string? DeletedBy { get; set; }

    private Customer() { } // EF Core constructor

    // Factory method enforcing invariants
    public static Customer Create(string name, Email email)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Null(email);

        var customer = new Customer
        {
            Name = name,
            Email = email,
            Status = CustomerStatus.Active
        };

        customer.AddDomainEvent(new CustomerCreatedEvent(customer.Id, email.Value));
        return customer;
    }

    // Behavior methods (not anemic)
    public void UpdateEmail(Email newEmail)
    {
        Guard.Against.Null(newEmail);

        if (Email == newEmail) return;

        var oldEmail = Email;
        Email = newEmail;

        AddDomainEvent(new CustomerEmailChangedEvent(Id, oldEmail.Value, newEmail.Value));
    }

    public void Suspend()
    {
        if (Status == CustomerStatus.Suspended)
            throw new BusinessRuleViolationException("Customer is already suspended");

        Status = CustomerStatus.Suspended;
    }

    public void Activate()
    {
        if (Status == CustomerStatus.Active)
            throw new BusinessRuleViolationException("Customer is already active");

        Status = CustomerStatus.Active;
    }
}
```

#### Value Object Example

```csharp
// Domain/ValueObjects/Email.cs
public sealed class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string email)
    {
        Guard.Against.NullOrWhiteSpace(email);

        if (!IsValidEmail(email))
            throw new DomainException($"Invalid email format: {email}");

        return new Email(email.ToLowerInvariant());
    }

    private static bool IsValidEmail(string email) =>
        Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    // Implicit conversion for convenience
    public static implicit operator string(Email email) => email.Value;
}
```

#### Domain Event Example

```csharp
// Domain/Events/CustomerCreatedEvent.cs
public record CustomerCreatedEvent(int CustomerId, string Email) : DomainEvent;

// Application/Features/Customers/EventHandlers/CustomerCreatedEventHandler.cs
public class CustomerCreatedEventHandler : INotificationHandler<CustomerCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<CustomerCreatedEventHandler> _logger;

    public CustomerCreatedEventHandler(IEmailService emailService, ILogger<CustomerCreatedEventHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(CustomerCreatedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation("Sending welcome email to {Email}", notification.Email);

        await _emailService.SendAsync(
            notification.Email,
            "Welcome!",
            "Thank you for registering with us.");
    }
}
```

### Application Layer

CQRS handlers, validators, and DTOs.

#### Folder Structure

```
Application/
├── Features/
│   ├── Customers/
│   │   ├── Commands/
│   │   │   ├── CreateCustomer/
│   │   │   │   ├── CreateCustomerCommand.cs
│   │   │   │   ├── CreateCustomerCommand.Validator.cs
│   │   │   │   └── CreateCustomerCommandHandler.cs
│   │   │   ├── UpdateCustomer/
│   │   │   └── DeleteCustomer/
│   │   ├── Queries/
│   │   │   ├── GetCustomerById/
│   │   │   └── GetCustomers/
│   │   ├── DTOs/
│   │   │   └── CustomerDto.cs
│   │   ├── Specifications/
│   │   │   ├── CustomerByIdSpec.cs
│   │   │   └── CustomersListSpec.cs
│   │   └── EventHandlers/
│   │       └── CustomerCreatedEventHandler.cs
│   ├── Orders/
│   └── Products/
└── Common/
    ├── Behaviors/
    ├── Mappings/
    └── Interfaces/
```

---

## CQRS Implementation

### Command Example

```csharp
// Application/Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs
public record CreateCustomerCommand(
    string Name,
    string Email,
    string? Phone
) : IRequest<Result<CustomerDto>>;
```

### Command Validator

```csharp
// CreateCustomerCommand.Validator.cs
public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[\d\s-]+$")
            .When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Invalid phone format");
    }
}
```

### Command Handler

```csharp
// CreateCustomerCommandHandler.cs
public class CreateCustomerCommandHandler
    : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly IRepository<Customer, IApplicationDbContext> _repository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateCustomerCommandHandler> _logger;

    public CreateCustomerCommandHandler(
        IRepository<Customer, IApplicationDbContext> repository,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        IMapper mapper,
        ILogger<CreateCustomerCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<CustomerDto>> Handle(
        CreateCustomerCommand request,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating customer: {Email}", request.Email);

        // Check for duplicate email
        var existingCustomer = await _repository.FirstOrDefaultAsync(
            new CustomerByEmailSpec(request.Email), ct);

        if (existingCustomer is not null)
            return Result<CustomerDto>.Failure(
                Error.Conflict("Customer.DuplicateEmail", "A customer with this email already exists"));

        // Create domain entity using factory method
        var email = Email.Create(request.Email);
        var customer = Customer.Create(request.Name, email);

        // Persist
        await _repository.AddAsync(customer, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Created customer {Id}", customer.Id);

        // Return DTO
        return Result<CustomerDto>.Success(_mapper.Map<CustomerDto>(customer));
    }
}
```

### Query Example

```csharp
// GetCustomersQuery.cs
public record GetCustomersQuery(
    string? SearchTerm,
    CustomerStatus? Status,
    int Page = 1,
    int PageSize = 10
) : IRequest<Result<PagedList<CustomerDto>>>;

// GetCustomersQueryHandler.cs
public class GetCustomersQueryHandler
    : IRequestHandler<GetCustomersQuery, Result<PagedList<CustomerDto>>>
{
    private readonly IReadRepository<Customer, IApplicationDbContext> _repository;
    private readonly IMapper _mapper;

    public GetCustomersQueryHandler(
        IReadRepository<Customer, IApplicationDbContext> repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Result<PagedList<CustomerDto>>> Handle(
        GetCustomersQuery request,
        CancellationToken ct)
    {
        var spec = new CustomersListSpec(
            request.SearchTerm,
            request.Status,
            request.Page,
            request.PageSize);

        var customers = await _repository.ListAsync(spec, ct);
        var totalCount = await _repository.CountAsync(spec.WithoutPaging(), ct);

        var dtos = _mapper.Map<List<CustomerDto>>(customers);

        return Result<PagedList<CustomerDto>>.Success(
            new PagedList<CustomerDto>(dtos, totalCount, request.Page, request.PageSize));
    }
}
```

### Specification Example

```csharp
// CustomersListSpec.cs
public class CustomersListSpec : Specification<Customer>
{
    public CustomersListSpec(
        string? searchTerm,
        CustomerStatus? status,
        int page,
        int pageSize)
    {
        // Search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            Query.Where(c =>
                c.Name.Contains(searchTerm) ||
                c.Email.Value.Contains(searchTerm));
        }

        // Status filter
        if (status.HasValue)
        {
            Query.Where(c => c.Status == status.Value);
        }

        // Sorting
        Query.OrderBy(c => c.Name);

        // Pagination
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    // For getting total count without pagination
    public CustomersListSpec WithoutPaging()
    {
        Query.Skip(0).Take(int.MaxValue);
        return this;
    }
}
```

### MediatR Pipeline Behaviors

Behaviors execute in registration order (outermost to innermost):

```
Request
    │
    ▼
┌───────────────────────────────┐
│ UnhandledExceptionBehavior    │  ← Catches & logs exceptions
├───────────────────────────────┤
│ LoggingBehavior               │  ← Logs request start/end + timing
├───────────────────────────────┤
│ PerformanceBehavior           │  ← Warns if >500ms
├───────────────────────────────┤
│ ValidationBehavior            │  ← Runs FluentValidation
├───────────────────────────────┤
│ AccessLogBehavior             │  ← Logs audit trail on success
├───────────────────────────────┤
│        Handler                │  ← Business logic
└───────────────────────────────┘
    │
    ▼
Response
```

---

## Repository & Unit of Work

### Single Database Usage

```csharp
// Simple case: single database
public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Result>
{
    private readonly IRepository<Customer, IApplicationDbContext> _repository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken ct)
    {
        var customer = await _repository.GetByIdAsync(request.Id, ct);
        if (customer is null)
            return Result.Failure(Error.NotFound("Customer.NotFound", "Customer not found"));

        customer.UpdateEmail(Email.Create(request.Email));

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

### Multi-Database Usage

```csharp
// Advanced: reading from reporting database, writing to main database
public class GenerateReportCommandHandler : IRequestHandler<GenerateReportCommand, Result<ReportDto>>
{
    // Read from reporting replica
    private readonly IReadRepository<Order, IReportingDbContext> _reportingRepo;

    // Write to main database
    private readonly IRepository<Report, IApplicationDbContext> _reportRepo;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public async Task<Result<ReportDto>> Handle(GenerateReportCommand request, CancellationToken ct)
    {
        // Read aggregated data from reporting database (no locks on main DB)
        var orders = await _reportingRepo.ListAsync(
            new OrdersForReportSpec(request.StartDate, request.EndDate), ct);

        // Process and create report
        var report = Report.Generate(orders);

        // Write to main database
        await _reportRepo.AddAsync(report, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ReportDto>.Success(new ReportDto { /* ... */ });
    }
}
```

### Transaction Management

```csharp
// Explicit transaction control
public async Task<Result> Handle(ComplexOperationCommand request, CancellationToken ct)
{
    await _unitOfWork.BeginTransactionAsync(ct);

    try
    {
        // Multiple operations...
        await _repository.AddAsync(entity1, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _repository.AddAsync(entity2, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _unitOfWork.CommitTransactionAsync(ct);
        return Result.Success();
    }
    catch
    {
        await _unitOfWork.RollbackTransactionAsync(ct);
        throw;
    }
}
```

---

## Validation

### FluentValidation Integration

Validation runs automatically via `ValidationBehavior` before handlers execute.

```csharp
// Validator with complex rules
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    private readonly IReadRepository<Customer, IApplicationDbContext> _customerRepo;

    public CreateOrderCommandValidator(
        IReadRepository<Customer, IApplicationDbContext> customerRepo)
    {
        _customerRepo = customerRepo;

        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .MustAsync(CustomerExists)
            .WithMessage("Customer does not exist");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Order must have at least one item");

        RuleForEach(x => x.Items).SetValidator(new OrderItemValidator());
    }

    private async Task<bool> CustomerExists(int customerId, CancellationToken ct)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId, ct);
        return customer is not null;
    }
}

public class OrderItemValidator : AbstractValidator<OrderItemDto>
{
    public OrderItemValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
```

### Validation Response

Failed validation returns a structured error:

```json
{
  "isSuccess": false,
  "error": {
    "code": "Validation.Error",
    "description": "One or more validation errors occurred",
    "errors": [
      { "propertyName": "Name", "errorMessage": "Name is required" },
      { "propertyName": "Email", "errorMessage": "Invalid email format" }
    ]
  }
}
```

---

## Error Handling

### Result Pattern

The `Result<T>` pattern makes errors explicit in the type system.

```csharp
// Result types
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}

public class Result<TValue> : Result
{
    public TValue Value { get; }

    public static Result<TValue> Success(TValue value) => new(value);
    public static Result<TValue> Failure(Error error) => new(error);
}

// Error types
public record Error(string Code, string Description)
{
    public static Error None => new(string.Empty, string.Empty);
    public static Error NotFound(string code, string description) => new(code, description);
    public static Error Validation(string code, string description) => new(code, description);
    public static Error Conflict(string code, string description) => new(code, description);
    public static Error Unauthorized(string code, string description) => new(code, description);
    public static Error Forbidden(string code, string description) => new(code, description);
}
```

### Controller Result Handling

```csharp
// Controllers/ApiControllerBase.cs
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly IMediator Mediator;

    protected ApiControllerBase(IMediator mediator) => Mediator = mediator;

    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
            return NoContent();

        return HandleError(result.Error);
    }

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return HandleError(result.Error);
    }

    protected IActionResult HandleCreatedResult<T>(Result<T> result, string actionName, object routeValues)
    {
        if (result.IsSuccess)
            return CreatedAtAction(actionName, routeValues, result.Value);

        return HandleError(result.Error);
    }

    private IActionResult HandleError(Error error)
    {
        return error.Code switch
        {
            _ when error.Code.Contains("NotFound") => NotFound(error),
            _ when error.Code.Contains("Validation") => BadRequest(error),
            _ when error.Code.Contains("Conflict") => Conflict(error),
            _ when error.Code.Contains("Unauthorized") => Unauthorized(error),
            _ when error.Code.Contains("Forbidden") => Forbid(),
            _ => BadRequest(error)
        };
    }
}
```

### Usage in Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ApiControllerBase
{
    public CustomersController(IMediator mediator) : base(mediator) { }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command)
    {
        var result = await Mediator.Send(command);
        return HandleCreatedResult(result, nameof(GetById), new { id = result.Value?.Id });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Mediator.Send(new GetCustomerByIdQuery(id));
        return HandleResult(result);
    }
}
```

---

## Mapping

### Mapster Configuration

```csharp
// Application/Common/Mappings/MappingConfig.cs
public static class MappingConfig
{
    public static void RegisterMappings(this IServiceCollection services)
    {
        var config = TypeAdapterConfig.GlobalSettings;

        // Scan assembly for IMapFrom<T> implementations
        config.Scan(typeof(MappingConfig).Assembly);

        // Custom mappings
        config.NewConfig<Customer, CustomerDto>()
            .Map(dest => dest.Email, src => src.Email.Value)
            .Map(dest => dest.StatusName, src => src.Status.ToString());

        config.NewConfig<Order, OrderDto>()
            .Map(dest => dest.TotalAmount, src => src.Items.Sum(i => i.UnitPrice * i.Quantity));

        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();
    }
}

// Convention-based mapping interface
public interface IMapFrom<T>
{
    void Mapping(TypeAdapterConfig config) => config.NewConfig(typeof(T), GetType());
}

// DTO with custom mapping
public class CustomerDto : IMapFrom<Customer>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;

    public void Mapping(TypeAdapterConfig config)
    {
        config.NewConfig<Customer, CustomerDto>()
            .Map(d => d.Email, s => s.Email.Value)
            .Map(d => d.StatusName, s => s.Status.ToString());
    }
}
```

---

## Caching

### Cache Service

```csharp
// Infrastructure/Services/ICacheService.cs
public interface ICacheService : IScopedService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null,
        TimeSpan? absoluteExpiration = null, CancellationToken ct = default);
    Task<T?> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null,
        CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
```

### Usage in Handler

```csharp
public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IReadRepository<Product, IApplicationDbContext> _repository;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var cacheKey = $"product:{request.Id}";

        var dto = await _cache.GetOrSetAsync(
            cacheKey,
            async ct =>
            {
                var product = await _repository.GetByIdAsync(request.Id, ct);
                return product is null ? null : _mapper.Map<ProductDto>(product);
            },
            slidingExpiration: TimeSpan.FromMinutes(10),
            absoluteExpiration: TimeSpan.FromHours(1),
            ct);

        return dto is null
            ? Result<ProductDto>.Failure(Error.NotFound("Product.NotFound", "Product not found"))
            : Result<ProductDto>.Success(dto);
    }
}

// Don't forget to invalidate cache on updates!
public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
{
    private readonly ICacheService _cache;

    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        // ... update logic ...

        // Invalidate cache
        await _cache.RemoveAsync($"product:{request.Id}", ct);

        return Result.Success();
    }
}
```

---

## Multi-Database Support

### Configuration

```csharp
// Persistence/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Main database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.EnableRetryOnFailure(3)));

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }

    public static IServiceCollection AddReportingDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Reporting replica (read-only)
        services.AddDbContext<ReportingDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("ReportingConnection"),
                b => b.EnableRetryOnFailure(3))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddScoped<IReportingDbContext>(sp =>
            sp.GetRequiredService<ReportingDbContext>());

        return services;
    }
}
```

### Usage Pattern

```csharp
// Inject the context-specific repository
public class MyHandler : IRequestHandler<MyQuery, Result<MyDto>>
{
    // Use IReadRepository for queries
    private readonly IReadRepository<Entity, IReportingDbContext> _reportingRepo;

    // Use IRepository for commands
    private readonly IRepository<Entity, IApplicationDbContext> _writeRepo;
}
```

---

## Observability & Aspire

### ServiceDefaults Configuration

```csharp
// ServiceDefaults/Extensions.cs
public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddEntityFrameworkCoreInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }
}
```

### AppHost Configuration

```csharp
// AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Add resources
var sql = builder.AddSqlServer("sql")
    .AddDatabase("PMSDb");

var redis = builder.AddRedis("cache");

// Add projects with references
builder.AddProject<Projects.PMS_WebApi>("webapi")
    .WithReference(sql)
    .WithReference(redis);

builder.Build().Run();
```

### What You See in Dashboard

| Tab | Information |
|-----|-------------|
| **Resources** | All services with clickable Swagger/health endpoints |
| **Console** | Live logs from all services |
| **Traces** | Request flow: HTTP → MediatR → EF Core → SQL |
| **Metrics** | Request rates, latency, memory, GC stats |
| **Structured Logs** | Correlated logs with trace IDs |

---

## Testing

### Architecture Tests

```csharp
// ArchitectureTests/LayerDependencyTests.cs
public class LayerDependencyTests
{
    [Fact]
    public void Domain_Should_Not_Depend_On_Application()
    {
        var result = Types.InAssembly(typeof(Customer).Assembly)
            .Should()
            .NotHaveDependencyOn("PMS.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types.InAssembly(typeof(CreateCustomerCommand).Assembly)
            .Should()
            .NotHaveDependencyOn("PMS.Infrastructure")
            .And()
            .NotHaveDependencyOn("PMS.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_Should_End_With_Handler()
    {
        var result = Types.InAssembly(typeof(CreateCustomerCommandHandler).Assembly)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
```

### Integration Tests

```csharp
// IntegrationTests/CustomersEndpointTests.cs
public class CustomersEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateCustomer_WithValidData_ReturnsCreated()
    {
        // Arrange
        var command = new CreateCustomerCommand("John Doe", "john@example.com", null);

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();
        customer.Should().NotBeNull();
        customer!.Name.Should().Be("John Doe");
        customer.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateCustomerCommand("John Doe", "invalid-email", null);

        // Act
        var response = await Client.PostAsJsonAsync("/api/customers", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

### Unit Tests

```csharp
// UnitTests/Handlers/CreateCustomerCommandHandlerTests.cs
public class CreateCustomerCommandHandlerTests
{
    private readonly Mock<IRepository<Customer, IApplicationDbContext>> _repositoryMock;
    private readonly Mock<IUnitOfWork<IApplicationDbContext>> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly CreateCustomerCommandHandler _handler;

    public CreateCustomerCommandHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<Customer, IApplicationDbContext>>();
        _unitOfWorkMock = new Mock<IUnitOfWork<IApplicationDbContext>>();
        _mapperMock = new Mock<IMapper>();

        _handler = new CreateCustomerCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _mapperMock.Object,
            Mock.Of<ILogger<CreateCustomerCommandHandler>>());
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessResult()
    {
        // Arrange
        var command = new CreateCustomerCommand("John Doe", "john@example.com", null);
        _mapperMock.Setup(m => m.Map<CustomerDto>(It.IsAny<Customer>()))
            .Returns(new CustomerDto { Id = 1, Name = "John Doe" });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("John Doe");
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Customer>(), default), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
```

---

## Configuration

### appsettings.json Structure

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=PMS;Trusted_Connection=True;TrustServerCertificate=True",
    "ReportingConnection": "Server=.;Database=PMS_Reporting;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "PMS:"
  },
  "Email": {
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "Username": "user@example.com",
    "Password": "your-password",
    "FromAddress": "noreply@example.com",
    "FromName": "PMS"
  },
  "FileStorage": {
    "BasePath": "uploads",
    "MaxFileSizeBytes": 10485760
  },
  "HttpClientPolicies": {
    "RetryCount": 3,
    "RetryDelayMilliseconds": 500,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### DI Registration in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add layers
builder.Services
    .AddApplication()            // MediatR, FluentValidation, Mapster
    .AddPersistence(config)      // EF Core, Repositories
    .AddInfrastructure(config);  // Services (Email, Cache, etc.)

// Add presentation concerns
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Add Aspire ServiceDefaults
builder.AddServiceDefaults();

var app = builder.Build();

// Configure pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.UseHealthChecks("/health");
app.MapControllers();

app.Run();
```

---

## Common Scenarios

### Scenario 1: Adding a New Feature

1. **Create Domain Entity** (if new)
   ```
   Domain/Entities/Invoice.cs
   ```

2. **Create Command/Query**
   ```
   Application/Features/Invoices/Commands/CreateInvoice/
   ├── CreateInvoiceCommand.cs
   ├── CreateInvoiceCommand.Validator.cs
   └── CreateInvoiceCommandHandler.cs
   ```

3. **Create DTO**
   ```
   Application/Features/Invoices/DTOs/InvoiceDto.cs
   ```

4. **Create Specification** (if needed)
   ```
   Application/Features/Invoices/Specifications/InvoiceByIdSpec.cs
   ```

5. **Add Controller**
   ```
   WebApi/Controllers/InvoicesController.cs
   ```

### Scenario 2: Adding External API Integration

1. **Define Interface in Application**
   ```csharp
   // Application/Common/Interfaces/IPaymentGateway.cs
   public interface IPaymentGateway : IScopedService
   {
       Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request);
   }
   ```

2. **Implement in Infrastructure**
   ```csharp
   // Infrastructure/Services/StripePaymentGateway.cs
   public class StripePaymentGateway : IPaymentGateway
   {
       private readonly IHttpClientService _http;

       public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
       {
           var response = await _http.PostAsync<PaymentRequest, PaymentResponse>(
               "/v1/charges",
               request,
               clientName: "Stripe");

           return response.IsSuccess
               ? PaymentResult.Success(response.Data.ChargeId)
               : PaymentResult.Failure(response.Error);
       }
   }
   ```

3. **Register HTTP Client**
   ```csharp
   services.AddResilientHttpClient("Stripe", options =>
   {
       options.BaseAddress = new Uri("https://api.stripe.com");
       options.DefaultHeaders.Add("Authorization", $"Bearer {apiKey}");
   });
   ```

### Scenario 3: Adding Caching to a Query

```csharp
public class GetPopularProductsQueryHandler
    : IRequestHandler<GetPopularProductsQuery, Result<List<ProductDto>>>
{
    private readonly IReadRepository<Product, IApplicationDbContext> _repo;
    private readonly ICacheService _cache;

    public async Task<Result<List<ProductDto>>> Handle(
        GetPopularProductsQuery request,
        CancellationToken ct)
    {
        return await _cache.GetOrSetAsync(
            "products:popular",
            async ct =>
            {
                var products = await _repo.ListAsync(
                    new PopularProductsSpec(request.Count), ct);
                return Result<List<ProductDto>>.Success(
                    products.Select(p => new ProductDto(p)).ToList());
            },
            slidingExpiration: TimeSpan.FromMinutes(5),
            ct);
    }
}
```

---

## Best Practices

### DO

| Practice | Reason |
|----------|--------|
| Use factory methods for entity creation | Enforce invariants at construction |
| Return `Result<T>` from handlers | Make errors explicit in type system |
| Use specifications for queries | Encapsulate query logic, enable reuse |
| Keep handlers focused | One command/query = one handler |
| Use value objects for domain concepts | Type safety and validation |
| Add validators for all commands | Fail fast with clear error messages |
| Use `IReadRepository` for queries | No tracking overhead |
| Invalidate cache on writes | Prevent stale data |
| Log correlation IDs | Enable distributed tracing |
| Test architecture rules | Prevent dependency violations |

### DON'T

| Anti-Pattern | Why |
|--------------|-----|
| Business logic in controllers | Violates single responsibility |
| Domain entities with public setters | Bypasses invariant checks |
| Throwing exceptions for expected errors | Use Result pattern instead |
| Direct DbContext usage in handlers | Use repository abstraction |
| Large handler methods | Split into smaller, focused handlers |
| Caching without invalidation strategy | Leads to stale data |
| Ignoring validation errors | Check `result.IsSuccess` before using value |
| Hard-coded connection strings | Use configuration |
| Mixing read/write concerns | Separate commands and queries |
| Skipping architecture tests | Dependencies will leak |

### Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Command | `{Verb}{Entity}Command` | `CreateCustomerCommand` |
| Query | `Get{Entity}(By{Criteria})?Query` | `GetCustomerByIdQuery` |
| Handler | `{Command/Query}Handler` | `CreateCustomerCommandHandler` |
| Validator | `{Command}Validator` | `CreateCustomerCommandValidator` |
| Specification | `{Entity}{Criteria}Spec` | `CustomerByEmailSpec` |
| DTO | `{Entity}Dto` | `CustomerDto` |
| Event | `{Entity}{Action}Event` | `CustomerCreatedEvent` |

---

## Troubleshooting

### Common Issues

#### "Handler not found"

**Cause**: Handler not registered in MediatR

**Solution**: Ensure handler assembly is scanned:
```csharp
services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateCustomerCommandHandler).Assembly));
```

#### "Validation not running"

**Cause**: ValidationBehavior not registered

**Solution**: Ensure behaviors are registered:
```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

#### "DbContext disposed"

**Cause**: Using DbContext outside of scope

**Solution**: Use scoped services, don't store DbContext in singleton

#### "Soft deleted entities appearing"

**Cause**: Query filters not applied

**Solution**: Ensure entity implements `ISoftDelete` and configuration applies filter:
```csharp
builder.HasQueryFilter(e => !e.IsDeleted);
```

#### "Audit fields not populated"

**Cause**: Interceptor not registered

**Solution**: Add interceptor to DbContext options:
```csharp
options.AddInterceptors(new AuditableEntityInterceptor(currentUserService, dateTime));
```

#### "Domain events not dispatched"

**Cause**: DomainEventDispatcherInterceptor missing

**Solution**: Add interceptor to DbContext options:
```csharp
options.AddInterceptors(new DomainEventDispatcherInterceptor(mediator));
```

---

## Links

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [Ardalis.Specification](https://github.com/ardalis/Specification)
- [FluentValidation](https://docs.fluentvalidation.net/)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)

---

## License

MIT License - See [LICENSE](../LICENSE) for details.
