using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using PMS.DataImport.Csv;
using PMS.Domain.Entities.Catalog;
using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace PMS.DataImport;

/// <summary>
/// Loads the six source CSVs into the five catalog tables.
///
/// <para><b>Order is not optional</b>: drug classes, dosage forms and manufacturers first
/// (nothing depends on them), then generics (which reference a drug class), then medicines
/// (which reference all three). Running them in any other order means writing a foreign key
/// to a row that does not exist yet.</para>
///
/// <para><b>Idempotency</b> comes from upserting on natural keys — the case-folded name for
/// each lookup, and the source's own product id for a medicine. A second run reports updates
/// and unchanged rows; it cannot insert a duplicate, and the unique indexes would refuse one
/// if the logic ever let it try.</para>
///
/// <para><b>Batching</b>: rows are staged in memory, matched against a single pre-loaded
/// dictionary of what already exists, then written with one SaveChanges per chunk. Calling
/// SaveChanges per row would be roughly 21,000 round trips for the medicines alone.</para>
/// </summary>
public sealed class CatalogImporter
{
    private const int BatchSize = 2000;

    private readonly ApplicationDbContext _db;
    private readonly string _sourceFolder;
    private readonly ImportReport _report;
    private readonly bool _dryRun;
    private readonly Action<string> _log;

    // Resolved lookups, keyed by the case-folded name, so a medicine row can find its foreign
    // keys without touching the database.
    private readonly Dictionary<string, CatalogDrugClass> _drugClasses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogDosageForm> _dosageForms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogManufacturer> _manufacturers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogGeneric> _generics = new(StringComparer.Ordinal);

    public CatalogImporter(
        ApplicationDbContext db,
        string sourceFolder,
        ImportReport report,
        bool dryRun,
        Action<string> log)
    {
        _db = db;
        _sourceFolder = sourceFolder;
        _report = report;
        _dryRun = dryRun;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await ImportDrugClassesAsync(ct);
        await ImportDosageFormsAsync(ct);
        await ImportManufacturersAsync(ct);
        await ImportGenericsAsync(ct);
        await ImportMedicinesAsync(ct);
    }

