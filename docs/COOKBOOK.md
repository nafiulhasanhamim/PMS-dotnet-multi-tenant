# PMS Cookbook

Practical recipes for common scenarios and use cases.

---

## Table of Contents

1. [Adding a New Domain Entity](#recipe-1-adding-a-new-domain-entity)
2. [CRUD Operations](#recipe-2-crud-operations)
3. [Pagination and Filtering](#recipe-3-pagination-and-filtering)
4. [Domain Events and Side Effects](#recipe-4-domain-events-and-side-effects)
5. [Caching Strategy](#recipe-5-caching-strategy)
6. [External API Integration](#recipe-6-external-api-integration)
7. [File Upload/Download](#recipe-7-file-uploaddownload)
8. [Background Jobs](#recipe-8-background-jobs)
9. [Multi-Tenant Support](#recipe-9-multi-tenant-support)
10. [Soft Delete and Restore](#recipe-10-soft-delete-and-restore)
11. [Audit Trail](#recipe-11-audit-trail)
12. [Complex Validation](#recipe-12-complex-validation)
13. [Optimistic Concurrency](#recipe-13-optimistic-concurrency)
14. [Bulk Operations](#recipe-14-bulk-operations)

---

## Recipe 1: Adding a New Domain Entity

**Goal**: Add a new `Invoice` entity with proper Clean Architecture structure.

### Step 1: Create Domain Entity

```csharp
// Domain/Entities/Invoice.cs
public class Invoice : AggregateRoot<int>, IAuditable, ISoftDelete
{
    public string InvoiceNumber { get; private set; } = string.Empty;
    public int CustomerId { get; private set; }
    public Money TotalAmount { get; private set; } = null!;
    public InvoiceStatus Status { get; private set; }
    public DateTime DueDate { get; private set; }

    private readonly List<InvoiceLineItem> _lineItems = new();
    public IReadOnlyList<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    // IAuditable
    public DateTime CreatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public string? ModifiedBy { get; set; }

    // ISoftDelete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public string? DeletedBy { get; set; }

    private Invoice() { }

    public static Invoice Create(int customerId, DateTime dueDate, string currency = "USD")
    {
        Guard.Against.NegativeOrZero(customerId);
        Guard.Against.Default(dueDate);

        var invoice = new Invoice
        {
            InvoiceNumber = GenerateInvoiceNumber(),
            CustomerId = customerId,
            TotalAmount = Money.Zero(currency),
            Status = InvoiceStatus.Draft,
            DueDate = dueDate
        };

        invoice.AddDomainEvent(new InvoiceCreatedEvent(invoice.Id, customerId));
        return invoice;
    }

    public void AddLineItem(string description, decimal unitPrice, int quantity)
    {
        Guard.Against.NullOrWhiteSpace(description);
        Guard.Against.NegativeOrZero(unitPrice);
        Guard.Against.NegativeOrZero(quantity);

        if (Status != InvoiceStatus.Draft)
            throw new BusinessRuleViolationException("Can only add items to draft invoices");

        var lineItem = new InvoiceLineItem(Id, description, unitPrice, quantity);
        _lineItems.Add(lineItem);

        RecalculateTotal();
    }

    public void Submit()
    {
        if (Status != InvoiceStatus.Draft)
            throw new BusinessRuleViolationException("Only draft invoices can be submitted");

        if (!_lineItems.Any())
            throw new BusinessRuleViolationException("Invoice must have at least one line item");

        Status = InvoiceStatus.Submitted;
        AddDomainEvent(new InvoiceSubmittedEvent(Id, TotalAmount.Amount));
    }

    public void MarkAsPaid()
    {
        if (Status != InvoiceStatus.Submitted)
            throw new BusinessRuleViolationException("Only submitted invoices can be marked as paid");

        Status = InvoiceStatus.Paid;
        AddDomainEvent(new InvoicePaidEvent(Id, CustomerId, TotalAmount.Amount));
    }

    private void RecalculateTotal()
    {
        var total = _lineItems.Sum(li => li.UnitPrice * li.Quantity);
        TotalAmount = Money.Create(total, TotalAmount.Currency);
    }

    private static string GenerateInvoiceNumber() =>
        $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
}
```

### Step 2: Create EF Configuration

```csharp
// Persistence/Configurations/InvoiceConfiguration.cs
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        // Value Object (owned type)
        builder.OwnsOne(i => i.TotalAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalAmount")
                .HasPrecision(18, 2);
            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3);
        });

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Navigation
        builder.HasMany(i => i.LineItems)
            .WithOne()
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft delete filter
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
```

### Step 3: Create DTO

```csharp
// Application/Features/Invoices/DTOs/InvoiceDto.cs
public class InvoiceDto : IMapFrom<Invoice>
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public List<InvoiceLineItemDto> LineItems { get; set; } = new();

    public void Mapping(TypeAdapterConfig config)
    {
        config.NewConfig<Invoice, InvoiceDto>()
            .Map(d => d.TotalAmount, s => s.TotalAmount.Amount)
            .Map(d => d.Currency, s => s.TotalAmount.Currency)
            .Map(d => d.Status, s => s.Status.ToString());
    }
}
```

---

## Recipe 2: CRUD Operations

**Goal**: Complete CRUD for an entity.

### Create Command

```csharp
// Commands/CreateInvoice/CreateInvoiceCommand.cs
public record CreateInvoiceCommand(
    int CustomerId,
    DateTime DueDate,
    List<CreateInvoiceLineItemDto> LineItems
) : IRequest<Result<InvoiceDto>>;

// CreateInvoiceCommand.Validator.cs
public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.DueDate).GreaterThan(DateTime.Today);
        RuleFor(x => x.LineItems).NotEmpty();
        RuleForEach(x => x.LineItems).SetValidator(new LineItemValidator());
    }
}

// CreateInvoiceCommandHandler.cs
public class CreateInvoiceCommandHandler
    : IRequestHandler<CreateInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IRepository<Invoice, IApplicationDbContext> _repo;
    private readonly IUnitOfWork<IApplicationDbContext> _uow;
    private readonly IMapper _mapper;

    public async Task<Result<InvoiceDto>> Handle(
        CreateInvoiceCommand request, CancellationToken ct)
    {
        var invoice = Invoice.Create(request.CustomerId, request.DueDate);

        foreach (var item in request.LineItems)
        {
            invoice.AddLineItem(item.Description, item.UnitPrice, item.Quantity);
        }

        await _repo.AddAsync(invoice, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<InvoiceDto>.Success(_mapper.Map<InvoiceDto>(invoice));
    }
}
```

### Read Query

```csharp
// Queries/GetInvoiceById/GetInvoiceByIdQuery.cs
public record GetInvoiceByIdQuery(int Id) : IRequest<Result<InvoiceDto>>;

// GetInvoiceByIdQueryHandler.cs
public class GetInvoiceByIdQueryHandler
    : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IReadRepository<Invoice, IApplicationDbContext> _repo;
    private readonly IMapper _mapper;

    public async Task<Result<InvoiceDto>> Handle(
        GetInvoiceByIdQuery request, CancellationToken ct)
    {
        var invoice = await _repo.FirstOrDefaultAsync(
            new InvoiceByIdSpec(request.Id), ct);

        return invoice is null
            ? Result<InvoiceDto>.Failure(Error.NotFound("Invoice.NotFound", "Invoice not found"))
            : Result<InvoiceDto>.Success(_mapper.Map<InvoiceDto>(invoice));
    }
}

// Specification with includes
public class InvoiceByIdSpec : Specification<Invoice>, ISingleResultSpecification<Invoice>
{
    public InvoiceByIdSpec(int id)
    {
        Query.Where(i => i.Id == id)
             .Include(i => i.LineItems);
    }
}
```

### Update Command

```csharp
// Commands/UpdateInvoice/UpdateInvoiceCommand.cs
public record UpdateInvoiceCommand(
    int Id,
    DateTime DueDate
) : IRequest<Result>;

// UpdateInvoiceCommandHandler.cs
public class UpdateInvoiceCommandHandler
    : IRequestHandler<UpdateInvoiceCommand, Result>
{
    private readonly IRepository<Invoice, IApplicationDbContext> _repo;
    private readonly IUnitOfWork<IApplicationDbContext> _uow;

    public async Task<Result> Handle(
        UpdateInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _repo.GetByIdAsync(request.Id, ct);

        if (invoice is null)
            return Result.Failure(Error.NotFound("Invoice.NotFound", "Invoice not found"));

        invoice.UpdateDueDate(request.DueDate);

        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

### Delete Command

```csharp
// Commands/DeleteInvoice/DeleteInvoiceCommand.cs
public record DeleteInvoiceCommand(int Id) : IRequest<Result>;

// DeleteInvoiceCommandHandler.cs
public class DeleteInvoiceCommandHandler
    : IRequestHandler<DeleteInvoiceCommand, Result>
{
    private readonly IRepository<Invoice, IApplicationDbContext> _repo;
    private readonly IUnitOfWork<IApplicationDbContext> _uow;

    public async Task<Result> Handle(
        DeleteInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _repo.GetByIdAsync(request.Id, ct);

        if (invoice is null)
            return Result.Failure(Error.NotFound("Invoice.NotFound", "Invoice not found"));

        // Soft delete (via ISoftDelete interface)
        await _repo.DeleteAsync(invoice, ct);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

---

## Recipe 3: Pagination and Filtering

**Goal**: Implement server-side pagination with filtering and sorting.

### Query with Pagination

```csharp
// GetInvoicesQuery.cs
public record GetInvoicesQuery(
    int? CustomerId,
    InvoiceStatus? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    string? SortBy,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 10
) : IRequest<Result<PagedList<InvoiceDto>>>;
```

### Flexible Specification

```csharp
// InvoicesListSpec.cs
public class InvoicesListSpec : Specification<Invoice>
{
    public InvoicesListSpec(
        int? customerId,
        InvoiceStatus? status,
        DateTime? fromDate,
        DateTime? toDate,
        string? sortBy,
        bool sortDescending,
        int page,
        int pageSize)
    {
        // Filters
        if (customerId.HasValue)
            Query.Where(i => i.CustomerId == customerId.Value);

        if (status.HasValue)
            Query.Where(i => i.Status == status.Value);

        if (fromDate.HasValue)
            Query.Where(i => i.CreatedOnUtc >= fromDate.Value);

        if (toDate.HasValue)
            Query.Where(i => i.CreatedOnUtc <= toDate.Value);

        // Dynamic sorting
        Query.ApplyOrdering(sortBy, sortDescending, new Dictionary<string, Expression<Func<Invoice, object>>>
        {
            ["invoiceNumber"] = i => i.InvoiceNumber,
            ["dueDate"] = i => i.DueDate,
            ["totalAmount"] = i => i.TotalAmount.Amount,
            ["createdDate"] = i => i.CreatedOnUtc
        }, defaultSort: i => i.CreatedOnUtc, defaultDescending: true);

        // Pagination
        Query.Skip((page - 1) * pageSize).Take(pageSize);

        // Include related data
        Query.Include(i => i.LineItems);
    }
}

// Extension for dynamic ordering
public static class SpecificationExtensions
{
    public static ISpecificationBuilder<T> ApplyOrdering<T>(
        this ISpecificationBuilder<T> query,
        string? sortBy,
        bool sortDescending,
        Dictionary<string, Expression<Func<T, object>>> sortMappings,
        Expression<Func<T, object>> defaultSort,
        bool defaultDescending = false) where T : class
    {
        var sortKey = sortBy?.ToLowerInvariant();
        var sortExpression = sortMappings.GetValueOrDefault(sortKey ?? "", defaultSort);
        var descending = string.IsNullOrEmpty(sortBy) ? defaultDescending : sortDescending;

        if (descending)
            query.OrderByDescending(sortExpression);
        else
            query.OrderBy(sortExpression);

        return query;
    }
}
```

### Handler with Count

```csharp
// GetInvoicesQueryHandler.cs
public class GetInvoicesQueryHandler
    : IRequestHandler<GetInvoicesQuery, Result<PagedList<InvoiceDto>>>
{
    private readonly IReadRepository<Invoice, IApplicationDbContext> _repo;
    private readonly IMapper _mapper;

    public async Task<Result<PagedList<InvoiceDto>>> Handle(
        GetInvoicesQuery request, CancellationToken ct)
    {
        var spec = new InvoicesListSpec(
            request.CustomerId,
            request.Status,
            request.FromDate,
            request.ToDate,
            request.SortBy,
            request.SortDescending,
            request.Page,
            request.PageSize);

        var invoices = await _repo.ListAsync(spec, ct);

        // Count without pagination
        var countSpec = new InvoicesListSpec(
            request.CustomerId,
            request.Status,
            request.FromDate,
            request.ToDate,
            null, false, 1, int.MaxValue);

        var totalCount = await _repo.CountAsync(countSpec, ct);

        var dtos = _mapper.Map<List<InvoiceDto>>(invoices);

        return Result<PagedList<InvoiceDto>>.Success(
            new PagedList<InvoiceDto>(dtos, totalCount, request.Page, request.PageSize));
    }
}
```

---

## Recipe 4: Domain Events and Side Effects

**Goal**: Trigger side effects (email, notifications) via domain events.

### Define Events

```csharp
// Domain/Events/InvoiceEvents.cs
public record InvoicePaidEvent(int InvoiceId, int CustomerId, decimal Amount) : DomainEvent;
public record InvoiceOverdueEvent(int InvoiceId, int CustomerId, decimal Amount) : DomainEvent;
```

### Event Handlers

```csharp
// Application/Features/Invoices/EventHandlers/

// Send payment confirmation email
public class InvoicePaidEventHandler : INotificationHandler<InvoicePaidEvent>
{
    private readonly IEmailService _email;
    private readonly IReadRepository<Customer, IApplicationDbContext> _customerRepo;
    private readonly ILogger<InvoicePaidEventHandler> _logger;

    public async Task Handle(InvoicePaidEvent notification, CancellationToken ct)
    {
        _logger.LogInformation("Invoice {InvoiceId} paid", notification.InvoiceId);

        var customer = await _customerRepo.GetByIdAsync(notification.CustomerId, ct);
        if (customer is null) return;

        await _email.SendAsync(
            customer.Email.Value,
            "Payment Received",
            $"Thank you! We received your payment of ${notification.Amount:F2}.");
    }
}

// Record payment in accounting system
public class InvoicePaidAccountingHandler : INotificationHandler<InvoicePaidEvent>
{
    private readonly IAccountingService _accounting;

    public async Task Handle(InvoicePaidEvent notification, CancellationToken ct)
    {
        await _accounting.RecordPaymentAsync(new PaymentRecord
        {
            InvoiceId = notification.InvoiceId,
            Amount = notification.Amount,
            ReceivedAt = DateTime.UtcNow
        });
    }
}
```

### Raise Events in Entity

```csharp
// In Invoice entity
public void MarkAsPaid()
{
    if (Status != InvoiceStatus.Submitted)
        throw new BusinessRuleViolationException("Only submitted invoices can be marked as paid");

    Status = InvoiceStatus.Paid;

    // Event will be dispatched by DomainEventDispatcherInterceptor
    AddDomainEvent(new InvoicePaidEvent(Id, CustomerId, TotalAmount.Amount));
}
```

---

## Recipe 5: Caching Strategy

**Goal**: Implement intelligent caching with invalidation.

### Cache Keys Service

```csharp
// Infrastructure/Services/CacheKeyService.cs
public interface ICacheKeyService : ISingletonService
{
    string Invoice(int id);
    string InvoicesByCustomer(int customerId);
    string CustomerInvoiceStats(int customerId);
    string Pattern(string prefix);
}

public class CacheKeyService : ICacheKeyService
{
    private const string Prefix = "cleanarch";

    public string Invoice(int id) => $"{Prefix}:invoice:{id}";
    public string InvoicesByCustomer(int customerId) => $"{Prefix}:invoices:customer:{customerId}";
    public string CustomerInvoiceStats(int customerId) => $"{Prefix}:stats:customer:{customerId}";
    public string Pattern(string prefix) => $"{Prefix}:{prefix}:*";
}
```

### Cached Query Handler

```csharp
public class GetInvoiceByIdQueryHandler
    : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IReadRepository<Invoice, IApplicationDbContext> _repo;
    private readonly ICacheService _cache;
    private readonly ICacheKeyService _cacheKey;
    private readonly IMapper _mapper;

    public async Task<Result<InvoiceDto>> Handle(
        GetInvoiceByIdQuery request, CancellationToken ct)
    {
        var key = _cacheKey.Invoice(request.Id);

        var dto = await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var invoice = await _repo.FirstOrDefaultAsync(
                    new InvoiceByIdSpec(request.Id), ct);
                return invoice is null ? null : _mapper.Map<InvoiceDto>(invoice);
            },
            slidingExpiration: TimeSpan.FromMinutes(15),
            absoluteExpiration: TimeSpan.FromHours(2),
            ct);

        return dto is null
            ? Result<InvoiceDto>.Failure(Error.NotFound("Invoice.NotFound", "Not found"))
            : Result<InvoiceDto>.Success(dto);
    }
}
```

### Cache Invalidation on Update

```csharp
public class UpdateInvoiceCommandHandler
    : IRequestHandler<UpdateInvoiceCommand, Result>
{
    private readonly IRepository<Invoice, IApplicationDbContext> _repo;
    private readonly IUnitOfWork<IApplicationDbContext> _uow;
    private readonly ICacheService _cache;
    private readonly ICacheKeyService _cacheKey;

    public async Task<Result> Handle(
        UpdateInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _repo.GetByIdAsync(request.Id, ct);
        if (invoice is null)
            return Result.Failure(Error.NotFound("Invoice.NotFound", "Not found"));

        invoice.UpdateDueDate(request.DueDate);
        await _uow.SaveChangesAsync(ct);

        // Invalidate caches
        await _cache.RemoveAsync(_cacheKey.Invoice(request.Id), ct);
        await _cache.RemoveByPatternAsync(_cacheKey.Pattern("invoices"), ct);
        await _cache.RemoveAsync(_cacheKey.CustomerInvoiceStats(invoice.CustomerId), ct);

        return Result.Success();
    }
}
```

---

## Recipe 6: External API Integration

**Goal**: Call external APIs with resilience patterns.

### Define Interface

```csharp
// Application/Common/Interfaces/IPaymentGateway.cs
public interface IPaymentGateway : IScopedService
{
    Task<Result<PaymentResult>> ProcessPaymentAsync(
        string cardToken,
        decimal amount,
        string currency,
        CancellationToken ct = default);

    Task<Result<RefundResult>> RefundPaymentAsync(
        string chargeId,
        decimal amount,
        CancellationToken ct = default);
}
```

### Implementation with HTTP Client

```csharp
// Infrastructure/Services/StripePaymentGateway.cs
public class StripePaymentGateway : IPaymentGateway
{
    private readonly IHttpClientService _http;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(IHttpClientService http, ILogger<StripePaymentGateway> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<Result<PaymentResult>> ProcessPaymentAsync(
        string cardToken,
        decimal amount,
        string currency,
        CancellationToken ct = default)
    {
        var request = new
        {
            source = cardToken,
            amount = (int)(amount * 100), // Stripe uses cents
            currency = currency.ToLower()
        };

        var response = await _http.PostAsync<object, StripeChargeResponse>(
            "/v1/charges",
            request,
            HttpCallOptions.ForClient("Stripe")
                .WithTimeout(30)
                .WithRetry(2));

        if (!response.IsSuccess)
        {
            _logger.LogWarning("Payment failed: {Error}", response.Error);
            return Result<PaymentResult>.Failure(
                Error.Validation("Payment.Failed", response.Error ?? "Payment processing failed"));
        }

        return Result<PaymentResult>.Success(new PaymentResult
        {
            ChargeId = response.Data!.Id,
            Status = response.Data.Status
        });
    }
}
```

### Register HTTP Client

```csharp
// Infrastructure/DependencyInjection.cs
services.AddResilientHttpClient("Stripe", options =>
{
    options.BaseAddress = new Uri(configuration["Stripe:BaseUrl"]!);
    options.DefaultHeaders.Add("Authorization", $"Bearer {configuration["Stripe:SecretKey"]}");
});
```

### Usage in Handler

```csharp
public class ProcessInvoicePaymentCommandHandler
    : IRequestHandler<ProcessInvoicePaymentCommand, Result<PaymentDto>>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IRepository<Invoice, IApplicationDbContext> _invoiceRepo;
    private readonly IUnitOfWork<IApplicationDbContext> _uow;

    public async Task<Result<PaymentDto>> Handle(
        ProcessInvoicePaymentCommand request, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(request.InvoiceId, ct);
        if (invoice is null)
            return Result<PaymentDto>.Failure(Error.NotFound("Invoice.NotFound", "Not found"));

        // Process payment via external gateway
        var paymentResult = await _paymentGateway.ProcessPaymentAsync(
            request.CardToken,
            invoice.TotalAmount.Amount,
            invoice.TotalAmount.Currency,
            ct);

        if (!paymentResult.IsSuccess)
            return Result<PaymentDto>.Failure(paymentResult.Error);

        // Update invoice
        invoice.MarkAsPaid();
        await _uow.SaveChangesAsync(ct);

        return Result<PaymentDto>.Success(new PaymentDto
        {
            InvoiceId = invoice.Id,
            ChargeId = paymentResult.Value.ChargeId,
            Status = "Paid"
        });
    }
}
```

---

## Recipe 7: File Upload/Download

**Goal**: Handle file uploads with storage abstraction.

### Storage Interface

```csharp
// Application/Common/Interfaces/IFileStorageService.cs
public interface IFileStorageService : IScopedService
{
    Task<Result<string>> UploadAsync(
        Stream fileStream,
        string fileName,
        string folder,
        CancellationToken ct = default);

    Task<Result<Stream>> DownloadAsync(
        string filePath,
        CancellationToken ct = default);

    Task<Result> DeleteAsync(
        string filePath,
        CancellationToken ct = default);
}
```

### Upload Command

```csharp
// Commands/UploadInvoiceAttachment/
public record UploadInvoiceAttachmentCommand(
    int InvoiceId,
    Stream FileStream,
    string FileName,
    string ContentType
) : IRequest<Result<AttachmentDto>>;

public class UploadInvoiceAttachmentCommandHandler
    : IRequestHandler<UploadInvoiceAttachmentCommand, Result<AttachmentDto>>
{
    private readonly IRepository<Invoice, IApplicationDbContext> _invoiceRepo;
    private readonly IRepository<Attachment, IApplicationDbContext> _attachmentRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork<IApplicationDbContext> _uow;

    public async Task<Result<AttachmentDto>> Handle(
        UploadInvoiceAttachmentCommand request, CancellationToken ct)
    {
        var invoice = await _invoiceRepo.GetByIdAsync(request.InvoiceId, ct);
        if (invoice is null)
            return Result<AttachmentDto>.Failure(Error.NotFound("Invoice.NotFound", "Not found"));

        // Upload to storage
        var uploadResult = await _fileStorage.UploadAsync(
            request.FileStream,
            request.FileName,
            $"invoices/{request.InvoiceId}",
            ct);

        if (!uploadResult.IsSuccess)
            return Result<AttachmentDto>.Failure(uploadResult.Error);

        // Create attachment record
        var attachment = Attachment.Create(
            request.InvoiceId,
            request.FileName,
            uploadResult.Value,
            request.ContentType);

        await _attachmentRepo.AddAsync(attachment, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<AttachmentDto>.Success(new AttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            FilePath = attachment.FilePath
        });
    }
}
```

### Controller with File Upload

```csharp
[HttpPost("{id}/attachments")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> UploadAttachment(
    int id,
    [FromForm] IFormFile file)
{
    if (file.Length == 0)
        return BadRequest("File is empty");

    using var stream = file.OpenReadStream();

    var command = new UploadInvoiceAttachmentCommand(
        id,
        stream,
        file.FileName,
        file.ContentType);

    var result = await Mediator.Send(command);
    return HandleResult(result);
}
```

---

## Recipe 8: Background Jobs

**Goal**: Execute long-running tasks in background.

### Using Channels for Queue

```csharp
// Application/Common/Interfaces/IBackgroundJobQueue.cs
public interface IBackgroundJobQueue
{
    ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem);
    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct);
}

// Infrastructure/Services/BackgroundJobQueue.cs
public class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;

    public BackgroundJobQueue()
    {
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(100);
    }

    public async ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(
        CancellationToken ct)
    {
        return await _queue.Reader.ReadAsync(ct);
    }
}

// BackgroundJobProcessor.cs (Hosted Service)
public class BackgroundJobProcessor : BackgroundService
{
    private readonly IBackgroundJobQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundJobProcessor> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _queue.DequeueAsync(stoppingToken);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job failed");
            }
        }
    }
}
```

### Queue Job from Handler

```csharp
public class GenerateInvoiceReportCommandHandler
    : IRequestHandler<GenerateInvoiceReportCommand, Result<Guid>>
{
    private readonly IBackgroundJobQueue _jobQueue;

    public async Task<Result<Guid>> Handle(
        GenerateInvoiceReportCommand request, CancellationToken ct)
    {
        var jobId = Guid.NewGuid();

        await _jobQueue.QueueAsync(async (sp, ct) =>
        {
            var repo = sp.GetRequiredService<IReadRepository<Invoice, IApplicationDbContext>>();
            var reportService = sp.GetRequiredService<IReportService>();

            var invoices = await repo.ListAsync(
                new InvoicesForReportSpec(request.CustomerId, request.Year), ct);

            await reportService.GenerateAndSaveAsync(jobId, invoices, ct);
        });

        return Result<Guid>.Success(jobId);
    }
}
```

---

## Recipe 9: Multi-Tenant Support

**Goal**: Support multiple tenants with data isolation.

### Tenant Context

```csharp
// Application/Common/Interfaces/ITenantContext.cs
public interface ITenantContext : IScopedService
{
    int TenantId { get; }
    string TenantName { get; }
}

// Infrastructure/Services/TenantContext.cs
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public int TenantId =>
        int.Parse(_httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value ?? "0");

    public string TenantName =>
        _httpContextAccessor.HttpContext?.User.FindFirst("tenant_name")?.Value ?? "";
}
```

### Multi-Tenant Entity

```csharp
// Domain/Common/ITenantEntity.cs
public interface ITenantEntity
{
    int TenantId { get; }
}

// Domain/Entities/Invoice.cs
public class Invoice : AggregateRoot<int>, IAuditable, ISoftDelete, ITenantEntity
{
    public int TenantId { get; private set; }
    // ... other properties

    public static Invoice Create(int tenantId, int customerId, DateTime dueDate)
    {
        return new Invoice
        {
            TenantId = tenantId,
            CustomerId = customerId,
            DueDate = dueDate
        };
    }
}
```

### Global Query Filter

```csharp
// Persistence/Configurations/InvoiceConfiguration.cs
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    private readonly ITenantContext _tenantContext;

    public InvoiceConfiguration(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        // ... other config

        // Tenant filter
        builder.HasQueryFilter(i =>
            !i.IsDeleted && i.TenantId == _tenantContext.TenantId);
    }
}
```

### Handler with Tenant

```csharp
public class CreateInvoiceCommandHandler
    : IRequestHandler<CreateInvoiceCommand, Result<InvoiceDto>>
{
    private readonly ITenantContext _tenant;

    public async Task<Result<InvoiceDto>> Handle(
        CreateInvoiceCommand request, CancellationToken ct)
    {
        var invoice = Invoice.Create(
            _tenant.TenantId,  // Automatically set tenant
            request.CustomerId,
            request.DueDate);

        // ...
    }
}
```

---

## Recipe 10: Soft Delete and Restore

**Goal**: Implement soft delete with ability to restore.

### Soft Delete Implementation

```csharp
// SharedKernel/Interfaces/ISoftDelete.cs
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedOnUtc { get; set; }
    string? DeletedBy { get; set; }
}

// Persistence/Repositories/Repository.cs
public async Task DeleteAsync(TEntity entity, CancellationToken ct = default)
{
    if (entity is ISoftDelete softDelete)
    {
        softDelete.IsDeleted = true;
        softDelete.DeletedOnUtc = DateTime.UtcNow;
        softDelete.DeletedBy = _currentUser.UserId;
        _context.Set<TEntity>().Update(entity);
    }
    else
    {
        _context.Set<TEntity>().Remove(entity);
    }
}
```

### Restore Command

```csharp
// Commands/RestoreInvoice/
public record RestoreInvoiceCommand(int Id) : IRequest<Result>;

public class RestoreInvoiceCommandHandler
    : IRequestHandler<RestoreInvoiceCommand, Result>
{
    private readonly ApplicationDbContext _context;
    private readonly IUnitOfWork<IApplicationDbContext> _uow;

    public async Task<Result> Handle(
        RestoreInvoiceCommand request, CancellationToken ct)
    {
        // Query without soft delete filter
        var invoice = await _context.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.Id == request.Id && i.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (invoice is null)
            return Result.Failure(Error.NotFound("Invoice.NotFound", "Deleted invoice not found"));

        invoice.IsDeleted = false;
        invoice.DeletedOnUtc = null;
        invoice.DeletedBy = null;

        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

### Query Deleted Items

```csharp
// Queries/GetDeletedInvoices/
public class GetDeletedInvoicesQueryHandler
    : IRequestHandler<GetDeletedInvoicesQuery, Result<List<InvoiceDto>>>
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public async Task<Result<List<InvoiceDto>>> Handle(
        GetDeletedInvoicesQuery request, CancellationToken ct)
    {
        var deleted = await _context.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.IsDeleted)
            .OrderByDescending(i => i.DeletedOnUtc)
            .ToListAsync(ct);

        return Result<List<InvoiceDto>>.Success(
            _mapper.Map<List<InvoiceDto>>(deleted));
    }
}
```

---

## Recipe 11: Audit Trail

**Goal**: Track all changes with full audit history.

### Audit Trail Interceptor

```csharp
// Persistence/Interceptors/AuditableEntityInterceptor.cs
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _dateTime;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null) return base.SavingChangesAsync(eventData, result, ct);

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOnUtc = _dateTime.UtcNow;
                    entry.Entity.CreatedBy = _currentUser.UserId;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedOnUtc = _dateTime.UtcNow;
                    entry.Entity.ModifiedBy = _currentUser.UserId;
                    break;
            }
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}
```

### Detailed Audit Log Entity

```csharp
// Domain/Entities/AuditLog.cs
public class AuditLog : BaseEntity<long>
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? UserId { get; set; }
    public DateTime Timestamp { get; set; }
}

// Enhanced Interceptor
public override ValueTask<InterceptionResult<int>> SavingChangesAsync(...)
{
    var auditEntries = new List<AuditLog>();

    foreach (var entry in context.ChangeTracker.Entries())
    {
        if (entry.Entity is AuditLog) continue;

        var audit = new AuditLog
        {
            EntityType = entry.Entity.GetType().Name,
            EntityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
            Action = entry.State.ToString(),
            UserId = _currentUser.UserId,
            Timestamp = _dateTime.UtcNow
        };

        if (entry.State == EntityState.Modified)
        {
            audit.OldValues = JsonSerializer.Serialize(
                entry.Properties.Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
            audit.NewValues = JsonSerializer.Serialize(
                entry.Properties.Where(p => p.IsModified)
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
        }

        auditEntries.Add(audit);
    }

    context.Set<AuditLog>().AddRange(auditEntries);

    return base.SavingChangesAsync(eventData, result, ct);
}
```

---

## Recipe 12: Complex Validation

**Goal**: Implement cross-field and async validation.

### Cross-Field Validation

```csharp
public class CreatePromotionCommandValidator : AbstractValidator<CreatePromotionCommand>
{
    public CreatePromotionCommandValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty()
            .LessThan(x => x.EndDate)
            .WithMessage("Start date must be before end date");

        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(1, 100)
            .When(x => x.DiscountType == DiscountType.Percentage);

        RuleFor(x => x.DiscountAmount)
            .GreaterThan(0)
            .When(x => x.DiscountType == DiscountType.FixedAmount);

        // Cross-field: at least one discount type must be set
        RuleFor(x => x)
            .Must(x => x.DiscountPercent > 0 || x.DiscountAmount > 0)
            .WithMessage("Either discount percent or amount must be specified");
    }
}
```

### Async Validation with Database

```csharp
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    private readonly IReadRepository<Product, IApplicationDbContext> _productRepo;
    private readonly IReadRepository<Customer, IApplicationDbContext> _customerRepo;

    public CreateOrderCommandValidator(
        IReadRepository<Product, IApplicationDbContext> productRepo,
        IReadRepository<Customer, IApplicationDbContext> customerRepo)
    {
        _productRepo = productRepo;
        _customerRepo = customerRepo;

        RuleFor(x => x.CustomerId)
            .MustAsync(CustomerExistsAndIsActive)
            .WithMessage("Customer does not exist or is inactive");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .MustAsync(ProductExistsAndInStock)
                .WithMessage("Product does not exist or is out of stock");
        });
    }

    private async Task<bool> CustomerExistsAndIsActive(int customerId, CancellationToken ct)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId, ct);
        return customer is not null && customer.Status == CustomerStatus.Active;
    }

    private async Task<bool> ProductExistsAndInStock(int productId, CancellationToken ct)
    {
        var product = await _productRepo.GetByIdAsync(productId, ct);
        return product is not null && product.StockQuantity > 0;
    }
}
```

---

## Recipe 13: Optimistic Concurrency

**Goal**: Handle concurrent updates safely.

### Entity with RowVersion

```csharp
// Already included in AggregateRoot<TId>
public abstract class AggregateRoot<TId> : BaseEntity<TId>
{
    public byte[] RowVersion { get; protected set; } = Array.Empty<byte>();
}
```

### EF Configuration

```csharp
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(i => i.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}
```

### Handling Concurrency in Handler

```csharp
public class UpdateInvoiceCommandHandler
    : IRequestHandler<UpdateInvoiceCommand, Result>
{
    public async Task<Result> Handle(
        UpdateInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _repo.GetByIdAsync(request.Id, ct);
        if (invoice is null)
            return Result.Failure(Error.NotFound("Invoice.NotFound", "Not found"));

        // Check row version matches
        if (!invoice.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure(Error.Conflict(
                "Invoice.ConcurrencyConflict",
                "The invoice was modified by another user. Please refresh and try again."));

        invoice.UpdateDueDate(request.DueDate);

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict(
                "Invoice.ConcurrencyConflict",
                "The invoice was modified. Please refresh and try again."));
        }

        return Result.Success();
    }
}
```

---

## Recipe 14: Bulk Operations

**Goal**: Efficiently handle bulk insert/update operations.

### Bulk Insert with Dapper

```csharp
public class BulkImportProductsCommandHandler
    : IRequestHandler<BulkImportProductsCommand, Result<int>>
{
    private readonly IDapperService _dapper;

    public async Task<Result<int>> Handle(
        BulkImportProductsCommand request, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO Products (Name, SKU, Price, CreatedOnUtc)
            VALUES (@Name, @SKU, @Price, @CreatedOnUtc)";

        var products = request.Products.Select(p => new
        {
            p.Name,
            p.SKU,
            p.Price,
            CreatedOnUtc = DateTime.UtcNow
        });

        var rowsAffected = await _dapper.ExecuteAsync(sql, products, ct);

        return Result<int>.Success(rowsAffected);
    }
}
```

### Bulk Update with Raw SQL

```csharp
public class BulkUpdatePricesCommandHandler
    : IRequestHandler<BulkUpdatePricesCommand, Result<int>>
{
    private readonly IDapperService _dapper;

    public async Task<Result<int>> Handle(
        BulkUpdatePricesCommand request, CancellationToken ct)
    {
        const string sql = @"
            UPDATE Products
            SET Price = Price * @Multiplier,
                ModifiedOnUtc = @ModifiedAt
            WHERE CategoryId = @CategoryId";

        var rowsAffected = await _dapper.ExecuteAsync(sql, new
        {
            Multiplier = request.PriceMultiplier,
            ModifiedAt = DateTime.UtcNow,
            CategoryId = request.CategoryId
        }, ct);

        return Result<int>.Success(rowsAffected);
    }
}
```

---

## Conclusion

These recipes cover the most common scenarios you'll encounter when building applications with PMS. Each recipe follows the established patterns and can be adapted to your specific needs.

For more details, see:
- [README.md](./README.md) - Full documentation
- [QUICK_REFERENCE.md](./QUICK_REFERENCE.md) - Code snippets
- [aspire-integration-guide.md](./aspire-integration-guide.md) - Observability setup
