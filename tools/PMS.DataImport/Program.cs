using PMS.DataImport;
using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Medicine reference catalog importer.
//
// A platform operation, run by hand, occasionally. Deliberately NOT an API endpoint: it
// rewrites shared data for every pharmacy at once, takes tens of seconds, and there is no
// version of "a tenant user triggers this over HTTP" that is a good idea.
//
//   dotnet run --project tools/PMS.DataImport -- --source <folder> --dry-run
//   dotnet run --project tools/PMS.DataImport -- --source <folder>
//
// See docs/data/medicine-reference-catalog.md.
// ═══════════════════════════════════════════════════════════════════════════════════════════

var options = CommandLine.Parse(args);

if (options is null)
{
    CommandLine.PrintUsage();
    return 2;
}

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = options.ConnectionString
    ?? config.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "No connection string. Pass --connection, or set ConnectionStrings__DefaultConnection.");
    return 2;
}

if (!Directory.Exists(options.SourceFolder))
{
    Console.Error.WriteLine($"Source folder not found: {options.SourceFolder}");
    return 2;
}

var report = new ImportReport { DryRun = options.DryRun };

Console.WriteLine();
Console.WriteLine("Medicine reference catalog import");
Console.WriteLine($"  source     : {options.SourceFolder}");
Console.WriteLine($"  mode       : {(options.DryRun ? "DRY RUN - nothing will be written" : "LIVE")}");
Console.WriteLine($"  reports to : {options.OutputFolder}");
Console.WriteLine();

// A DbContext with NO tenant context, on purpose.
//
// The catalog tables do not implement ITenantEntity, so no query filter and no interceptor
// applies to them, and there is no tenant to supply. This is also the cleanest proof that the
// catalog sits outside the tenant mechanism: if any of it were tenant-scoped, this importer
// would silently read and write nothing, because an unresolved tenant is Guid.Empty and
// matches no rows.
var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString, sql => sql.CommandTimeout(180))
    .Options;

await using var db = new ApplicationDbContext(dbOptions);

try
{
    if (!await db.Database.CanConnectAsync())
    {
        Console.Error.WriteLine("Cannot connect to the database. Check the connection string.");
        return 3;
    }

    var importer = new CatalogImporter(
        db, options.SourceFolder, report, options.DryRun, Console.WriteLine);

    await importer.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Import FAILED: {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);

    // Still write the reports — knowing how far it got is the useful part of a failure.
    WriteReports(report, options.OutputFolder);
    return 1;
}

Console.WriteLine(report.BuildSummary());
WriteReports(report, options.OutputFolder);

if (options.DryRun)
{
    Console.WriteLine("  Dry run only. Re-run without --dry-run to write.");
}

return 0;

static void WriteReports(ImportReport report, string folder)
{
    Directory.CreateDirectory(folder);

    var stamp = report.StartedUtc.ToString("yyyyMMdd-HHmmss");
    var suffix = report.DryRun ? "-dryrun" : string.Empty;

    var files = new (string Name, string Content)[]
    {
        ($"import-summary-{stamp}{suffix}.txt", report.BuildSummary()),
        ($"antibiotic-review-{stamp}{suffix}.txt", report.BuildAntibioticReport()),
        ($"rejected-rows-{stamp}{suffix}.txt", report.BuildRejectsReport()),
    };

    foreach (var (name, content) in files)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, content, new System.Text.UTF8Encoding(false));
        Console.WriteLine($"  wrote {path}");
    }
}

/// <summary>
/// Hand-rolled argument parsing. Deliberately not System.CommandLine: four flags do not
/// justify a beta dependency, and the failure mode of an unknown flag should be a clear
/// message rather than a stack trace.
/// </summary>
internal sealed record ImportOptions(
    string SourceFolder,
    string OutputFolder,
    bool DryRun,
    string? ConnectionString);

internal static class CommandLine
{
    public static ImportOptions? Parse(string[] args)
    {
        var source = @"C:\Projects\bd-medicine-scraper-dev\kaggle_data";
        var output = Path.Combine(Directory.GetCurrentDirectory(), "import-reports");
        var dryRun = false;
        string? connection = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--source" or "-s" when i + 1 < args.Length:
                    source = args[++i];
                    break;

                case "--output" or "-o" when i + 1 < args.Length:
                    output = args[++i];
                    break;

                case "--connection" or "-c" when i + 1 < args.Length:
                    connection = args[++i];
                    break;

                case "--dry-run" or "-n":
                    dryRun = true;
                    break;

                case "--help" or "-h" or "-?":
                    return null;

                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    return null;
            }
        }

        return new ImportOptions(source, output, dryRun, connection);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            Medicine reference catalog importer

            Usage:
              dotnet run --project tools/PMS.DataImport -- [options]

            Options:
              -s, --source <folder>      Folder holding the six source CSVs.
                                         Default: C:\Projects\bd-medicine-scraper-dev\kaggle_data
              -o, --output <folder>      Where the report files are written.
                                         Default: ./import-reports
              -c, --connection <string>  Override the connection string. Otherwise taken from
                                         appsettings.json or ConnectionStrings__DefaultConnection.
              -n, --dry-run              Parse, validate and report. Write nothing.
              -h, --help                 This message.

            Run --dry-run first, read the summary and the antibiotic review file, then run for
            real. Both are safe to repeat: the import upserts, so a second run updates rather
            than duplicating.
            """);
    }
}
