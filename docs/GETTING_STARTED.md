# Building a Clean Architecture Project from Scratch

> **Note on the examples in this document.**
> The worked examples below use `Customer`, `Product` and `Order`. Those were the
> template's sample domain and have been removed from the codebase — PMS builds its
> own entities (Medicine, Batch, Sale, Supplier, ...) on the same patterns. Read them
> as illustrations of the pattern, not as code that exists in this repository.

A complete step-by-step guide to creating a production-ready .NET 8 Clean Architecture solution.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Phase 1: Solution Setup](#phase-1-solution-setup)
3. [Phase 2: SharedKernel Layer](#phase-2-sharedkernel-layer)
4. [Phase 3: Domain Layer](#phase-3-domain-layer)
5. [Phase 4: Application Layer](#phase-4-application-layer)
6. [Phase 5: Persistence Layer](#phase-5-persistence-layer)
7. [Phase 6: Infrastructure Layer](#phase-6-infrastructure-layer)
8. [Phase 7: WebApi Layer](#phase-7-webapi-layer)
9. [Phase 8: Aspire Integration](#phase-8-aspire-integration)
10. [Phase 9: Testing Projects](#phase-9-testing-projects)
11. [Phase 10: Final Configuration](#phase-10-final-configuration)
12. [Verification Checklist](#verification-checklist)

---

## Prerequisites

### Required Software

```bash
# .NET 8.0 SDK
dotnet --version  # Should be 8.0.x or higher

# Install Aspire workload
dotnet workload install aspire

# Verify installation
dotnet workload list
```

### Recommended Tools

- Visual Studio 2022 or VS Code with C# Dev Kit
- SQL Server (LocalDB, Express, or Developer Edition)
- Docker Desktop (optional, for Aspire resources)
- Git for version control

---

## Phase 1: Solution Setup

### Step 1.1: Create Solution Structure

```bash
# Create root folder
mkdir MyApp
cd MyApp

# Create solution file
dotnet new sln -n MyApp

# Create folder structure
mkdir -p src/Core
mkdir -p src/Infrastructure
mkdir -p src/Presentation
mkdir -p src/Aspire
mkdir -p tests
mkdir -p docs
mkdir -p tools
```

### Step 1.2: Create Directory.Build.props

Create `Directory.Build.props` in root folder for shared build settings:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>13</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningsAsErrors>nullable</WarningsAsErrors>
  </PropertyGroup>

  <PropertyGroup>
    <Company>MyCompany</Company>
    <Authors>MyTeam</Authors>
    <Copyright>Copyright (c) $(Company) $([System.DateTime]::Now.Year)</Copyright>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <Optimize>true</Optimize>
  </PropertyGroup>
</Project>
```

### Step 1.3: Create Directory.Packages.props

Create `Directory.Packages.props` for central package management:

```xml
<Project>

  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Ardalis Packages -->
    <PackageVersion Include="Ardalis.GuardClauses" Version="4.6.0" />
    <PackageVersion Include="Ardalis.Result" Version="7.2.0" />
    <PackageVersion Include="Ardalis.Specification" Version="7.0.0" />
    <PackageVersion Include="Ardalis.Specification.EntityFrameworkCore" Version="7.0.0" />

    <!-- MediatR & CQRS -->
    <PackageVersion Include="MediatR" Version="12.4.1" />

    <!-- Validation -->
    <PackageVersion Include="FluentValidation" Version="11.5.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.5.1" />

    <!-- Mapping -->
    <PackageVersion Include="Mapster" Version="7.4.0" />
    <PackageVersion Include="Mapster.DependencyInjection" Version="1.0.1" />

    <!-- Entity Framework Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.11" />

    <!-- Database -->
    <PackageVersion Include="Dapper" Version="2.1.35" />
    <PackageVersion Include="Microsoft.Data.SqlClient" Version="5.2.2" />

    <!-- Microsoft Extensions -->
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
    <PackageVersion Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="8.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />

    <!-- Logging -->
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageVersion Include="Serilog.Enrichers.Environment" Version="3.0.1" />
    <PackageVersion Include="Serilog.Formatting.Compact" Version="3.0.0" />

    <!-- API Documentation -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="6.5.0" />

    <!-- Health Checks -->
    <PackageVersion Include="AspNetCore.HealthChecks.SqlServer" Version="8.0.2" />
    <PackageVersion Include="AspNetCore.HealthChecks.UI.Client" Version="8.0.1" />

    <!-- OpenTelemetry -->
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.0.0-beta.12" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.0" />

    <!-- .NET Aspire -->
    <PackageVersion Include="Aspire.Hosting" Version="8.2.2" />
    <PackageVersion Include="Aspire.Hosting.AppHost" Version="8.2.2" />
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="8.10.0" />
    <PackageVersion Include="Microsoft.Extensions.ServiceDiscovery" Version="8.2.2" />

    <!-- Testing -->
    <PackageVersion Include="xunit" Version="2.6.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.5.5" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageVersion Include="FluentAssertions" Version="6.12.0" />
    <PackageVersion Include="Moq" Version="4.20.69" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
  </ItemGroup>

</Project>
```

### Step 1.4: Create .gitignore

```bash
dotnet new gitignore
```

---

## Phase 2: SharedKernel Layer

The foundation layer with base classes and interfaces.

### Step 2.1: Create Project

```bash
dotnet new classlib -n MyApp.SharedKernel -o src/Core/MyApp.SharedKernel
dotnet sln add src/Core/MyApp.SharedKernel/MyApp.SharedKernel.csproj
```

### Step 2.2: Update .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>MyApp.SharedKernel</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Ardalis.GuardClauses" />
    <PackageReference Include="Ardalis.Specification" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="MediatR" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>

</Project>
```

### Step 2.3: Create Base Classes

**Base/BaseEntity.cs**
```csharp
namespace MyApp.SharedKernel.Base;

public abstract class BaseEntity<TId> : IEquatable<BaseEntity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    private readonly List<DomainEvent> _domainEvents = new();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(DomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    public bool Equals(BaseEntity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as BaseEntity<TId>);
    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(BaseEntity<TId>? left, BaseEntity<TId>? right) =>
        Equals(left, right);
    public static bool operator !=(BaseEntity<TId>? left, BaseEntity<TId>? right) =>
        !Equals(left, right);
}
```

**Base/AggregateRoot.cs**
```csharp
namespace MyApp.SharedKernel.Base;

public abstract class AggregateRoot<TId> : BaseEntity<TId>
    where TId : notnull
{
    public byte[] RowVersion { get; protected set; } = Array.Empty<byte>();
}
```

**Base/ValueObject.cs**
```csharp
namespace MyApp.SharedKernel.Base;

public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(1, (current, obj) =>
                HashCode.Combine(current, obj?.GetHashCode() ?? 0));

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        Equals(left, right);
    public static bool operator !=(ValueObject? left, ValueObject? right) =>
        !Equals(left, right);
}
```

**Base/DomainEvent.cs**
```csharp
using MediatR;

namespace MyApp.SharedKernel.Base;

public abstract record DomainEvent : INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
```

### Step 2.4: Create Interfaces

**Interfaces/IDbContext.cs**
```csharp
namespace MyApp.SharedKernel.Interfaces;

public interface IDbContext { }

public interface IApplicationDbContext : IDbContext { }
```

**Interfaces/IRepository.cs**
```csharp
using Ardalis.Specification;

namespace MyApp.SharedKernel.Interfaces;

public interface IReadRepository<TEntity, TContext>
    where TEntity : class
    where TContext : IDbContext
{
    Task<TEntity?> GetByIdAsync<TId>(TId id, CancellationToken ct = default) where TId : notnull;
    Task<TEntity?> FirstOrDefaultAsync(ISpecification<TEntity> specification, CancellationToken ct = default);
    Task<List<TEntity>> ListAsync(CancellationToken ct = default);
    Task<List<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<TEntity> specification, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task<bool> AnyAsync(ISpecification<TEntity> specification, CancellationToken ct = default);
    IQueryable<TEntity> Query();
}

public interface IRepository<TEntity, TContext> : IReadRepository<TEntity, TContext>
    where TEntity : class
    where TContext : IDbContext
{
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
}
```

**Interfaces/IUnitOfWork.cs**
```csharp
namespace MyApp.SharedKernel.Interfaces;

public interface IUnitOfWork<TContext> where TContext : IDbContext
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
```

**Interfaces/IAuditable.cs**
```csharp
namespace MyApp.SharedKernel.Interfaces;

public interface IAuditable
{
    DateTime CreatedOnUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? ModifiedOnUtc { get; set; }
    string? ModifiedBy { get; set; }
}
```

**Interfaces/ISoftDelete.cs**
```csharp
namespace MyApp.SharedKernel.Interfaces;

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedOnUtc { get; set; }
    string? DeletedBy { get; set; }
}
```

**Interfaces/IDateTime.cs**
```csharp
namespace MyApp.SharedKernel.Interfaces;

public interface IDateTime
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}
```

**Interfaces/ICurrentUserService.cs**
```csharp
namespace MyApp.SharedKernel.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
}
```

### Step 2.5: Create Service Lifetime Markers

**DependencyInjection/ServiceLifetimeMarkers.cs**
```csharp
namespace MyApp.SharedKernel.DependencyInjection;

public interface ITransientService { }
public interface IScopedService { }
public interface ISingletonService { }
```

**DependencyInjection/ServiceLifetimeRegistrar.cs**
```csharp
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace MyApp.SharedKernel.DependencyInjection;

public static class ServiceLifetimeRegistrar
{
    public static IServiceCollection AddServicesFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType);

        foreach (var type in types)
        {
            var interfaces = type.GetInterfaces();

            foreach (var @interface in interfaces)
            {
                if (@interface.GetInterfaces().Any(i => i == typeof(ITransientService)))
                {
                    services.AddTransient(@interface, type);
                }
                else if (@interface.GetInterfaces().Any(i => i == typeof(IScopedService)))
                {
                    services.AddScoped(@interface, type);
                }
                else if (@interface.GetInterfaces().Any(i => i == typeof(ISingletonService)))
                {
                    services.AddSingleton(@interface, type);
                }
            }
        }

        return services;
    }
}
```

### Step 2.6: Create Result Pattern

**Results/Error.cs**
```csharp
namespace MyApp.SharedKernel.Results;

public record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string code, string description) => new(code, description);
    public static Error Validation(string code, string description) => new(code, description);
    public static Error Conflict(string code, string description) => new(code, description);
    public static Error Unauthorized(string code, string description) => new(code, description);
    public static Error Forbidden(string code, string description) => new(code, description);
    public static Error Failure(string code, string description) => new(code, description);
}
```

**Results/Result.cs**
```csharp
namespace MyApp.SharedKernel.Results;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);
    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);
}

public class Result<TValue> : Result
{
    public TValue Value { get; }

    private Result(TValue value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    public static Result<TValue> Success(TValue value) => new(value, true, Error.None);
    public new static Result<TValue> Failure(Error error) => new(default!, false, error);

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
```

### Step 2.7: Create MediatR Behaviors

**Behaviors/ValidationBehavior.cs**
```csharp
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MyApp.SharedKernel.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            _logger.LogWarning("Validation failed for {RequestType}: {@Errors}",
                typeof(TRequest).Name, failures);
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

**Behaviors/LoggingBehavior.cs**
```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MyApp.SharedKernel.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid().ToString()[..8];

        _logger.LogInformation("Handling {RequestName} [{RequestId}]", requestName, requestId);

        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        _logger.LogInformation("Handled {RequestName} [{RequestId}] in {ElapsedMs}ms",
            requestName, requestId, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
```

**Behaviors/PerformanceBehavior.cs**
```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MyApp.SharedKernel.Behaviors;

public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly Stopwatch _timer;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
        _timer = new Stopwatch();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _timer.Start();
        var response = await next();
        _timer.Stop();

        var elapsedMs = _timer.ElapsedMilliseconds;

        if (elapsedMs > 500)
        {
            _logger.LogWarning("Long Running Request: {RequestName} ({ElapsedMs}ms) {@Request}",
                typeof(TRequest).Name, elapsedMs, request);
        }

        return response;
    }
}
```

**Behaviors/UnhandledExceptionBehavior.cs**
```csharp
using MediatR;
using Microsoft.Extensions.Logging;

namespace MyApp.SharedKernel.Behaviors;

public class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehavior(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled Exception for Request {RequestName} {@Request}",
                typeof(TRequest).Name, request);
            throw;
        }
    }
}
```

### Step 2.8: Create Pagination Models

**Models/PagedList.cs**
```csharp
namespace MyApp.SharedKernel.Models;

public class PagedList<T>
{
    public List<T> Items { get; }
    public int TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PagedList(List<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }
}
```

---

## Phase 3: Domain Layer

Pure business logic with entities and value objects.

### Step 3.1: Create Project

```bash
dotnet new classlib -n MyApp.Domain -o src/Core/MyApp.Domain
dotnet sln add src/Core/MyApp.Domain/MyApp.Domain.csproj
```

### Step 3.2: Update .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>MyApp.Domain</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyApp.SharedKernel\MyApp.SharedKernel.csproj" />
  </ItemGroup>

</Project>
```

### Step 3.3: Create Value Objects

**ValueObjects/Email.cs**
```csharp
using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using MyApp.Domain.Exceptions;
using MyApp.SharedKernel.Base;

namespace MyApp.Domain.ValueObjects;

public sealed class Email : ValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string email)
    {
        Guard.Against.NullOrWhiteSpace(email, nameof(email));

        if (!EmailRegex.IsMatch(email))
            throw new DomainException($"Invalid email format: {email}");

        return new Email(email.ToLowerInvariant().Trim());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(Email email) => email.Value;
}
```

**ValueObjects/Money.cs**
```csharp
using Ardalis.GuardClauses;
using MyApp.SharedKernel.Base;

namespace MyApp.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Create(decimal amount, string currency = "USD")
    {
        Guard.Against.Negative(amount, nameof(amount));
        Guard.Against.NullOrWhiteSpace(currency, nameof(currency));

        return new Money(Math.Round(amount, 2), currency.ToUpperInvariant());
    }

    public static Money Zero(string currency = "USD") => new(0, currency.ToUpperInvariant());

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");

        return Create(Amount + other.Amount, Currency);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Currency} {Amount:F2}";
}
```

### Step 3.4: Create Enums

**Enums/CustomerStatus.cs**
```csharp
namespace MyApp.Domain.Enums;

public enum CustomerStatus
{
    Active = 1,
    Suspended = 2,
    Inactive = 3
}
```

**Enums/OrderStatus.cs**
```csharp
namespace MyApp.Domain.Enums;

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Processing = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6
}
```

### Step 3.5: Create Exceptions

**Exceptions/DomainException.cs**
```csharp
namespace MyApp.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.") { }
}

public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message) { }
}
```

### Step 3.6: Create Domain Events

**Events/CustomerEvents.cs**
```csharp
using MyApp.SharedKernel.Base;

namespace MyApp.Domain.Events;

public record CustomerCreatedEvent(int CustomerId, string Email) : DomainEvent;
public record CustomerEmailChangedEvent(int CustomerId, string OldEmail, string NewEmail) : DomainEvent;
public record CustomerStatusChangedEvent(int CustomerId, string OldStatus, string NewStatus) : DomainEvent;
```

### Step 3.7: Create Entities

**Entities/Customer.cs**
```csharp
using Ardalis.GuardClauses;
using MyApp.Domain.Enums;
using MyApp.Domain.Events;
using MyApp.Domain.Exceptions;
using MyApp.Domain.ValueObjects;
using MyApp.SharedKernel.Base;
using MyApp.SharedKernel.Interfaces;

namespace MyApp.Domain.Entities;

public sealed class Customer : AggregateRoot<int>, IAuditable, ISoftDelete
{
    public string Name { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public CustomerStatus Status { get; private set; }

    // IAuditable
    public DateTime CreatedOnUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public string? ModifiedBy { get; set; }

    // ISoftDelete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public string? DeletedBy { get; set; }

    // Navigation
    private readonly List<Order> _orders = new();
    public IReadOnlyList<Order> Orders => _orders.AsReadOnly();

    private Customer() { } // EF Core

    public static Customer Create(string name, Email email)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.Null(email, nameof(email));

        var customer = new Customer
        {
            Name = name.Trim(),
            Email = email,
            Status = CustomerStatus.Active
        };

        customer.AddDomainEvent(new CustomerCreatedEvent(customer.Id, email.Value));

        return customer;
    }

    public void UpdateName(string name)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Name = name.Trim();
    }

    public void UpdateEmail(Email email)
    {
        Guard.Against.Null(email, nameof(email));

        if (Email == email) return;

        var oldEmail = Email.Value;
        Email = email;

        AddDomainEvent(new CustomerEmailChangedEvent(Id, oldEmail, email.Value));
    }

    public void Suspend()
    {
        if (Status == CustomerStatus.Suspended)
            throw new BusinessRuleViolationException("Customer is already suspended");

        var oldStatus = Status.ToString();
        Status = CustomerStatus.Suspended;

        AddDomainEvent(new CustomerStatusChangedEvent(Id, oldStatus, Status.ToString()));
    }

    public void Activate()
    {
        if (Status == CustomerStatus.Active)
            throw new BusinessRuleViolationException("Customer is already active");

        var oldStatus = Status.ToString();
        Status = CustomerStatus.Active;

        AddDomainEvent(new CustomerStatusChangedEvent(Id, oldStatus, Status.ToString()));
    }

    public void Deactivate()
    {
        if (Status == CustomerStatus.Inactive)
            throw new BusinessRuleViolationException("Customer is already inactive");

        var oldStatus = Status.ToString();
        Status = CustomerStatus.Inactive;

        AddDomainEvent(new CustomerStatusChangedEvent(Id, oldStatus, Status.ToString()));
    }
}
```

---

## Phase 4: Application Layer

CQRS handlers, validators, and DTOs.

### Step 4.1: Create Project

```bash
dotnet new classlib -n MyApp.Application -o src/Core/MyApp.Application
dotnet sln add src/Core/MyApp.Application/MyApp.Application.csproj
```

### Step 4.2: Update .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>MyApp.Application</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Mapster" />
    <PackageReference Include="Mapster.DependencyInjection" />
    <PackageReference Include="MediatR" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyApp.Domain\MyApp.Domain.csproj" />
  </ItemGroup>

</Project>
```

### Step 4.3: Create DTOs

**Features/Customers/DTOs/CustomerDto.cs**
```csharp
namespace MyApp.Application.Features.Customers.DTOs;

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; }
}
```

### Step 4.4: Create Command

**Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.cs**
```csharp
using MediatR;
using MyApp.Application.Features.Customers.DTOs;
using MyApp.SharedKernel.Results;

namespace MyApp.Application.Features.Customers.Commands.CreateCustomer;

public record CreateCustomerCommand(
    string Name,
    string Email
) : IRequest<Result<CustomerDto>>;
```

**Features/Customers/Commands/CreateCustomer/CreateCustomerCommand.Validator.cs**
```csharp
using FluentValidation;

namespace MyApp.Application.Features.Customers.Commands.CreateCustomer;

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
    }
}
```

**Features/Customers/Commands/CreateCustomer/CreateCustomerCommandHandler.cs**
```csharp
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.Application.Features.Customers.DTOs;
using MyApp.Domain.Entities;
using MyApp.Domain.ValueObjects;
using MyApp.SharedKernel.Interfaces;
using MyApp.SharedKernel.Results;

namespace MyApp.Application.Features.Customers.Commands.CreateCustomer;

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
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating customer: {Email}", request.Email);

        var email = Email.Create(request.Email);
        var customer = Customer.Create(request.Name, email);

        await _repository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created customer with Id: {CustomerId}", customer.Id);

        return Result<CustomerDto>.Success(_mapper.Map<CustomerDto>(customer));
    }
}
```

### Step 4.5: Create Query

**Features/Customers/Queries/GetCustomerById/GetCustomerByIdQuery.cs**
```csharp
using MediatR;
using MyApp.Application.Features.Customers.DTOs;
using MyApp.SharedKernel.Results;

namespace MyApp.Application.Features.Customers.Queries.GetCustomerById;

public record GetCustomerByIdQuery(int Id) : IRequest<Result<CustomerDto>>;
```

**Features/Customers/Queries/GetCustomerById/GetCustomerByIdQueryHandler.cs**
```csharp
using MapsterMapper;
using MediatR;
using MyApp.Application.Features.Customers.DTOs;
using MyApp.Domain.Entities;
using MyApp.SharedKernel.Interfaces;
using MyApp.SharedKernel.Results;

namespace MyApp.Application.Features.Customers.Queries.GetCustomerById;

public class GetCustomerByIdQueryHandler
    : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly IReadRepository<Customer, IApplicationDbContext> _repository;
    private readonly IMapper _mapper;

    public GetCustomerByIdQueryHandler(
        IReadRepository<Customer, IApplicationDbContext> repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Result<CustomerDto>> Handle(
        GetCustomerByIdQuery request,
        CancellationToken cancellationToken)
    {
        var customer = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (customer is null)
            return Result<CustomerDto>.Failure(
                Error.NotFound("Customer.NotFound", $"Customer with Id {request.Id} was not found"));

        return Result<CustomerDto>.Success(_mapper.Map<CustomerDto>(customer));
    }
}
```

### Step 4.6: Create Mapping Configuration

**Common/Mappings/MappingConfig.cs**
```csharp
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Features.Customers.DTOs;
using MyApp.Domain.Entities;

namespace MyApp.Application.Common.Mappings;

public static class MappingConfig
{
    public static IServiceCollection AddMappings(this IServiceCollection services)
    {
        var config = TypeAdapterConfig.GlobalSettings;

        // Customer mappings
        config.NewConfig<Customer, CustomerDto>()
            .Map(dest => dest.Email, src => src.Email.Value)
            .Map(dest => dest.Status, src => src.Status.ToString());

        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
```

### Step 4.7: Create DI Registration

**DependencyInjection.cs**
```csharp
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Common.Mappings;
using MyApp.SharedKernel.Behaviors;

namespace MyApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // MediatR
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(assembly));

        // FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        // Mapster
        services.AddMappings();

        // Pipeline Behaviors (order matters - outermost to innermost)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
```

---

## Phase 5: Persistence Layer

EF Core DbContext, repositories, and interceptors.

### Step 5.1: Create Project

```bash
dotnet new classlib -n MyApp.Persistence -o src/Infrastructure/MyApp.Persistence
dotnet sln add src/Infrastructure/MyApp.Persistence/MyApp.Persistence.csproj
```

### Step 5.2: Update .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>MyApp.Persistence</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Ardalis.Specification.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Core\MyApp.Application\MyApp.Application.csproj" />
  </ItemGroup>

</Project>
```

### Step 5.3: Create DbContext

**Contexts/ApplicationDbContext.cs**
```csharp
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Entities;
using MyApp.SharedKernel.Interfaces;
using System.Reflection;

namespace MyApp.Persistence.Contexts;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
```

### Step 5.4: Create Entity Configuration

**Configurations/CustomerConfiguration.cs**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Entities;

namespace MyApp.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Value Object as Owned Type
        builder.OwnsOne(c => c.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(256);

            email.HasIndex(e => e.Value).IsUnique();
        });

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Optimistic concurrency
        builder.Property(c => c.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Soft delete filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Indexes
        builder.HasIndex(c => c.Name);
        builder.HasIndex(c => c.Status);
    }
}
```

### Step 5.5: Create Interceptors

**Interceptors/AuditableEntityInterceptor.cs**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyApp.SharedKernel.Interfaces;

namespace MyApp.Persistence.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _dateTime;

    public AuditableEntityInterceptor(ICurrentUserService currentUser, IDateTime dateTime)
    {
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedOnUtc = _dateTime.UtcNow;
                entry.Entity.CreatedBy = _currentUser.UserId;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.ModifiedOnUtc = _dateTime.UtcNow;
                entry.Entity.ModifiedBy = _currentUser.UserId;
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedOnUtc = _dateTime.UtcNow;
                entry.Entity.DeletedBy = _currentUser.UserId;
            }
        }
    }
}
```

