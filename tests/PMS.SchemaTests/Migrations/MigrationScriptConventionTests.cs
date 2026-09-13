using FluentAssertions;
using Xunit;

namespace PMS.SchemaTests.Migrations;

/// <summary>
/// Conventions that every migration script has to follow, checked across the whole folder.
///
/// <para><b>These exist because of a bug that shipped.</b> Script 015 created a filtered index
/// without declaring <c>SET QUOTED_IDENTIFIER ON</c>. It passed every time it was applied by hand
/// — <c>Invoke-Sqlcmd</c> defaults that option ON — and failed the first time anything ran it
/// through the <c>sqlcmd</c> CLI, which defaults it OFF. The script created its tables and then
/// died on the index, leaving a half-built schema.</para>
///
/// <para>The cost of finding that was a container build. The cost of finding the next one is this
/// file.</para>
/// </summary>
public class MigrationScriptConventionTests
{
    private static IReadOnlyList<(string Name, string Sql)> Scripts()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "database", "scripts")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull(
            "the database/scripts folder has to be findable from the test binary");

        return Directory
            .EnumerateFiles(
                Path.Combine(directory!.FullName, "database", "scripts"), "*.sql")
            .Select(path => (Name: Path.GetFileName(path), Sql: File.ReadAllText(path)))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// A filtered index cannot be created while QUOTED_IDENTIFIER is OFF — SQL Server refuses it
    /// outright with a message about SET options that names half a dozen unrelated features.
    ///
    /// <para>Relying on the caller to have it right is what went wrong: SSMS, Azure Data Studio
    /// and <c>Invoke-Sqlcmd</c> all set it ON, and the <c>sqlcmd</c> CLI does not. The script has
    /// to say so itself.</para>
    /// </summary>
    [Fact]
    public void EveryScriptWithAFilteredIndex_DeclaresQuotedIdentifierOn()
    {
        var offenders = Scripts()
            .Where(s => HasFilteredIndex(s.Sql))
            .Where(s => !s.Sql.Contains("SET QUOTED_IDENTIFIER ON", StringComparison.Ordinal))
            .Select(s => s.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "a filtered index fails under sqlcmd unless the script declares "
            + "SET QUOTED_IDENTIFIER ON, and these create one without it");
    }

    /// <summary>
    /// ANSI_NULLS travels with it. The two are set together everywhere in this folder, and a
    /// script that turned one on without the other would be a puzzle rather than a convention.
    /// </summary>
    [Fact]
    public void QuotedIdentifierAndAnsiNulls_AreDeclaredTogether()
    {
        var offenders = Scripts()
            .Where(s => s.Sql.Contains("SET QUOTED_IDENTIFIER ON", StringComparison.Ordinal)
                     != s.Sql.Contains("SET ANSI_NULLS ON", StringComparison.Ordinal))
            .Select(s => s.Name)
            .ToList();

        offenders.Should().BeEmpty("these declare one of the pair without the other");
    }

    /// <summary>
    /// Scripts run in filename order, and the runner in docker-compose.yml globs the folder. A
    /// gap means somebody renamed or deleted one, which would leave a database that stops partway
    /// through the schema with no error to show for it.
    /// </summary>
    [Fact]
    public void TheNumberedScripts_FormAnUnbrokenSequence()
    {
        var numbers = Scripts()
            .Select(s => s.Name[..3])
            .Where(prefix => prefix.All(char.IsDigit))
            .Select(int.Parse)
            .Where(n => n is > 0 and < 99)   // 000 is an index, 099 drops everything
            .OrderBy(n => n)
            .ToList();

        numbers.Should().NotBeEmpty();
        numbers.Should().OnlyHaveUniqueItems("two scripts with one number have no defined order");

        numbers.Should().BeEquivalentTo(
            Enumerable.Range(numbers[0], numbers.Count),
            options => options.WithStrictOrdering(),
            "a missing number means a script was renamed or removed");
    }

    /// <summary>
    /// Whether the script creates an index with a WHERE clause.
    ///
    /// <para>Deliberately crude: it looks for CREATE ... INDEX and a WHERE somewhere after it in
    /// the same statement. A false positive costs one unnecessary SET block; a false negative
    /// costs a broken deployment.</para>
    /// </summary>
    private static bool HasFilteredIndex(string sql)
    {
        var at = sql.IndexOf("CREATE ", StringComparison.OrdinalIgnoreCase);

        while (at >= 0)
        {
            // The statement runs to the next semicolon or GO, whichever comes first.
            var end = sql.IndexOf(';', at);
            var go = sql.IndexOf("\nGO", at, StringComparison.OrdinalIgnoreCase);

            end = end < 0 ? (go < 0 ? sql.Length : go)
                : go < 0 ? end
                : Math.Min(end, go);

            var statement = sql[at..end];

            if (statement.Contains("INDEX", StringComparison.OrdinalIgnoreCase)
                && statement.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            at = sql.IndexOf("CREATE ", end, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