    // ── CSV plumbing ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a CSV as UTF-8.
    ///
    /// UTF-8 explicitly, not the machine default: the price fields carry the Bengali taka sign
    /// and manufacturer names carry accented characters. Reading these as Windows-1252 turns
    /// them into mojibake that then gets written into nvarchar columns looking like real data.
    ///
    /// <c>BadDataFound = null</c> and <c>MissingFieldFound = null</c> keep a single malformed
    /// row from aborting the run; the row is validated afterwards and rejected with a reason
    /// if it is unusable.
    /// </summary>
    private IEnumerable<(T Record, int Line, string Raw)> ReadCsv<T>(string fileName)
    {
        var path = Path.Combine(_sourceFolder, fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Source file not found: {path}", path);
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            DetectDelimiter = false,
        };

        using var reader = new StreamReader(path, System.Text.Encoding.UTF8);
        using var csv = new CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var line = csv.Context.Parser?.Row ?? 0;
            var raw = csv.Context.Parser?.RawRecord?.Trim() ?? string.Empty;

            T record;
            try
            {
                record = csv.GetRecord<T>();
            }
            catch (CsvHelperException ex)
            {
                _report.Reject(fileName, line, $"Unparseable row: {ex.GetType().Name}", raw);
                continue;
            }

            yield return (record, line, raw);
        }
    }

    // ── Lookup tables ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Upserts a lookup table from a file, deduplicating case-insensitively.
    ///
    /// The first spelling encountered wins for display, so the catalogue reads naturally
    /// ("Beximco", not "beximco") while the dedupe stays exact.
    /// </summary>
    private async Task ImportLookupAsync<TRecord, TEntity>(
        string fileName,
        string label,
        TableTally tally,
        Func<TRecord, string?> nameSelector,
        DbSet<TEntity> set,
        Dictionary<string, TEntity> cache,
        Func<TEntity, string> entityName,
        Func<string, TEntity> create,
        Action<TEntity, string>? update,
        CancellationToken ct)
        where TEntity : class
    {
        _log($"  {label}: reading {fileName}...");

        foreach (var existing in await set.AsTracking().ToListAsync(ct))
        {
            var key = Cleaning.Key(entityName(existing));
            if (key is not null)
            {
                cache[key] = existing;
            }
        }

        var seenInFile = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (record, line, raw) in ReadCsv<TRecord>(fileName))
        {
            tally.Read++;

            var display = Cleaning.Fit(nameSelector(record), 300);
            var key = Cleaning.Key(display);

            if (key is null)
            {
                tally.Rejected++;
                _report.Reject(fileName, line, "Name is empty", raw);
                continue;
            }

            if (!seenInFile.Add(key))
            {
                // A case-only duplicate within the same file — drug class.csv has 20 of these,
                // e.g. "Proton Pump Inhibitor" five times over.
                tally.DeduplicatedInFile++;
                continue;
            }

            if (cache.TryGetValue(key, out var entity))
            {
                var before = entityName(entity);
                update?.Invoke(entity, display!);

                if (!_dryRun && _db.Entry(entity).State == EntityState.Modified)
                {
                    tally.Updated++;
                }
                else if (update is not null && before != entityName(entity))
                {
                    tally.Updated++;
                }
                else
                {
                    tally.Unchanged++;
                }

                continue;
            }

            var created = create(display!);
            cache[key] = created;

            if (!_dryRun)
            {
                set.Add(created);
            }

            tally.Inserted++;
        }

        if (!_dryRun)
        {
            await _db.SaveChangesAsync(ct);
        }

        _log($"  {label}: {tally}");
    }

    private Task ImportDrugClassesAsync(CancellationToken ct) =>
        ImportLookupAsync<DrugClassRecord, CatalogDrugClass>(
            "drug class.csv", "Drug classes", _report.DrugClasses,
            r => r.DrugClassName,
            _db.CatalogDrugClasses, _drugClasses,
            e => e.Name,
            name =>
            {
                var isAntibiotic = AntibioticClassifier.ClassIndicatesAntibiotic(name);
                (isAntibiotic ? _report.AntibioticClasses : _report.NonAntibioticClasses).Add(name);

                return new CatalogDrugClass { Name = name, IsAntibioticClass = isAntibiotic };
            },
            (entity, name) =>
            {
                // Re-derive on every run so a widened keyword list takes effect, and record
                // the class in the report either way.
                var isAntibiotic = AntibioticClassifier.ClassIndicatesAntibiotic(name);
                (isAntibiotic ? _report.AntibioticClasses : _report.NonAntibioticClasses)
                    .Add(entity.Name);

                if (entity.IsAntibioticClass != isAntibiotic)
                {
                    entity.IsAntibioticClass = isAntibiotic;
                }
            },
            ct);

    private Task ImportDosageFormsAsync(CancellationToken ct) =>
        ImportLookupAsync<DosageFormRecord, CatalogDosageForm>(
            "dosage form.csv", "Dosage forms", _report.DosageForms,
            r => r.DosageFormName,
            _db.CatalogDosageForms, _dosageForms,
            e => e.Name,
            name => new CatalogDosageForm { Name = name },
            update: null,
            ct);

    private Task ImportManufacturersAsync(CancellationToken ct) =>
        ImportLookupAsync<ManufacturerRecord, CatalogManufacturer>(
            "manufacturer.csv", "Manufacturers", _report.Manufacturers,
            r => r.ManufacturerName,
            _db.CatalogManufacturers, _manufacturers,
            e => e.Name,
            name => new CatalogManufacturer { Name = name },
            update: null,
            ct);

    // ── Generics ────────────────────────────────────────────────────────────────────────

    private async Task ImportGenericsAsync(CancellationToken ct)
    {
        const string fileName = "generic.csv";
        var tally = _report.Generics;

        _log($"  Generics: reading {fileName}...");

        foreach (var existing in await _db.CatalogGenerics.AsTracking().ToListAsync(ct))
        {
            var key = Cleaning.Key(existing.Name);
            if (key is not null)
            {
                _generics[key] = existing;
            }
        }

        var seenInFile = new HashSet<string>(StringComparer.Ordinal);
        var pending = 0;

        foreach (var (record, line, raw) in ReadCsv<GenericRecord>(fileName))
        {
            tally.Read++;

            var name = Cleaning.Fit(record.GenericName, 500);
            var key = Cleaning.Key(name);

            if (key is null)
            {
                tally.Rejected++;
                _report.Reject(fileName, line, "Generic name is empty", raw);
                continue;
            }

            if (!seenInFile.Add(key))
            {
                tally.DeduplicatedInFile++;
                continue;
            }

            var className = Cleaning.Text(record.DrugClass);
            var classKey = Cleaning.Key(className);
            CatalogDrugClass? drugClass = null;

            if (classKey is not null && !_drugClasses.TryGetValue(classKey, out drugClass))
            {
                // Not seen in drug class.csv. Create it rather than dropping the association:
                // losing which class a generic belongs to also loses the class signal for the
                // antibiotic flag, which is the one place that matters most.
                drugClass = new CatalogDrugClass
                {
                    Name = className!,
                    IsAntibioticClass = AntibioticClassifier.ClassIndicatesAntibiotic(className),
                };

                _drugClasses[classKey] = drugClass;

                if (!_dryRun)
                {
                    _db.CatalogDrugClasses.Add(drugClass);
                }

                _report.DrugClasses.Inserted++;
            }

            var verdict = AntibioticClassifier.Classify(name, className);

            // Record the reasoning for the review file.
            if (verdict.IsAntibiotic)
            {
                var label = $"{name}   [class: {className ?? "(none)"}]";
                switch (verdict.Signal)
                {
                    case AntibioticSignal.ClassAndName: _report.AntibioticByBothSignals.Add(label); break;
                    case AntibioticSignal.ClassOnly: _report.AntibioticByClassOnly.Add(label); break;
                    case AntibioticSignal.NameOnly: _report.AntibioticByNameOnly.Add(label); break;
                }
            }

            if (_generics.TryGetValue(key, out var entity))
            {
                var changed = false;

                if (entity.Name != name) { entity.Name = name!; changed = true; }
                if (entity.MonographUrl != Cleaning.Fit(record.MonographLink, 1000))
                {
                    entity.MonographUrl = Cleaning.Fit(record.MonographLink, 1000);
                    changed = true;
                }
                if (entity.IndicationSummary != Cleaning.Text(record.Indication))
                {
                    entity.IndicationSummary = Cleaning.Text(record.Indication);
                    changed = true;
                }
                if (drugClass is not null && entity.DrugClass != drugClass
                    && entity.DrugClassId != drugClass.Id)
                {
                    entity.DrugClass = drugClass;
                    changed = true;
                }

                // A human decision is never overwritten by a re-derived guess.
                if (entity.AntibioticSignal != AntibioticSignal.ManuallyReviewed
                    && (entity.IsAntibiotic != verdict.IsAntibiotic
                        || entity.AntibioticSignal != verdict.Signal))
                {
                    entity.IsAntibiotic = verdict.IsAntibiotic;
                    entity.AntibioticSignal = verdict.Signal;
                    changed = true;
                }

                if (changed) { tally.Updated++; } else { tally.Unchanged++; }
            }
            else
            {
                var created = new CatalogGeneric
                {
                    Name = name!,
                    DrugClass = drugClass,
                    MonographUrl = Cleaning.Fit(record.MonographLink, 1000),
                    IndicationSummary = Cleaning.Text(record.Indication),
                    IsAntibiotic = verdict.IsAntibiotic,
                    AntibioticSignal = verdict.Signal,
                };

                _generics[key] = created;

                if (!_dryRun)
                {
                    _db.CatalogGenerics.Add(created);
                }

                tally.Inserted++;
            }

            if (++pending >= BatchSize && !_dryRun)
            {
                await _db.SaveChangesAsync(ct);
                pending = 0;
            }
        }

        if (!_dryRun)
        {
            await _db.SaveChangesAsync(ct);
        }

        _report.AntibioticGenerics = _generics.Values.Count(g => g.IsAntibiotic);

        _log($"  Generics: {tally}");
    }

    // ── Medicines ───────────────────────────────────────────────────────────────────────

    private async Task ImportMedicinesAsync(CancellationToken ct)
    {
        const string fileName = "medicine.csv";
        var tally = _report.Medicines;

        _log($"  Medicines: reading {fileName}...");

        // Keyed on the source's product id. See CatalogMedicine.SourceBrandId for why the
        // brand+strength+manufacturer key in the specification could not be used.
        var existing = await _db.CatalogMedicines
            .AsTracking()
            .ToDictionaryAsync(m => m.SourceBrandId, ct);

        var seenInFile = new HashSet<int>();
        var now = DateTime.UtcNow;
        var pending = 0;
        var antibioticGenericIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kvp in _generics.Where(g => g.Value.IsAntibiotic))
        {
            antibioticGenericIds.Add(kvp.Key);
        }

        foreach (var (record, line, raw) in ReadCsv<MedicineRecord>(fileName))
        {
            tally.Read++;

            var sourceId = Cleaning.SourceId(record.BrandId);
            var brandName = Cleaning.Fit(record.BrandName, 300);

            if (sourceId is null)
            {
                tally.Rejected++;
                _report.Reject(fileName, line, "Missing or non-numeric brand id", raw);
                continue;
            }

            if (brandName is null)
            {
                tally.Rejected++;
                _report.Reject(fileName, line, "Brand name is empty", raw);
                continue;
            }

            if (!seenInFile.Add(sourceId.Value))
            {
                tally.DeduplicatedInFile++;
                _report.Reject(fileName, line,
                    $"Duplicate source brand id {sourceId} within the file", raw);
                continue;
            }

            var genericKey = Cleaning.Key(record.Generic);
            var manufacturerKey = Cleaning.Key(record.Manufacturer);
            var dosageFormKey = Cleaning.Key(record.DosageForm);

            var generic = ResolveGeneric(genericKey, record.Generic);
            var manufacturer = ResolveManufacturer(manufacturerKey, record.Manufacturer);
            var dosageForm = ResolveDosageForm(dosageFormKey, record.DosageForm);

            if (generic is null)
            {
                _report.MedicinesWithNoGeneric++;
            }

            var price = Cleaning.UnitPrice(record.PackageContainer);

            if (price is null)
            {
                _report.MedicinesWithNoPrice++;
            }

            if (generic?.IsAntibiotic == true)
            {
                _report.AntibioticMedicines++;
            }

            var strength = Cleaning.Fit(record.Strength, 300);
            var medicineType = Cleaning.Fit(record.Type, 50);
            var packageInfo = Cleaning.Fit(record.PackageContainer, 1000);

            if (existing.TryGetValue(sourceId.Value, out var entity))
            {
                var changed = false;

                if (entity.BrandName != brandName) { entity.BrandName = brandName; changed = true; }
                if (entity.Strength != strength) { entity.Strength = strength; changed = true; }
                if (entity.MedicineType != medicineType) { entity.MedicineType = medicineType; changed = true; }
                if (entity.PackageInfo != packageInfo) { entity.PackageInfo = packageInfo; changed = true; }
                if (entity.SourceUnitPrice != price) { entity.SourceUnitPrice = price; changed = true; }

                if (generic is not null && entity.GenericId != generic.Id)
                {
                    entity.Generic = generic; changed = true;
                }
                if (manufacturer is not null && entity.ManufacturerId != manufacturer.Id)
                {
                    entity.Manufacturer = manufacturer; changed = true;
                }
                if (dosageForm is not null && entity.DosageFormId != dosageForm.Id)
                {
                    entity.DosageForm = dosageForm; changed = true;
                }

                if (changed)
                {
                    entity.LastUpdatedAt = now;
                    tally.Updated++;
                }
                else
                {
                    tally.Unchanged++;
                }
            }
            else
            {
                var created = new CatalogMedicine
                {
                    SourceBrandId = sourceId.Value,
                    BrandName = brandName,
                    Generic = generic,
                    Manufacturer = manufacturer,
                    DosageForm = dosageForm,
                    Strength = strength,
                    MedicineType = medicineType,
                    PackageInfo = packageInfo,
                    SourceUnitPrice = price,
                    IsActive = true,
                    ImportedAt = now,
                    LastUpdatedAt = now,
                };

                if (!_dryRun)
                {
                    _db.CatalogMedicines.Add(created);
                }

                tally.Inserted++;
            }

            if (++pending >= BatchSize)
            {
                if (!_dryRun)
                {
                    await _db.SaveChangesAsync(ct);
                }

                _log($"    {tally.Read,6:N0} rows processed"
                     + $"   (+{tally.Inserted:N0} new, ~{tally.Updated:N0} updated)");
                pending = 0;
            }
        }

        if (!_dryRun)
        {
            await _db.SaveChangesAsync(ct);
        }

        _log($"  Medicines: {tally}");
    }

    // ── On-the-fly lookup creation ──────────────────────────────────────────────────────
    //
    // Policy, applied consistently: create the lookup row rather than leaving the FK null.
    // A null makes the medicine unfindable by that dimension, and "we do not know the
    // manufacturer" is worse than an extra lookup row the source forgot to list. Every
    // creation is counted so the summary shows when the source has drifted.

    private CatalogGeneric? ResolveGeneric(string? key, string? display)
    {
        if (key is null)
        {
            return null;
        }

        if (_generics.TryGetValue(key, out var found))
        {
            return found;
        }

        var created = new CatalogGeneric
        {
            Name = Cleaning.Fit(display, 500)!,
            IsAntibiotic = AntibioticClassifier.NameIndicatesAntibiotic(display),
            AntibioticSignal = AntibioticClassifier.NameIndicatesAntibiotic(display)
                ? AntibioticSignal.NameOnly
                : AntibioticSignal.None,
        };

        _generics[key] = created;

        if (!_dryRun)
        {
            _db.CatalogGenerics.Add(created);
        }

        _report.GenericsCreatedFromMedicines++;
        _report.Generics.Inserted++;

        return created;
    }

    private CatalogManufacturer? ResolveManufacturer(string? key, string? display)
    {
        if (key is null)
        {
            return null;
        }

        if (_manufacturers.TryGetValue(key, out var found))
        {
            return found;
        }

        var created = new CatalogManufacturer { Name = Cleaning.Fit(display, 300)! };
        _manufacturers[key] = created;

        if (!_dryRun)
        {
            _db.CatalogManufacturers.Add(created);
        }

        _report.ManufacturersCreatedFromMedicines++;
        _report.Manufacturers.Inserted++;

        return created;
    }

    private CatalogDosageForm? ResolveDosageForm(string? key, string? display)
    {
        if (key is null)
        {
            return null;
        }

        if (_dosageForms.TryGetValue(key, out var found))
        {
            return found;
        }

        var created = new CatalogDosageForm { Name = Cleaning.Fit(display, 200)! };
        _dosageForms[key] = created;

        if (!_dryRun)
        {
            _db.CatalogDosageForms.Add(created);
        }

        _report.DosageFormsCreatedFromMedicines++;
        _report.DosageForms.Inserted++;

        return created;
    }
}