### Step 5.6: Create Repository

**Repositories/Repository.cs**
```csharp
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyApp.SharedKernel.Interfaces;

namespace MyApp.Persistence.Repositories;

public class Repository<TEntity, TContext> : IRepository<TEntity, TContext>
    where TEntity : class
    where TContext : class, IDbContext
{
    protected readonly DbContext Context;

    public Repository(TContext context)
    {
        Context = context as DbContext
            ?? throw new ArgumentException("Context must derive from DbContext");
    }

    public virtual async Task<TEntity?> GetByIdAsync<TId>(TId id, CancellationToken ct = default)
        where TId : notnull
    {
        return await Context.Set<TEntity>().FindAsync(new object[] { id }, ct);
    }

    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        ISpecification<TEntity> specification,
        CancellationToken ct = default)
    {
        return await ApplySpecification(specification).FirstOrDefaultAsync(ct);
    }

    public virtual async Task<List<TEntity>> ListAsync(CancellationToken ct = default)
    {
        return await Context.Set<TEntity>().ToListAsync(ct);
    }

    public virtual async Task<List<TEntity>> ListAsync(
        ISpecification<TEntity> specification,
        CancellationToken ct = default)
    {
        return await ApplySpecification(specification).ToListAsync(ct);
    }

    public virtual async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await Context.Set<TEntity>().CountAsync(ct);
    }

    public virtual async Task<int> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken ct = default)
    {
        return await ApplySpecification(specification).CountAsync(ct);
    }

    public virtual async Task<bool> AnyAsync(CancellationToken ct = default)
    {
        return await Context.Set<TEntity>().AnyAsync(ct);
    }

    public virtual async Task<bool> AnyAsync(
        ISpecification<TEntity> specification,
        CancellationToken ct = default)
    {
        return await ApplySpecification(specification).AnyAsync(ct);
    }

    public virtual IQueryable<TEntity> Query()
    {
        return Context.Set<TEntity>().AsQueryable();
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await Context.Set<TEntity>().AddAsync(entity, ct);
        return entity;
    }

    public virtual Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        Context.Set<TEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(TEntity entity, CancellationToken ct = default)
    {
        Context.Set<TEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        Context.Set<TEntity>().RemoveRange(entities);
        return Task.CompletedTask;
    }

    protected IQueryable<TEntity> ApplySpecification(ISpecification<TEntity> specification)
    {
        return SpecificationEvaluator.Default.GetQuery(
            Context.Set<TEntity>().AsQueryable(),
            specification);
    }
}
```

