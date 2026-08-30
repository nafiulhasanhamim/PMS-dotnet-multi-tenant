using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using PMS.PerformanceTests.Benchmarks;

namespace PMS.PerformanceTests;

/// <summary>
/// Entry point for running BenchmarkDotNet benchmarks.
/// Run with: dotnet run -c Release
/// </summary>
public class PerformanceTestRunner
{
    public static void Main(string[] args)
    {
        // Parse command line args to determine which benchmarks to run
        if (args.Length > 0 && args[0] == "--filter")
        {
            // Run specific benchmark: dotnet run -c Release -- --filter *QueryBenchmarks*
            BenchmarkSwitcher.FromAssembly(typeof(PerformanceTestRunner).Assembly).Run(args);
        }
        else if (args.Length > 0 && args[0] == "--list")
        {
            // List available benchmarks
            Console.WriteLine("Available Benchmarks:");
            Console.WriteLine("  - QueryBenchmarks: EF Core query performance");
            Console.WriteLine("  - HandlerBenchmarks: MediatR handler performance");
            Console.WriteLine("  - ApiLoadTests: NBomber load testing");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -c Release                           # Run all benchmarks");
            Console.WriteLine("  dotnet run -c Release -- --filter *Query*       # Run query benchmarks");
            Console.WriteLine("  dotnet run -c Release -- --job short            # Quick benchmark run");
        }
        else
        {
            // Run all benchmarks
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .WithOptions(ConfigOptions.JoinSummary);

            BenchmarkRunner.Run<QueryBenchmarks>(config);
            BenchmarkRunner.Run<HandlerBenchmarks>(config);
        }
    }
}
