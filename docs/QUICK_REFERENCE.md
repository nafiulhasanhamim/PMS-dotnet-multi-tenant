# PMS Quick Reference

A concise reference for common patterns and code snippets.

---

## Creating a New Feature

### 1. Command (Write Operation)

```csharp
// Application/Features/{Module}/Commands/{Action}/

// 1. Command
public record Create{Entity}Command(
    string Name,
    string Email
) : IRequest<Result<{Entity}Dto>>;

// 2. Validator
public class Create{Entity}CommandValidator : AbstractValidator<Create{Entity}Command>
{
    public Create{Entity}CommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

// 3. Handler
public class Create{Entity}CommandHandler
    : IRequestHandler<Create{Entity}Command, Result<{Entity}Dto>>
{
    private readonly IRepository<{Entity}, IApplicationDbContext> _repo;
    private readonly IUnitOfWork<IApplicationDbContext> _uow;
    private readonly IMapper _mapper;

    public async Task<Result<{Entity}Dto>> Handle(
        Create{Entity}Command request, CancellationToken ct)
    {
        var entity = {Entity}.Create(request.Name, Email.Create(request.Email));

        await _repo.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<{Entity}Dto>.Success(_mapper.Map<{Entity}Dto>(entity));
    }
}
```

### 2. Query (Read Operation)

```csharp
// Application/Features/{Module}/Queries/{Action}/

// 1. Query
public record Get{Entity}ByIdQuery(int Id) : IRequest<Result<{Entity}Dto>>;

// 2. Handler (no validator needed for simple queries)
public class Get{Entity}ByIdQueryHandler
    : IRequestHandler<Get{Entity}ByIdQuery, Result<{Entity}Dto>>
{
    private readonly IReadRepository<{Entity}, IApplicationDbContext> _repo;
    private readonly IMapper _mapper;

    public async Task<Result<{Entity}Dto>> Handle(
        Get{Entity}ByIdQuery request, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(request.Id, ct);

        return entity is null
            ? Result<{Entity}Dto>.Failure(Error.NotFound("{Entity}.NotFound", "Not found"))
            : Result<{Entity}Dto>.Success(_mapper.Map<{Entity}Dto>(entity));
    }
}
```

---

## Entity Patterns

### Basic Entity

```csharp
public class Customer : AggregateRoot<int>, IAuditable
{
    public string Name { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;

    // Audit fields
    public DateTime CreatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public string? ModifiedBy { get; set; }

    private Customer() { }

    public static Customer Create(string name, Email email)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.Null(email);

        return new Customer { Name = name, Email = email };
    }

    public void UpdateEmail(Email newEmail)
    {
        Guard.Against.Null(newEmail);
        Email = newEmail;
        AddDomainEvent(new CustomerEmailChangedEvent(Id, newEmail.Value));
    }
}
```

### Value Object

```csharp
public sealed class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string email)
    {
        Guard.Against.NullOrWhiteSpace(email);
        if (!IsValid(email))
            throw new DomainException($"Invalid email: {email}");
        return new Email(email.ToLowerInvariant());
    }

    private static bool IsValid(string email) =>
        Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

---

## Repository Patterns

### Basic CRUD

```csharp
// Read by ID
var entity = await _repo.GetByIdAsync(id, ct);

// Add
await _repo.AddAsync(entity, ct);
await _uow.SaveChangesAsync(ct);

// Update
entity.Update(newValue);
await _uow.SaveChangesAsync(ct);

// Delete
await _repo.DeleteAsync(entity, ct);
await _uow.SaveChangesAsync(ct);
```

### Using Specifications

```csharp
// Define specification
public class ActiveCustomersSpec : Specification<Customer>
{
    public ActiveCustomersSpec(string? search, int page, int pageSize)
    {
        Query.Where(c => c.Status == CustomerStatus.Active);

        if (!string.IsNullOrEmpty(search))
            Query.Where(c => c.Name.Contains(search));

        Query.OrderBy(c => c.Name)
             .Skip((page - 1) * pageSize)
             .Take(pageSize);
    }
}

// Use specification
var customers = await _repo.ListAsync(new ActiveCustomersSpec(search, 1, 10), ct);
var count = await _repo.CountAsync(new ActiveCustomersSpec(search, 1, int.MaxValue), ct);
```

### Using IQueryable

```csharp
var query = _repo.Query()
    .Where(c => c.Status == CustomerStatus.Active);

if (!string.IsNullOrEmpty(search))
    query = query.Where(c => c.Name.Contains(search));

var results = await query
    .OrderBy(c => c.Name)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(ct);
```

---

## Validation Patterns

### Basic Rules

```csharp
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        // Required
        RuleFor(x => x.CustomerId).NotEmpty();

        // String length
        RuleFor(x => x.Notes).MaximumLength(500);

        // Range
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100);

        // Email format
        RuleFor(x => x.Email).EmailAddress();

        // Regex
        RuleFor(x => x.Phone).Matches(@"^\d{10}$");

        // Conditional
        RuleFor(x => x.ShippingAddress)
            .NotEmpty()
            .When(x => x.RequiresShipping);

        // Collection
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item required");
        RuleForEach(x => x.Items).SetValidator(new OrderItemValidator());
    }
}
```

### Async Validation (Database Check)

```csharp
public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    private readonly IReadRepository<Customer, IApplicationDbContext> _repo;

    public CreateCustomerCommandValidator(
        IReadRepository<Customer, IApplicationDbContext> repo)
    {
        _repo = repo;

        RuleFor(x => x.Email)
            .MustAsync(BeUniqueEmail)
            .WithMessage("Email already exists");
    }

    private async Task<bool> BeUniqueEmail(string email, CancellationToken ct)
    {
        var existing = await _repo.FirstOrDefaultAsync(
            new CustomerByEmailSpec(email), ct);
        return existing is null;
    }
}
```

---

## Result Pattern

### Handler Returns

```csharp
// Success with value
return Result<CustomerDto>.Success(dto);