### Step 5.7: Create Unit of Work

**Common/UnitOfWork.cs**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyApp.SharedKernel.Interfaces;

namespace MyApp.Persistence.Common;

public class UnitOfWork<TContext> : IUnitOfWork<TContext>
    where TContext : class, IDbContext
{
    private readonly DbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(TContext context)
    {
        _context = context as DbContext
            ?? throw new ArgumentException("Context must derive from DbContext");
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Transaction not started");

        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Transaction not started");

        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
```

### Step 5.8: Create DI Registration

**DependencyInjection.cs**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Persistence.Common;
using MyApp.Persistence.Contexts;
using MyApp.Persistence.Interceptors;
using MyApp.Persistence.Repositories;
using MyApp.SharedKernel.Interfaces;

namespace MyApp.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Interceptors
        services.AddScoped<AuditableEntityInterceptor>();

        // DbContext
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();

            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null));

            options.AddInterceptors(interceptor);
        });

        // Register as interface
        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // Repositories
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped(typeof(IReadRepository<,>), typeof(Repository<,>));

        // Unit of Work
        services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));

        return services;
    }
}
```

---

## Phase 6: Infrastructure Layer

Cross-cutting services (email, caching, etc.).

### Step 6.1: Create Project

```bash
dotnet new classlib -n MyApp.Infrastructure -o src/Infrastructure/MyApp.Infrastructure
dotnet sln add src/Infrastructure/MyApp.Infrastructure/MyApp.Infrastructure.csproj
```

### Step 6.2: Update .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>MyApp.Infrastructure</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Dapper" />
    <PackageReference Include="Microsoft.Data.SqlClient" />
    <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Core\MyApp.Application\MyApp.Application.csproj" />
  </ItemGroup>

</Project>
```

### Step 6.3: Create Services

**Services/DateTimeService.cs**
```csharp
using MyApp.SharedKernel.Interfaces;

namespace MyApp.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}
```

**Services/CurrentUserService.cs**
```csharp
using Microsoft.AspNetCore.Http;
using MyApp.SharedKernel.Interfaces;
using System.Security.Claims;

namespace MyApp.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
```

**Services/MemoryCacheService.cs**
```csharp
using Microsoft.Extensions.Caching.Memory;
using MyApp.SharedKernel.DependencyInjection;

namespace MyApp.Infrastructure.Services;

public interface ICacheService : IScopedService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task<T?> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        return Task.FromResult(_cache.TryGetValue(key, out T? value) ? value : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
            options.SetSlidingExpiration(expiration.Value);

        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out T? value))
            return value;

        value = await factory(ct);

        if (value is not null)
            await SetAsync(key, value, expiration, ct);

        return value;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
