# .NET Aspire Integration Guide

A complete guide to integrating .NET Aspire into an existing ASP.NET Core project for observability and orchestration.

## What You Get

After integration, you'll have:
- **Resources Dashboard** - View all services with clickable endpoints (Swagger, health checks)
- **Distributed Tracing** - Request flows through API, handlers, EF Core
- **Metrics** - Request rates, latency, memory, GC stats
- **Structured Logs** - Correlated logs with trace IDs
- **Service Discovery** - Automatic service resolution
- **HTTP Resilience** - Built-in retry policies

---

## Prerequisites

### 1. Install Aspire Workload

```bash
dotnet workload install aspire
```

Verify installation:
```bash
dotnet workload list
```

### 2. .NET 8.0+ SDK

Aspire requires .NET 8.0 or later.

---

## Step 1: Add Aspire Packages

Add to your `Directory.Packages.props` (or directly to csproj if not using central package management):

```xml
<!-- .NET Aspire -->
<PackageVersion Include="Aspire.Hosting" Version="8.2.2" />
<PackageVersion Include="Aspire.Hosting.AppHost" Version="8.2.2" />
<PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="8.10.0" />
<PackageVersion Include="Microsoft.Extensions.ServiceDiscovery" Version="8.2.2" />

<!-- OpenTelemetry (used by ServiceDefaults) -->
<PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
<PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
<PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.0" />
```

---

## Step 2: Create ServiceDefaults Project

This shared project configures OpenTelemetry, health checks, and resilience for all services.

### 2.1 Create Project

```bash
dotnet new aspire-servicedefaults -n YourApp.ServiceDefaults -o src/Aspire/YourApp.ServiceDefaults
```

### 2.2 Fix Central Package Management (if using)

If you use `Directory.Packages.props`, remove versions from the generated `.csproj`:

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

    <!-- Remove Version attributes if using central package management -->
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

### 2.3 Extensions.cs (Auto-generated)

The template creates `Extensions.cs` with:
- `AddServiceDefaults()` - Main entry point
- `ConfigureOpenTelemetry()` - Traces, metrics, logs
- `AddDefaultHealthChecks()` - Health endpoints
- `MapDefaultEndpoints()` - Health check routes

---

## Step 3: Create AppHost Project

The orchestrator that starts all services and the dashboard.

### 3.1 Create Project

```bash
dotnet new aspire-apphost -n YourApp.AppHost -o src/Aspire/YourApp.AppHost
```

### 3.2 Fix Central Package Management (if using)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsAspireHost>true</IsAspireHost>
    <UserSecretsId>your-guid-here</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <!-- Remove Version if using central package management -->
    <PackageReference Include="Aspire.Hosting.AppHost" />
  </ItemGroup>

  <!-- Reference your API project -->
  <ItemGroup>
    <ProjectReference Include="..\..\YourApp.WebApi\YourApp.WebApi.csproj" />
  </ItemGroup>

</Project>
```

### 3.3 Configure Program.cs

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add your API project
builder.AddProject<Projects.YourApp_WebApi>("webapi");

// Add more services as needed:
// builder.AddProject<Projects.YourApp_Worker>("worker");
// builder.AddRedis("cache");
// builder.AddSqlServer("sql");

builder.Build().Run();
```

### 3.4 Fix Launch Settings for HTTP (Optional)

If you want to run without HTTPS, add to `Properties/launchSettings.json`:

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

## Step 4: Update Your WebApi

### 4.1 Add ServiceDefaults Reference

In your WebApi `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\Aspire\YourApp.ServiceDefaults\YourApp.ServiceDefaults.csproj" />
</ItemGroup>
```

### 4.2 Update Program.cs

Add `AddServiceDefaults()` in your service configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Your existing services...
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// Add Aspire ServiceDefaults (OpenTelemetry, Health Checks, Resilience)
builder.AddServiceDefaults();

var app = builder.Build();

// Your existing middleware...
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
```

---

## Step 5: Add Projects to Solution

```bash
dotnet sln add src/Aspire/YourApp.ServiceDefaults/YourApp.ServiceDefaults.csproj
dotnet sln add src/Aspire/YourApp.AppHost/YourApp.AppHost.csproj
```

---

## Step 6: Run

```bash
cd src/Aspire/YourApp.AppHost
dotnet run
```

The console will display:
```
Now listening on: http://localhost:15191
Login to the dashboard at http://localhost:15191/login?t=<token>
```

Open the dashboard URL to see:
- **Resources** tab with your services and clickable endpoints
- **Console** with live logs
- **Traces**, **Metrics**, **Structured Logs** tabs

---

## Project Structure

After integration:

```
src/
├── Aspire/
│   ├── YourApp.AppHost/           # Run this to start everything
│   │   ├── Program.cs
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   └── YourApp.AppHost.csproj
│   └── YourApp.ServiceDefaults/   # Shared telemetry config
│       ├── Extensions.cs
│       └── YourApp.ServiceDefaults.csproj
└── YourApp.WebApi/
    ├── Program.cs                 # Uses AddServiceDefaults()
    └── YourApp.WebApi.csproj      # References ServiceDefaults
```

---

## Adding More Services

### Add Another Project

In AppHost `Program.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.YourApp_WebApi>("webapi");
var worker = builder.AddProject<Projects.YourApp_Worker>("worker");

builder.Build().Run();
```

### Add External Resources

```csharp
// Redis
var cache = builder.AddRedis("cache");

// SQL Server
var sql = builder.AddSqlServer("sql")
    .AddDatabase("mydb");

// Reference in projects
builder.AddProject<Projects.YourApp_WebApi>("webapi")
    .WithReference(cache)
    .WithReference(sql);
```

---

## Troubleshooting

### "Aspire workload not installed"

```bash
dotnet workload install aspire
```

### "HTTPS required" error

Add to AppHost `launchSettings.json`:
```json
"ASPIRE_ALLOW_UNSECURED_TRANSPORT": "true"
```

Or run with HTTPS profile:
```bash
dotnet run --launch-profile https
```

### "Endpoint already exists" error

Don't add explicit endpoints if they're auto-detected:

```csharp
// Wrong - causes conflict
builder.AddProject<Projects.WebApi>("webapi")
    .WithHttpEndpoint(name: "http");  // Remove this

// Correct - endpoints auto-detected
builder.AddProject<Projects.WebApi>("webapi");
```

### Central Package Management conflicts

Remove `Version` attributes from Aspire template-generated `.csproj` files and add versions to `Directory.Packages.props` instead.

### No traces in dashboard

1. Make some API requests first
2. Check OTLP endpoint configuration
3. Verify `AddServiceDefaults()` is called in Program.cs

---

## Quick Reference

| Command | Description |
|---------|-------------|
| `dotnet workload install aspire` | Install Aspire workload |
| `dotnet new aspire-servicedefaults` | Create ServiceDefaults project |
| `dotnet new aspire-apphost` | Create AppHost project |
| `dotnet run` (in AppHost) | Start dashboard + all services |
| `dotnet run --launch-profile https` | Start with HTTPS |

---

## Links

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard)
- [Service Discovery](https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery)
- [OpenTelemetry in Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/telemetry)
