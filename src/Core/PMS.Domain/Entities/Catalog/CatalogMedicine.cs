namespace PMS.Domain.Entities.Catalog;

/// <summary>
/// One branded product, e.g. "Napa 500 mg tablet by Beximco".
///
/// Platform-level reference data: no TenantId, not an ITenantEntity. See CatalogLookups.cs.
/// A pharmacy searches these during onboarding and copies the ones it stocks into its own
/// tenant-scoped Medicine rows, where it sets its own price and pack configuration.
/// </summary>
public sealed class CatalogMedicine
{
    public int Id { get; set; }

    /// <summary>
    /// The source dataset's own identifier for this product, and the key the importer upserts
    /// on.
    ///
    /// <para>This column is not in the original specification and was added because the
    /// proposed natural key does not hold. Brand + strength + manufacturer collapses 532 of
    /// the 21,714 source rows into each other, and adding dosage form still collapses 66:
    /// "Glarine 100 IU/ml" by ACI genuinely exists as a cartridge, a vial and a biopen at
    /// different prices, and those are three products, not one row imported three times.
    /// Keying on the source id makes re-import exactly idempotent instead of
    /// approximately.</para>
    /// </summary>
    public int SourceBrandId { get; set; }

    public string BrandName { get; set; } = null!;

    /// <summary>
    /// Nullable by design, though in practice every source row resolves. Two of the 21,714
    /// rows have no generic at all.
    /// </summary>
    public int? GenericId { get; set; }

    public CatalogGeneric? Generic { get; set; }

    public int? ManufacturerId { get; set; }

    public CatalogManufacturer? Manufacturer { get; set; }

    public int? DosageFormId { get; set; }

    public CatalogDosageForm? DosageForm { get; set; }

    /// <summary>e.g. "500 mg", "(10 mg+30 mg+1.25 mg)/5 ml". Blank on 849 source rows.</summary>
    public string? Strength { get; set; }

    /// <summary>"allopathic" or "herbal" in this dataset. Kept as found.</summary>
    public string? MedicineType { get; set; }

    /// <summary>
    /// The raw pack text, kept verbatim for reference — e.g.
    /// "Unit Price: ৳ 5.98,(100's pack: ৳ 598.00),".
    ///
    /// Deliberately not parsed into structured pack fields. The source has no reliable
    /// pieces-per-strip or strips-per-box, and inventing one here would give every tenant a
    /// pack configuration that looks authoritative and is guesswork. Tenants set their own.
    /// </summary>
    public string? PackageInfo { get; set; }

    /// <summary>
    /// The unit price scraped from the source, in BDT.
    ///
    /// <para><b>Reference only. This must never reach billing, margin or profit
    /// calculations.</b> It is a point-in-time scrape of a published price, already stale, and
    /// a pharmacy's actual selling price is its own. It exists so that a pharmacy browsing the
    /// catalog can recognise a product by roughly the price they expect.</para>
    /// </summary>
    public decimal? SourceUnitPrice { get; set; }

    /// <summary>
    /// Lets a later catalog refresh retire a discontinued product without deleting it — rows
    /// a tenant has already imported from must stay resolvable.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime ImportedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }
}