```

### Step 6.4: Create DI Registration

**DependencyInjection.cs**
```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Infrastructure.Services;
using MyApp.SharedKernel.DependencyInjection;
using MyApp.SharedKernel.Interfaces;

namespace MyApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core services
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpContextAccessor();

        // Caching
        services.AddMemoryCache();
        services.AddScoped<ICacheService, MemoryCacheService>();

        // Auto-register services with lifetime markers
        services.AddServicesFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
```

---

## Phase 7: WebApi Layer

ASP.NET Core API controllers and configuration.

### Step 7.1: Create Project

```bash
dotnet new webapi -n MyApp.WebApi -o src/Presentation/MyApp.WebApi --no-https
dotnet sln add src/Presentation/MyApp.WebApi/MyApp.WebApi.csproj
```

### Step 7.2: Update .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <RootNamespace>MyApp.WebApi</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Swashbuckle.AspNetCore" />
    <PackageReference Include="AspNetCore.HealthChecks.SqlServer" />
    <PackageReference Include="AspNetCore.HealthChecks.UI.Client" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Infrastructure\MyApp.Infrastructure\MyApp.Infrastructure.csproj" />
    <ProjectReference Include="..\..\Infrastructure\MyApp.Persistence\MyApp.Persistence.csproj" />
  </ItemGroup>

</Project>
```

