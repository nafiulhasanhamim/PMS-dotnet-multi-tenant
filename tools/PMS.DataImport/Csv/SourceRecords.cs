using CsvHelper.Configuration.Attributes;

namespace PMS.DataImport.Csv;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// The source CSVs, mapped by their ACTUAL headers.
//
// These were read off the files rather than taken from the dataset's documentation, which
// differs in several places. Notably medicine.csv carries its own "dosage form" column, which
// the documentation does not mention, so a medicine's dosage form is read directly instead of
// being inferred.
//
// Everything is a string here on purpose. Parsing and cleaning happen once, later, where the
// decisions are visible and testable — a CsvHelper type converter that quietly turns
// "Price Unavailable" into null would hide a data-quality fact worth reporting.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>medicine.csv — 21,714 rows, 10 columns.</summary>
public sealed class MedicineRecord
{
    [Name("brand id")] public string? BrandId { get; set; }

    [Name("brand name")] public string? BrandName { get; set; }

    /// <summary>"allopathic" (21,363) or "herbal" (351).</summary>
    [Name("type")] public string? Type { get; set; }

    [Name("slug")] public string? Slug { get; set; }

    /// <summary>Present in the file though absent from the dataset's documentation.</summary>
    [Name("dosage form")] public string? DosageForm { get; set; }

    [Name("generic")] public string? Generic { get; set; }

    [Name("strength")] public string? Strength { get; set; }

    [Name("manufacturer")] public string? Manufacturer { get; set; }

    /// <summary>
    /// Pack and price, e.g. "Unit Price: ৳ 5.98,(100's pack: ৳ 598.00),". Note the embedded
    /// commas inside a quoted field — the reason a real CSV parser is not optional here.
    /// </summary>
    [Name("package container")] public string? PackageContainer { get; set; }

    /// <summary>A second pack line, blank on 35.8% of rows.</summary>
    [Name("Package Size")] public string? PackageSize { get; set; }
}

/// <summary>generic.csv — 1,711 rows, 22 columns. Only the six we need are mapped.</summary>
public sealed class GenericRecord
{
    [Name("generic id")] public string? GenericId { get; set; }

    [Name("generic name")] public string? GenericName { get; set; }

    [Name("monograph link")] public string? MonographLink { get; set; }

    [Name("drug class")] public string? DrugClass { get; set; }

    /// <summary>Short indication, e.g. "Ulcerative colitis". Becomes IndicationSummary.</summary>
    [Name("indication")] public string? Indication { get; set; }
}

/// <summary>manufacturer.csv — 240 rows (the documentation says 245).</summary>
public sealed class ManufacturerRecord
{
    [Name("manufacturer name")] public string? ManufacturerName { get; set; }
}

/// <summary>drug class.csv — 453 rows, of which 421 are distinct once case is normalised.</summary>
public sealed class DrugClassRecord
{
    [Name("drug class name")] public string? DrugClassName { get; set; }
}

/// <summary>dosage form.csv — 113 rows (the documentation says ~120).</summary>
public sealed class DosageFormRecord
{
    [Name("dosage form name")] public string? DosageFormName { get; set; }
}
