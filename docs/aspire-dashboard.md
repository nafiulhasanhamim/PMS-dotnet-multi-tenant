# .NET Aspire Dashboard Setup

This document explains how to use .NET Aspire for observability in PMS.

## Overview

.NET Aspire provides:
- **Resources Tab** - View all services with clickable endpoints (Swagger, health checks)
- **Traces** - Distributed tracing through API, handlers, and EF Core
- **Metrics** - Request rates, latency, memory, GC stats
- **Logs** - Structured logs with correlation

## Quick Start (Recommended)

### Run with Aspire AppHost

```bash
cd src/Aspire/PMS.AppHost
dotnet run
```

This will:
1. Start the Aspire Dashboard automatically
2. Launch the WebApi
3. Show all resources with clickable links

### Access Dashboard

After running, the console will show the dashboard URL (typically `https://localhost:17xxx`).

**Resources Tab** shows:
- WebApi endpoints (HTTP/HTTPS)
- Swagger UI link (`/swagger`)
- Health check endpoints

## Project Structure

```
src/Aspire/
├── PMS.AppHost/         # Aspire orchestrator (run this)
│   ├── Program.cs                 # Defines resources
│   └── PMS.AppHost.csproj
└── PMS.ServiceDefaults/ # Shared telemetry config
    ├── Extensions.cs              # OpenTelemetry, health checks
    └── PMS.ServiceDefaults.csproj
```

## What's Included

### ServiceDefaults provides:
- OpenTelemetry (traces, metrics, logs)
- Health checks (`/health`, `/alive`)
- Service discovery
- HTTP client resilience

### AppHost orchestrates:
- WebApi project
- Dashboard visualization
- Resource management

## Alternative: Standalone Dashboard (Docker)

If you want to run the API independently with just the dashboard:

```bash
# Start dashboard container
docker-compose up -d

# Run API separately
cd src/Presentation/PMS.WebApi
dotnet run
```

Dashboard: `http://localhost:18888`

**Note:** Standalone mode does NOT have the Resources tab with clickable links.

## Dashboard Tabs

| Tab | What You See |
|-----|--------------|
| **Resources** | All services with endpoints, status, logs link |
| **Console** | Live console output from services |
| **Traces** | Request pipeline visualization |
| **Metrics** | Performance graphs |
| **Structured Logs** | Correlated log entries |

## What Gets Traced

| Component | Data Captured |
|-----------|---------------|
| ASP.NET Core | HTTP requests, responses, middleware |
| HttpClient | Outgoing HTTP calls |
| Entity Framework | SQL queries, parameters, duration |
| Runtime | GC collections, thread pool, memory |

## Configuration

### OTLP Endpoint

When using AppHost, the endpoint is auto-configured. For standalone mode:

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

## Troubleshooting

### AppHost fails to start

Ensure Aspire workload is installed:
```bash
dotnet workload install aspire
```

### No traces appearing

Make API calls first - traces only appear after requests.

### Port conflicts

Aspire uses dynamic ports. Check console output for actual URLs.

### Standalone dashboard: No data

1. Verify container is running: `docker ps`
2. Check OTLP endpoint configuration
3. Ensure WebApi started without errors