### Step 7.3: Create Base Controller

**Controllers/ApiControllerBase.cs**
```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyApp.SharedKernel.Results;

namespace MyApp.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly IMediator Mediator;

    protected ApiControllerBase(IMediator mediator)
    {
        Mediator = mediator;
    }

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

    protected IActionResult HandleCreatedResult<T>(
        Result<T> result,
        string actionName,
        object routeValues)
    {
        if (result.IsSuccess)
            return CreatedAtAction(actionName, routeValues, result.Value);

        return HandleError(result.Error);
    }

    private IActionResult HandleError(Error error)
    {
        return error.Code switch
        {
            var code when code.Contains("NotFound") => NotFound(new { error.Code, error.Description }),
            var code when code.Contains("Validation") => BadRequest(new { error.Code, error.Description }),
            var code when code.Contains("Conflict") => Conflict(new { error.Code, error.Description }),
            var code when code.Contains("Unauthorized") => Unauthorized(new { error.Code, error.Description }),
            var code when code.Contains("Forbidden") => Forbid(),
            _ => BadRequest(new { error.Code, error.Description })
        };
    }
}
```

### Step 7.4: Create Controllers

**Controllers/CustomersController.cs**
```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Features.Customers.Commands.CreateCustomer;
using MyApp.Application.Features.Customers.Queries.GetCustomerById;

namespace MyApp.WebApi.Controllers;

public class CustomersController : ApiControllerBase
{
    public CustomersController(IMediator mediator) : base(mediator) { }

    [HttpGet("{id:int}")]
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
}
```

