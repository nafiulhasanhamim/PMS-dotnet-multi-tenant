namespace PMS.Domain.Entities.Catalog;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// The medicine reference catalog.
//
// READ THIS BEFORE ADDING ANYTHING HERE.
//
// None of these types implement ITenantEntity, none carry a TenantId, and none are touched by
// the global query filters. That is deliberate and it is the whole point of the catalog: it is
// one shared, platform-owned list of every medicine sold in Bangladesh, so that a pharmacy
// signing up can search it and import entries rather than typing several hundred medicines by
// hand before the system is usable at all.
//
// They sit outside tenant isolation in the same way the Tenant table itself does. The
// consequence to hold on to: a query against these tables returns the same rows for every
// pharmacy, and for no pharmacy at all. There is nothing here to leak between tenants because
// nothing here belongs to a tenant.
//
// The counterpart is that **tenant users never write to these tables.** Only platform-level
// processes do — today that means the importer in tools/PMS.DataImport, and later whatever
// refreshes the catalog. A tenant's own catalog, with its own prices and pack configuration,
// is the tenant-scoped Medicine entity, which is a separate thing that copies FROM here.
//
// See docs/data/medicine-reference-catalog.md.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A pharmaceutical company, as named by the source dataset.</summary>
public sealed class CatalogManufacturer
{
    public int Id { get; set; }

    /// <summary>Display casing as first seen; uniqueness is enforced case-insensitively.</summary>
    public string Name { get; set; } = null!;
}

/// <summary>
/// A therapeutic class, e.g. "Broad spectrum penicillins".
///
/// <see cref="IsAntibioticClass"/> is what the antibiotic flag on a generic is partly derived
/// from. It is stored rather than recomputed so that a human correction survives the next
/// import — see the notes on <see cref="CatalogGeneric.IsAntibiotic"/>.
/// </summary>
public sealed class CatalogDrugClass
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// Whether this class was classified as antibacterial during import.
    ///
    /// Derived from keywords, and **not authoritative** — the source's classes are imprecise
    /// (it files both Linezolid and Clindamycin under "Macrolides") and some antibiotics carry
    /// a class that names the indication rather than the drug family
    /// ("Anti-diarrhoeal Antimicrobial drugs" holds Ciprofloxacin).
    /// </summary>
    public bool IsAntibioticClass { get; set; }
}

/// <summary>How a medicine is presented — tablet, syrup, IV infusion, and so on.</summary>
public sealed class CatalogDosageForm
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
