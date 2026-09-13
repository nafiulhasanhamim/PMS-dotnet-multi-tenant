using System.Text;

namespace PMS.DataImport;

/// <summary>Counts for one table, kept so the final summary can be read at a glance.</summary>
public sealed class TableTally
{
    public TableTally(string table)
    {
        Table = table;
    }

    public string Table { get; }

    public int Read { get; set; }

    public int Inserted { get; set; }

    public int Updated { get; set; }

    /// <summary>Row was already correct, so nothing was written.</summary>
    public int Unchanged { get; set; }

    /// <summary>Duplicate of a row already handled in this same file.</summary>
    public int DeduplicatedInFile { get; set; }

    public int Rejected { get; set; }

    public override string ToString() =>
        $"{Table,-22} read {Read,6:N0}   inserted {Inserted,6:N0}   updated {Updated,6:N0}   "
        + $"unchanged {Unchanged,6:N0}   deduped {DeduplicatedInFile,4:N0}   rejected {Rejected,4:N0}";
}

/// <summary>A row the importer would not accept, written out for a human to look at.</summary>
public sealed record RejectedRow(string File, int LineNumber, string Reason, string Raw);

/// <summary>
/// Everything the run wants to tell you afterwards.
///
/// Rejected rows go to a file rather than the console because a run that quietly drops
/// twenty rows amongst 21,714 lines of progress output has dropped them silently in every
/// practical sense.
/// </summary>
public sealed class ImportReport
{
    private readonly List<RejectedRow> _rejected = [];

    public bool DryRun { get; init; }

    public DateTime StartedUtc { get; } = DateTime.UtcNow;

    public TableTally DrugClasses { get; } = new("CatalogDrugClasses");

    public TableTally DosageForms { get; } = new("CatalogDosageForms");

    public TableTally Manufacturers { get; } = new("CatalogManufacturers");

    public TableTally Generics { get; } = new("CatalogGenerics");

    public TableTally Medicines { get; } = new("CatalogMedicines");

    public IEnumerable<TableTally> Tallies =>
        [DrugClasses, DosageForms, Manufacturers, Generics, Medicines];

    public IReadOnlyList<RejectedRow> Rejected => _rejected;

    // ── Lookups created on the fly ──────────────────────────────────────────────────────
    //
    // Policy: when a medicine names a generic, manufacturer or dosage form that the lookup
    // files do not contain, the lookup row is CREATED rather than the FK left null. A null
    // would make the medicine unsearchable by that dimension, and losing "which manufacturer
    // is this" is worse than carrying a lookup row the source forgot to list.
    //
    // Inspection says this should never fire — all three resolve completely — so a non-zero
    // count here means the source changed and is worth reading as a warning.

    public int GenericsCreatedFromMedicines { get; set; }

    public int ManufacturersCreatedFromMedicines { get; set; }

    public int DosageFormsCreatedFromMedicines { get; set; }

    public int MedicinesWithNoGeneric { get; set; }

    public int MedicinesWithNoPrice { get; set; }

    // ── Antibiotic classification ───────────────────────────────────────────────────────

    public List<string> AntibioticClasses { get; } = [];

    public List<string> NonAntibioticClasses { get; } = [];

    public List<string> AntibioticByBothSignals { get; } = [];

    public List<string> AntibioticByClassOnly { get; } = [];

    public List<string> AntibioticByNameOnly { get; } = [];

    public int AntibioticGenerics { get; set; }

    public int AntibioticMedicines { get; set; }

    public void Reject(string file, int line, string reason, string raw) =>
        _rejected.Add(new RejectedRow(file, line, reason, raw.Length > 500 ? raw[..500] : raw));

    public string BuildSummary()
    {
        var sb = new StringBuilder();
        var elapsed = DateTime.UtcNow - StartedUtc;

        sb.AppendLine();
        sb.AppendLine(new string('=', 100));
        sb.AppendLine(DryRun
            ? "  DRY RUN SUMMARY - nothing was written to the database"
            : "  IMPORT SUMMARY");
        sb.AppendLine(new string('=', 100));
        sb.AppendLine();

        foreach (var tally in Tallies)
        {
            sb.AppendLine("  " + tally);
        }

        sb.AppendLine();
        sb.AppendLine($"  Elapsed: {elapsed.TotalSeconds:N1}s");
        sb.AppendLine();
        sb.AppendLine("  Data quality");
        sb.AppendLine($"    Medicines with no generic resolved : {MedicinesWithNoGeneric,6:N0}");
        sb.AppendLine($"    Medicines with no usable price     : {MedicinesWithNoPrice,6:N0}");
        sb.AppendLine($"    Lookups created from medicine rows : "
                      + $"generic {GenericsCreatedFromMedicines}, "
                      + $"manufacturer {ManufacturersCreatedFromMedicines}, "
                      + $"dosage form {DosageFormsCreatedFromMedicines}");
        sb.AppendLine($"    Rejected rows                      : {Rejected.Count,6:N0}"
                      + (Rejected.Count > 0 ? "   (see the rejects file)" : string.Empty));
        sb.AppendLine();
        sb.AppendLine("  Antibiotic classification  -- PROVISIONAL, REQUIRES HUMAN REVIEW");
        sb.AppendLine($"    Drug classes flagged antibacterial : {AntibioticClasses.Count,6:N0}"
                      + $" of {AntibioticClasses.Count + NonAntibioticClasses.Count:N0}");
        sb.AppendLine($"    Generics flagged antibiotic        : {AntibioticGenerics,6:N0}");
        sb.AppendLine($"      both signals agreed              : {AntibioticByBothSignals.Count,6:N0}");
        sb.AppendLine($"      drug class only  (review these)  : {AntibioticByClassOnly.Count,6:N0}");
        sb.AppendLine($"      generic name only (review these) : {AntibioticByNameOnly.Count,6:N0}");
        sb.AppendLine($"    Medicines inheriting the flag      : {AntibioticMedicines,6:N0}");
        sb.AppendLine();
        sb.AppendLine("    Sanity check: if the generic count is single digits or in the many");
        sb.AppendLine("    thousands, the classifier is wrong. Around 150-250 of 1,711 is the");
        sb.AppendLine("    expected order of magnitude for a Bangladeshi retail catalogue.");
        sb.AppendLine(new string('=', 100));

        return sb.ToString();
    }