### Step 7.5: Create Middleware

**Middleware/ExceptionMiddleware.cs**
```csharp
using FluentValidation;
using MyApp.Domain.Exceptions;
using System.Text.Json;

namespace MyApp.WebApi.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                new
                {
                    Code = "Validation.Error",
                    Description = "Validation failed",
                    Errors = validationEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
                }),
            EntityNotFoundException => (
                StatusCodes.Status404NotFound,
                new { Code = "NotFound", Description = exception.Message } as object),
            BusinessRuleViolationException => (
                StatusCodes.Status400BadRequest,
                new { Code = "BusinessRule.Violation", Description = exception.Message } as object),
            DomainException => (
                StatusCodes.Status400BadRequest,
                new { Code = "Domain.Error", Description = exception.Message } as object),
            _ => (
                StatusCodes.Status500InternalServerError,
                new { Code = "Internal.Error", Description = "An unexpected error occurred" } as object)
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(message));
    }
}
```

### Step 7.6: Update Program.cs

**Program.cs**
```csharp
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MyApp.Application;
using MyApp.Infrastructure;
using MyApp.Persistence;
using MyApp.WebApi.Middleware;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting application");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, config) =>
        config.ReadFrom.Configuration(context.Configuration));

    // Add layers
    builder.Services
        .AddApplication()
        .AddPersistence(builder.Configuration)
        .AddInfrastructure(builder.Configuration);

    // Controllers
    builder.Services.AddControllers();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

    var app = builder.Build();

    // Middleware
    app.UseMiddleware<ExceptionMiddleware>();

    // Swagger (always on for now)
    app.UseSwagger();
    app.UseSwaggerUI();

    // Serilog request logging
    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    // Health checks
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
```

