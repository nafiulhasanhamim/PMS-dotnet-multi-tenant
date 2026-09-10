using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One thing a pharmacy sells. Tenant-scoped: each pharmacy has its own catalogue.
///
/// <para><b>Why one table and not separate Medicine and Product tables.</b> Everything
/// downstream — batches, FEFO, billing, reports — operates on integer quantities of a
/// product's base unit, and none of it needs to know whether that unit is a tablet, a bottle
/// or a tin. Splitting the table would duplicate all of that machinery, and every join in
/// the system would need to know which of two tables a line refers to. The medicine-specific
/// fields are nullable instead, and validation enforces that they are populated only when
/// <see cref="ProductType"/> is <see cref="ProductType.Medicine"/>.</para>
///
/// <para><b>Prices here are current defaults, not history.</b> The price actually charged is
/// snapshotted onto the sale line when a sale happens (Billing module), so changing a price
/// here never rewrites what a customer was charged last month.</para>
/// </summary>
public sealed class Product : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    // EF materialises through this; application code uses the factory below.
    private Product()
    {
    }

    /// <summary>
    /// Creates a product. Nothing here validates the type-conditional or unit rules — that is
    /// FluentValidation's job in the Application layer, where a failure becomes a field error
    /// on a form rather than an exception.
    /// </summary>
    public Product(
        ProductType productType,
        string brandName,
        string baseUnitName,
        decimal? pricePerBase)
    {
        Id = Guid.NewGuid();
        ProductType = productType;
        BrandName = brandName.Trim();
        BaseUnitName = baseUnitName.Trim();
        PricePerBase = pricePerBase;
        IsActive = true;

        RecomputeSetupComplete();
    }

    /// <summary>
    /// The owning pharmacy. Stamped by the persistence interceptor on insert; never set by
    /// a handler. See <see cref="ITenantEntity"/>.
    /// </summary>
    public Guid TenantId { get; set; }

    public ProductType ProductType { get; private set; }

    public string BrandName { get; private set; } = null!;

    /// <summary>The manufacturer or brand owner. Free text — not a foreign key.</summary>
    public string? Company { get; private set; }

    /// <summary>
    /// A secondary descriptor the pharmacy chooses: "Painkiller", "Feeding". Free text on
    /// purpose for now; a managed category table is out of scope.
    /// </summary>
    public string? Category { get; private set; }

    /// <summary>Soft delete. Inactive products stay out of default lists and new transactions.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// The platform catalogue row this product was imported from, or null when it was entered
    /// by hand.
    ///
    /// <para>A foreign key from tenant-scoped data to a platform-level table, which is
    /// deliberate and correct. The catalogue is shared reference data with no TenantId; two
    /// pharmacies importing Napa 500 produce two separate <see cref="Product"/> rows pointing
    /// at the same catalogue row. Nothing about this reference should tempt anyone into
    /// putting a query filter on the catalogue tables.</para>
    ///
    /// <para>It also distinguishes imported from hand-entered products, which is what a future
    /// "the reference data changed, review your product?" flow would need. Not built.</para>
    /// </summary>
    public int? CatalogMedicineId { get; private set; }

    // ── Medicine-specific. Null for every other product type. ───────────────────────────

    public string? GenericName { get; private set; }

    public string? Strength { get; private set; }

    public string? DosageForm { get; private set; }

    /// <summary>
    /// Whether this is a regulated antibiotic. Always false for non-medicines.
    ///
    /// Drives real behaviour downstream: employees cannot sell antibiotics, each sale captures
    /// a prescription, and the regulatory register is built from these. When a product is
    /// imported from the catalogue this is pre-set from the catalogue's own provisional flag,
    /// and a pharmacist confirms it — which is the point at which a machine-derived guess
    /// becomes a human decision.
    /// </summary>
    public bool IsAntibiotic { get; private set; }

    // ── Unit configuration ──────────────────────────────────────────────────────────────
    //
    // Configurable per product because "piece / strip / box" only makes sense for tablets.
    // A sanitiser is sold in bottles and cartons, formula in tins and cartons, saline in bags
    // and nothing else.

    /// <summary>The individual item a customer can buy. Always set.</summary>
    public string BaseUnitName { get; private set; } = null!;

    /// <summary>A middle pack, typically "strip". Null when the product has no middle level.</summary>
    public string? MidUnitName { get; private set; }

    /// <summary>A bulk pack: "box", "carton". Null when the product has no bulk level.</summary>
    public string? LargeUnitName { get; private set; }

    /// <summary>Base units in one mid unit. Set if and only if <see cref="MidUnitName"/> is.</summary>
    public int? BasePerMid { get; private set; }

    /// <summary>
    /// Units in one large unit — <b>and which unit depends on whether a mid level exists.</b>
    ///
    /// <para>With a mid level: mid units per large. 10 strips per box.</para>
    /// <para><b>Without a mid level: BASE units per large.</b> 24 bottles per carton.</para>
    ///
    /// <para>This is the single most likely place in the module to introduce an
    /// off-by-a-factor bug, which is why <see cref="BaseUnitsPerLarge"/> exists and why no
    /// caller should ever multiply these fields itself.</para>
    /// </summary>
    public int? MidPerLarge { get; private set; }

    // ── Pricing. Current defaults; the sale line snapshots what was charged. ────────────

    /// <summary>
    /// What one base unit sells for, or <b>null when nobody has priced it yet</b>.
    ///
    /// <para>Nullable because of bulk import: a pharmacy onboarding two hundred medicines can
    /// save them all and price them afterwards. The alternative was storing zero and treating
    /// it as "unpriced", which is the kind of sentinel that eventually sells something for
    /// nothing — zero is a real price, and a column that cannot tell the two apart will one
    /// day be asked to.</para>
    ///
    /// <para>An unpriced product is not sellable. That is what
    /// <see cref="IsSetupComplete"/> records, and Module 5 is where it will be enforced.</para>
    /// </summary>
    public decimal? PricePerBase { get; private set; }

    /// <summary>Required when <see cref="MidUnitName"/> is set, null otherwise.</summary>
    public decimal? PricePerMid { get; private set; }

    /// <summary>Required when <see cref="LargeUnitName"/> is set, null otherwise.</summary>
    public decimal? PricePerLarge { get; private set; }

    /// <summary>
    /// Whether every unit level this product defines has a price.
    ///
    /// <para><b>Stored, not derived on read.</b> It is a pure function of the price and unit
    /// columns, so a computed property would always agree — but the stock list, the medicines
    /// list, the incomplete-products banner and Module 5's sale path all need to filter and
    /// count on it, and none of those can put a C# expression in a WHERE clause. Storing it
    /// keeps the filter a single indexed predicate instead of loading every product to ask.</para>
    ///
    /// <para>The cost of storing it is that it can go stale, so nothing outside this entity
    /// may set it: <see cref="RecomputeSetupComplete"/> runs on construction and after every
    /// change to a price or a unit level. Adding a bulk pack to a product that has no bulk
    /// price makes it incomplete again, which is why the unit setter recomputes too.</para>
    /// </summary>
    public bool IsSetupComplete { get; private set; }

    // ── Inventory settings ──────────────────────────────────────────────────────────────

    /// <summary>Low-stock threshold, in base units.</summary>
    public int ReorderLevel { get; private set; } = 100;

    public string? ShelfLocation { get; private set; }

    // ── Derived ─────────────────────────────────────────────────────────────────────────

    public bool HasMidUnit => MidUnitName is not null;

    public bool HasLargeUnit => LargeUnitName is not null;

    /// <summary>
    /// How many base units one large unit contains, whichever shape the product has.
    ///
    /// <para>The one place the two-level edge case is resolved. With a mid level it is
    /// <c>BasePerMid × MidPerLarge</c> (10 pieces × 10 strips = 100 pieces per box). Without
    /// one, <see cref="MidPerLarge"/> already counts base units (24 bottles per carton) and
    /// must NOT be multiplied by anything.</para>
    /// </summary>
    public int? BaseUnitsPerLarge =>
        !HasLargeUnit || MidPerLarge is null
            ? null
            : HasMidUnit
                ? (BasePerMid ?? 1) * MidPerLarge.Value
                : MidPerLarge.Value;

    // ── Mutation ────────────────────────────────────────────────────────────────────────

    /// <summary>Applies an edit. Callers validate first; this trusts its input.</summary>
    public void Update(
        ProductType productType,
        string brandName,
        string? company,
        string? category,
        string? genericName,
        string? strength,
        string? dosageForm,
        bool isAntibiotic,
        string baseUnitName,
        string? midUnitName,
        string? largeUnitName,
        int? basePerMid,
        int? midPerLarge,
        decimal? pricePerBase,
        decimal? pricePerMid,
        decimal? pricePerLarge,
        int reorderLevel,
        string? shelfLocation)
    {
        ProductType = productType;
        BrandName = brandName.Trim();
        Company = Blank(company);
        Category = Blank(category);

        SetMedicineDetails(genericName, strength, dosageForm, isAntibiotic);
        SetUnits(baseUnitName, midUnitName, largeUnitName, basePerMid, midPerLarge);
        SetPrices(pricePerBase, pricePerMid, pricePerLarge);

        ReorderLevel = reorderLevel;
        ShelfLocation = Blank(shelfLocation);
    }

    /// <summary>
    /// Sets the medicine fields, and <b>forces them all null for a non-medicine</b> rather
    /// than trusting the caller. A diaper with a strength is exactly the corruption this
    /// module exists to prevent, so the entity refuses to hold one even if a validator is
    /// bypassed.
    /// </summary>
    public void SetMedicineDetails(
        string? genericName, string? strength, string? dosageForm, bool isAntibiotic)
    {
        if (ProductType != ProductType.Medicine)
        {
            GenericName = null;
            Strength = null;
            DosageForm = null;
            IsAntibiotic = false;
            return;
        }

        GenericName = Blank(genericName);
        Strength = Blank(strength);
        DosageForm = Blank(dosageForm);
        IsAntibiotic = isAntibiotic;
    }

    public void SetUnits(
        string baseUnitName,
        string? midUnitName,
        string? largeUnitName,
        int? basePerMid,
        int? midPerLarge)
    {
        BaseUnitName = baseUnitName.Trim();
        MidUnitName = Blank(midUnitName);
        LargeUnitName = Blank(largeUnitName);

        // A count without its level is meaningless, so drop it rather than store a number
        // nothing can interpret.
        BasePerMid = MidUnitName is null ? null : basePerMid;
        MidPerLarge = LargeUnitName is null ? null : midPerLarge;

        // Adding a level that has no price makes an otherwise complete product incomplete,
        // and removing one can complete it. Either way the flag has to follow.
        RecomputeSetupComplete();
    }

    public void SetPrices(decimal? pricePerBase, decimal? pricePerMid, decimal? pricePerLarge)
    {
        PricePerBase = pricePerBase;
        PricePerMid = MidUnitName is null ? null : pricePerMid;
        PricePerLarge = LargeUnitName is null ? null : pricePerLarge;

        RecomputeSetupComplete();
    }

    /// <summary>
    /// Recomputes <see cref="IsSetupComplete"/>. The only writer of that property.
    ///
    /// <para>A price is required for each level the product actually defines, and for no
    /// others — a product sold only in bags needs one price, not three. Note it asks whether
    /// the price is <em>present</em>, not whether it is positive: zero is a legitimate price
    /// for a sample or a giveaway, and treating it as unset is exactly the sentinel confusion
    /// nullable prices exist to avoid.</para>
    /// </summary>
    private void RecomputeSetupComplete() =>
        IsSetupComplete =
            PricePerBase is not null
            && (!HasMidUnit || PricePerMid is not null)
            && (!HasLargeUnit || PricePerLarge is not null);

    public void LinkToCatalog(int catalogMedicineId) => CatalogMedicineId = catalogMedicineId;

    public void SetCompany(string? company) => Company = Blank(company);

    public void SetCategory(string? category) => Category = Blank(category);

    public void SetInventory(int reorderLevel, string? shelfLocation)
    {
        ReorderLevel = reorderLevel;
        ShelfLocation = Blank(shelfLocation);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