// Success without value
return Result.Success();

// Failures
return Result<CustomerDto>.Failure(Error.NotFound("Customer.NotFound", "Customer not found"));
return Result<CustomerDto>.Failure(Error.Validation("Customer.Invalid", "Invalid data"));
return Result<CustomerDto>.Failure(Error.Conflict("Customer.Duplicate", "Already exists"));
return Result<CustomerDto>.Failure(Error.Unauthorized("Auth.Required", "Not authenticated"));
return Result<CustomerDto>.Failure(Error.Forbidden("Auth.Forbidden", "Not authorized"));
```

### Controller Handling

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetById(int id)
{
    var result = await Mediator.Send(new GetCustomerByIdQuery(id));
    return HandleResult(result);
}

[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command)
{
    var result = await Mediator.Send(command);
    return HandleCreatedResult(result, nameof(GetById), new { id = result.Value?.Id });
}
```

---

## Caching

### Get or Set Pattern

```csharp
var dto = await _cache.GetOrSetAsync(
    $"customer:{id}",
    async ct => await FetchFromDatabase(id, ct),
    slidingExpiration: TimeSpan.FromMinutes(10),
    absoluteExpiration: TimeSpan.FromHours(1),
    ct);
```

### Manual Cache Operations

```csharp
// Get
var cached = await _cache.GetAsync<CustomerDto>($"customer:{id}", ct);

// Set
await _cache.SetAsync($"customer:{id}", dto, TimeSpan.FromMinutes(10), ct);

// Remove
await _cache.RemoveAsync($"customer:{id}", ct);

// Remove by pattern
await _cache.RemoveByPatternAsync("customer:*", ct);
```

---

## Domain Events

### Define Event

```csharp
public record CustomerCreatedEvent(int CustomerId, string Email) : DomainEvent;
```

### Raise Event

```csharp
public static Customer Create(string name, Email email)
{
    var customer = new Customer { Name = name, Email = email };
    customer.AddDomainEvent(new CustomerCreatedEvent(customer.Id, email.Value));
    return customer;
}
```

### Handle Event

```csharp
public class CustomerCreatedEventHandler : INotificationHandler<CustomerCreatedEvent>
{
    private readonly IEmailService _email;

    public async Task Handle(CustomerCreatedEvent notification, CancellationToken ct)
    {
        await _email.SendAsync(notification.Email, "Welcome!", "Thanks for joining!");
    }
}
```

---

## Service Lifetime Registration

```csharp
// Auto-registered as Transient
public interface IIdGenerator : ITransientService { }

// Auto-registered as Scoped
public interface ICurrentUserService : IScopedService { }

// Auto-registered as Singleton
public interface IConfigService : ISingletonService { }
```

---

## Controller Base

```csharp
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ApiControllerBase
{
    public CustomersController(IMediator mediator) : base(mediator) { }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetCustomersQuery query)
        => HandleResult(await Mediator.Send(query));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
        => HandleResult(await Mediator.Send(new GetCustomerByIdQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command)
        => HandleCreatedResult(
            await Mediator.Send(command),
            nameof(GetById),
            new { id = (await Mediator.Send(command)).Value?.Id });

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerCommand command)
        => HandleResult(await Mediator.Send(command with { Id = id }));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => HandleResult(await Mediator.Send(new DeleteCustomerCommand(id)));
}
```

---

## Quick Commands

```bash
# Run with Aspire Dashboard
cd src/Aspire/PMS.AppHost && dotnet run

# Run WebApi directly
cd src/Presentation/PMS.WebApi && dotnet run

# Run tests
dotnet test

# Run architecture tests only
dotnet test tests/PMS.ArchitectureTests

# Add EF migration
cd src/Infrastructure/PMS.Persistence
dotnet ef migrations add MigrationName --startup-project ../../Presentation/PMS.WebApi

# Apply migrations
dotnet ef database update --startup-project ../../Presentation/PMS.WebApi

# Run benchmarks
cd tests/PMS.PerformanceTests
dotnet run -c Release
```

---

## File Locations Quick Reference

| What | Where |
|------|-------|
| Base classes | `SharedKernel/Base/` |
| Interfaces | `SharedKernel/Interfaces/` |
| Behaviors | `SharedKernel/Behaviors/` |
| Domain entities | `Domain/Entities/` |
| Value objects | `Domain/ValueObjects/` |
| Domain events | `Domain/Events/` |
| Commands/Queries | `Application/Features/{Module}/Commands/` or `Queries/` |
| DTOs | `Application/Features/{Module}/DTOs/` |
| Specifications | `Application/Features/{Module}/Specifications/` |
| EF Configurations | `Persistence/Configurations/` |
| Services | `Infrastructure/Services/` |
| Controllers | `WebApi/Controllers/` |