### Step 7.7: Update appsettings.json

**appsettings.json**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MyAppDb;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  },
  "AllowedHosts": "*"
}
```

---

## Phase 8: Aspire Integration

Add observability dashboard.

### Step 8.1: Create ServiceDefaults

```bash
dotnet new aspire-servicedefaults -n MyApp.ServiceDefaults -o src/Aspire/MyApp.ServiceDefaults
dotnet sln add src/Aspire/MyApp.ServiceDefaults/MyApp.ServiceDefaults.csproj
```

### Step 8.2: Fix .csproj (Central Package Management)

Remove Version attributes from the generated `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
  </ItemGroup>

</Project>
```

### Step 8.3: Create AppHost

```bash
dotnet new aspire-apphost -n MyApp.AppHost -o src/Aspire/MyApp.AppHost
dotnet sln add src/Aspire/MyApp.AppHost/MyApp.AppHost.csproj
```

### Step 8.4: Fix AppHost .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAspireHost>true</IsAspireHost>
    <UserSecretsId>myapp-apphost-secrets</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.AppHost" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Presentation\MyApp.WebApi\MyApp.WebApi.csproj" />
  </ItemGroup>

</Project>
```

### Step 8.5: Update AppHost Program.cs

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MyApp_WebApi>("webapi");

builder.Build().Run();
```

### Step 8.6: Add ServiceDefaults to WebApi

Update `MyApp.WebApi.csproj`:
```xml
<ItemGroup>
  <ProjectReference Include="..\..\Aspire\MyApp.ServiceDefaults\MyApp.ServiceDefaults.csproj" />
</ItemGroup>
```

Update `Program.cs`:
```csharp
// After builder creation
builder.AddServiceDefaults();

// After app.MapControllers()
app.MapDefaultEndpoints();
```

### Step 8.7: Add HTTP Profile for Aspire

Create/update `Properties/launchSettings.json` in AppHost:

```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "http://localhost:15191",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "ASPIRE_ALLOW_UNSECURED_TRANSPORT": "true"
      }
    }
  }
}
```

---

## Phase 9: Testing Projects

### Step 9.1: Create Unit Tests Project

```bash
dotnet new xunit -n MyApp.UnitTests -o tests/MyApp.UnitTests
dotnet sln add tests/MyApp.UnitTests/MyApp.UnitTests.csproj
```

Update `.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Core\MyApp.Application\MyApp.Application.csproj" />
  </ItemGroup>

