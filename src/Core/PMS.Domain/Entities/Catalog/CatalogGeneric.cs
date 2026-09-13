namespace PMS.Domain.Entities.Catalog;

/// <summary>
/// An active ingredient, e.g. "Amoxicillin Trihydrate" or "Paracetamol + Caffeine".
///
/// Platform-level reference data: no TenantId, not an ITenantEntity. See CatalogLookups.cs.
/// </summary>
public sealed class CatalogGeneric
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// Nullable because 61 of the 1,711 source generics have no drug class at all — and one of
    /// them is Azithromycin, which is exactly why the antibiotic flag cannot be derived from
    /// the class alone.
    /// </summary>
    public int? DrugClassId { get; set; }

    public CatalogDrugClass? DrugClass { get; set; }

    /// <summary>Link to the source's monograph PDF. Absent for 512 of 1,711 generics.</summary>
    public string? MonographUrl { get; set; }

    /// <summary>
    /// The source's short indication text, e.g. "Ulcerative colitis". A summary for browsing,
    /// not clinical guidance, and never to be shown as advice.
    /// </summary>
    public string? IndicationSummary { get; set; }

    /// <summary>
    /// Whether this generic is an antibacterial, for the regulatory module: employees are
    /// blocked from selling antibiotics, each sale must capture a prescription, and the
    /// antibiotic register is built from these.
    ///
    /// <para><b>This value requires human verification before it is relied on.</b> The source
    /// dataset has no antibiotic field, so the importer derives it from two weak signals — the
    /// drug class name and the generic's own name — and flags whichever it cannot corroborate.
    /// Every import writes a review report listing what was flagged, what was not, and where
    /// the two signals disagreed. Treat the report as a worklist, not a result.</para>
    ///
    /// <para>Why two signals: class alone misses Ciprofloxacin (filed under
    /// "Anti-diarrhoeal Antimicrobial drugs"), Metronidazole ("Amoebicides"), Isoniazid
    /// ("Anti-Tubercular Chemotherapeutics") and Azithromycin (no class at all). Name alone
    /// misses anything whose stem is unfamiliar. Either firing is enough to flag, because for
    /// this particular flag a false positive costs a pharmacist one extra prescription
    /// capture, while a false negative is a compliance failure.</para>
    /// </summary>
    public bool IsAntibiotic { get; set; }

    /// <summary>
    /// How <see cref="IsAntibiotic"/> was arrived at, so the review report can explain itself
    /// and so a later import can tell a derived value from a corrected one.
    /// </summary>
    public AntibioticSignal AntibioticSignal { get; set; }
}

/// <summary>Which evidence set the antibiotic flag for a generic.</summary>
public enum AntibioticSignal
{
    /// <summary>Neither signal fired — treated as not an antibiotic.</summary>
    None = 0,

    /// <summary>Both the drug class and the generic name indicated an antibacterial.</summary>
    ClassAndName = 1,

    /// <summary>Only the drug class matched. Worth review: the class may name an indication.</summary>
    ClassOnly = 2,

    /// <summary>
    /// Only the generic's name matched — the class was blank, or named something else. This is
    /// the group that catches the source's misfiled and unclassified antibiotics, and the group
    /// most in need of a human eye.
    /// </summary>
    NameOnly = 3,

    /// <summary>Set by a human, and left alone by subsequent imports.</summary>
    ManuallyReviewed = 4,
}