    /// <summary>The antibiotic review file: what was flagged, what was not, and on what basis.</summary>
    public string BuildAntibioticReport()
    {
        var sb = new StringBuilder();

        sb.AppendLine("ANTIBIOTIC CLASSIFICATION REVIEW");
        sb.AppendLine($"Generated {StartedUtc:yyyy-MM-dd HH:mm:ss} UTC"
                      + (DryRun ? "  (dry run)" : string.Empty));
        sb.AppendLine();
        sb.AppendLine("This file is a WORKLIST, not a result. The source dataset has no antibiotic");
        sb.AppendLine("field, so each generic was judged on two inferred signals: the name of its drug");
        sb.AppendLine("class, and stems in its own name. Either firing sets the flag, because a false");
        sb.AppendLine("positive costs one unnecessary prescription capture while a false negative is a");
        sb.AppendLine("compliance failure.");
        sb.AppendLine();
        sb.AppendLine("Review the two single-signal sections first - those are where the disagreements");
        sb.AppendLine("are. Corrections should be written back with AntibioticSignal = 4");
        sb.AppendLine("(ManuallyReviewed), which later imports leave alone.");
        sb.AppendLine();

        void Section(string title, IEnumerable<string> items, string note = "")
        {
            var list = items.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            sb.AppendLine(new string('-', 100));
            sb.AppendLine($"{title}  ({list.Count})");
            if (note.Length > 0)
            {
                sb.AppendLine(note);
            }
            sb.AppendLine(new string('-', 100));
            foreach (var item in list)
            {
                sb.AppendLine("  " + item);
            }
            sb.AppendLine();
        }

        Section("DRUG CLASSES FLAGGED AS ANTIBACTERIAL", AntibioticClasses,
            "Every generic in these classes is flagged.");

        Section("GENERICS FLAGGED BY BOTH SIGNALS", AntibioticByBothSignals,
            "Class and name agree. Lowest risk of error.");

        Section("GENERICS FLAGGED BY DRUG CLASS ONLY - REVIEW", AntibioticByClassOnly,
            "The class reads antibacterial but the name shows no antibacterial stem. Often a\n"
            + "combination preparation, e.g. a steroid plus an antibiotic, or a class that names\n"
            + "an indication. Check each one is genuinely antibiotic-containing.");

        Section("GENERICS FLAGGED BY NAME ONLY - REVIEW", AntibioticByNameOnly,
            "The name shows an antibacterial stem but the class does not say so - either the\n"
            + "class is blank, or the source filed it under something unrelated. This section is\n"
            + "what class-based classification alone would have MISSED, so it is the important\n"
            + "one: it should contain Azithromycin (no class), Ciprofloxacin (filed under\n"
            + "anti-diarrhoeals), Metronidazole (amoebicides) and Isoniazid.");

        Section("DRUG CLASSES NOT FLAGGED", NonAntibioticClasses,
            "Scan for anything antibacterial that was missed. Antifungals, antivirals and\n"
            + "anthelmintics are deliberately here: an antibiotic is an antibacterial, and the\n"
            + "stewardship rules this flag serves are about those.");

        return sb.ToString();
    }

    public string BuildRejectsReport()
    {
        var sb = new StringBuilder();

        sb.AppendLine("REJECTED ROWS");
        sb.AppendLine($"Generated {StartedUtc:yyyy-MM-dd HH:mm:ss} UTC"
                      + (DryRun ? "  (dry run)" : string.Empty));
        sb.AppendLine();
        sb.AppendLine("Rows the importer would not accept. Each was skipped, not silently altered.");
        sb.AppendLine();

        if (_rejected.Count == 0)
        {
            sb.AppendLine("None. Every source row parsed and validated.");
            return sb.ToString();
        }

        foreach (var group in _rejected.GroupBy(r => r.File))
        {
            sb.AppendLine(new string('-', 100));
            sb.AppendLine($"{group.Key}  ({group.Count()} rejected)");
            sb.AppendLine(new string('-', 100));

            foreach (var reason in group.GroupBy(r => r.Reason))
            {
                sb.AppendLine($"  {reason.Key}  x{reason.Count()}");
            }

            sb.AppendLine();

            foreach (var row in group)
            {
                sb.AppendLine($"  line {row.LineNumber,-8} {row.Reason}");
                sb.AppendLine($"      {row.Raw}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
