# Performance Tests

This project contains performance benchmarks and load tests for PMS.

## Prerequisites

- .NET 8.0 SDK
- Release build configuration for accurate results

## Test Types

### 1. BenchmarkDotNet (Micro-Benchmarks)

Measures code-level performance with precise metrics including execution time, memory allocation, and GC pressure.

**Available Benchmarks:**
- `QueryBenchmarks` - Database query performance
- `HandlerBenchmarks` - MediatR handler performance

### 2. NBomber (Load Tests)

HTTP load testing for API endpoints under concurrent traffic.

**Available Tests:**
- `ApiLoadTests` - Tests health and API endpoints under load

## Running Benchmarks

### Run All Benchmarks

```bash
cd tests/PMS.PerformanceTests
dotnet run -c Release
```

### Run Specific Benchmark Class

```bash
# Query benchmarks only
dotnet run -c Release -- --filter *QueryBenchmarks*

# Handler benchmarks only
dotnet run -c Release -- --filter *HandlerBenchmarks*
```

### Run Quick Benchmarks (Less Precise)

```bash
dotnet run -c Release -- --job short --filter *QueryBenchmarks*
```

### List Available Benchmarks

```bash
dotnet run -c Release -- --list flat
```

## Running Load Tests

Load tests are xUnit tests marked with `Skip` attribute by default.

### Enable and Run Load Tests

1. Remove the `Skip` attribute from test methods in `LoadTests/ApiLoadTests.cs`
2. Run the tests:

```bash
dotnet test --filter "FullyQualifiedName~LoadTests"
```

## Output

### BenchmarkDotNet Results

Results are saved to `BenchmarkDotNet.Artifacts/` folder:
- `results/` - CSV, JSON, and HTML reports
- Summary table printed to console

### NBomber Results

Results are saved to `nbomber_reports/` folder:
- HTML reports with charts
- JSON data for analysis

## Benchmark Results Interpretation

| Metric | Description |
|--------|-------------|
| Mean | Average execution time |
| Error | Half of 99.9% confidence interval |
| StdDev | Standard deviation |
| Gen0/Gen1 | GC collections per 1000 operations |
| Allocated | Memory allocated per operation |

## Example Results

| Query Type | 100 records | 500 records | 1000 records |
|------------|-------------|-------------|--------------|
| Count operations | ~20-23 us | ~63-99 us | ~122-195 us |
| Simple queries | ~32-128 us | ~248-724 us | ~378-1599 us |
| Eager loading | ~128-271 us | ~543-2593 us | ~744-6299 us |

## Tips

- Always run in `Release` mode for accurate results
- Close other applications to reduce noise
- Run benchmarks multiple times for consistency
- Use `--job short` for quick feedback during development