</Project>
```

### Step 9.2: Create Integration Tests Project

```bash
dotnet new xunit -n MyApp.IntegrationTests -o tests/MyApp.IntegrationTests
dotnet sln add tests/MyApp.IntegrationTests/MyApp.IntegrationTests.csproj
```

Update `.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Presentation\MyApp.WebApi\MyApp.WebApi.csproj" />
  </ItemGroup>

</Project>
```

### Step 9.3: Create Architecture Tests Project

```bash
dotnet new xunit -n MyApp.ArchitectureTests -o tests/MyApp.ArchitectureTests
dotnet sln add tests/MyApp.ArchitectureTests/MyApp.ArchitectureTests.csproj
```

Update `.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NetArchTest.Rules" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Core\MyApp.Application\MyApp.Application.csproj" />
    <ProjectReference Include="..\..\src\Infrastructure\MyApp.Persistence\MyApp.Persistence.csproj" />
    <ProjectReference Include="..\..\src\Presentation\MyApp.WebApi\MyApp.WebApi.csproj" />
  </ItemGroup>

</Project>
```

**LayerDependencyTests.cs**
```csharp
using FluentAssertions;
using NetArchTest.Rules;
using MyApp.Domain.Entities;
using MyApp.Application.Features.Customers.Commands.CreateCustomer;
using MyApp.Persistence.Contexts;

namespace MyApp.ArchitectureTests;

public class LayerDependencyTests
{
    [Fact]
    public void Domain_Should_Not_Have_Dependency_On_Application()
    {
        var result = Types.InAssembly(typeof(Customer).Assembly)
            .Should()
            .NotHaveDependencyOn("MyApp.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_Should_Not_Have_Dependency_On_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Customer).Assembly)
            .Should()
            .NotHaveDependencyOn("MyApp.Infrastructure")
            .And()
            .NotHaveDependencyOn("MyApp.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_Should_Not_Have_Dependency_On_Infrastructure()
    {
        var result = Types.InAssembly(typeof(CreateCustomerCommand).Assembly)
            .Should()
            .NotHaveDependencyOn("MyApp.Infrastructure")
            .And()
            .NotHaveDependencyOn("MyApp.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_Should_Have_Name_Ending_With_Handler()
    {
        var result = Types.InAssembly(typeof(CreateCustomerCommand).Assembly)
            .That()
            .ImplementInterface(typeof(MediatR.IRequestHandler<,>))
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
```

---

## Phase 10: Final Configuration

### Step 10.1: Create Database

```bash
cd src/Infrastructure/MyApp.Persistence
dotnet ef migrations add InitialCreate --startup-project ../../Presentation/MyApp.WebApi
dotnet ef database update --startup-project ../../Presentation/MyApp.WebApi
```

### Step 10.2: Verify Structure

Your final structure should look like:

```
MyApp/
├── src/
│   ├── Core/
│   │   ├── MyApp.SharedKernel/
│   │   ├── MyApp.Domain/
│   │   └── MyApp.Application/
│   ├── Infrastructure/
│   │   ├── MyApp.Infrastructure/
│   │   └── MyApp.Persistence/
│   ├── Presentation/
│   │   └── MyApp.WebApi/
│   └── Aspire/
│       ├── MyApp.ServiceDefaults/
│       └── MyApp.AppHost/
├── tests/
│   ├── MyApp.UnitTests/
│   ├── MyApp.IntegrationTests/
│   └── MyApp.ArchitectureTests/
├── docs/
├── Directory.Build.props
├── Directory.Packages.props
└── MyApp.sln
```

### Step 10.3: Run the Application

```bash
# With Aspire Dashboard
cd src/Aspire/MyApp.AppHost
dotnet run

# Or directly
cd src/Presentation/MyApp.WebApi
dotnet run
```

### Step 10.4: Test Endpoints

```bash
# Health check
curl http://localhost:5000/health

# Create customer
curl -X POST http://localhost:5000/api/customers \
  -H "Content-Type: application/json" \
  -d '{"name": "John Doe", "email": "john@example.com"}'

# Get customer
curl http://localhost:5000/api/customers/1
```

---

## Verification Checklist

### Architecture
- [ ] Domain has no dependencies on Application or Infrastructure
- [ ] Application only depends on Domain and SharedKernel
- [ ] Infrastructure implements interfaces from inner layers
- [ ] All handlers follow naming convention (*Handler)

### Features
- [ ] CQRS commands and queries work
- [ ] Validation runs automatically via pipeline
- [ ] Audit fields populated automatically
- [ ] Soft delete works (entities marked, not removed)
- [ ] Health checks return healthy status

### Observability
- [ ] Aspire dashboard loads
- [ ] WebApi appears in Resources tab
- [ ] Traces show request flow
- [ ] Logs appear in Console tab

### Testing
- [ ] Unit tests pass
- [ ] Architecture tests pass
- [ ] Integration tests pass

---

## Next Steps

1. **Add more entities** - Follow the Customer pattern
2. **Add authentication** - ASP.NET Core Identity or JWT
3. **Add more services** - Email, file storage, external APIs
4. **Add Redis caching** - Replace MemoryCache
5. **Add more tests** - Increase coverage
6. **Add CI/CD** - GitHub Actions or Azure DevOps

For detailed patterns and recipes, see:
- [README.md](./README.md) - Full documentation
- [QUICK_REFERENCE.md](./QUICK_REFERENCE.md) - Code snippets
- [COOKBOOK.md](./COOKBOOK.md) - Common scenarios
