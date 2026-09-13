using PMS.Web.Api;

namespace PMS.Web.Pages.Products;

/// <summary>
/// Every field the product form binds, shared by create (medicine), create (other item) and
/// edit. One shape so the markup can be one partial; which fields are rendered is decided by
/// <see cref="IsMedicine"/>.
/// </summary>
public sealed class ProductFormInput
{
    public ProductType ProductType { get; set; } = ProductType.Medicine;

    public string BrandName { get; set; } = string.Empty;

    public string? Company { get; set; }

    public string? Category { get; set; }

    // ── Medicine only. Not rendered at all for other items, so they stay null. ──────────

    public string? GenericName { get; set; }

    public string? Strength { get; set; }

    public string? DosageForm { get; set; }

    public bool IsAntibiotic { get; set; }

    // ── Unit setup ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Which preset the person chose. Purely a UI convenience — it decides which inputs are
    /// enabled, and the posted unit fields are what actually count.
    /// </summary>
    public UnitPreset Preset { get; set; } = UnitPreset.Custom;

    public string BaseUnitName { get; set; } = string.Empty;

    /// <summary>
    /// Ticked when the product has a middle pack. An explicit checkbox rather than "leave the
    /// name blank", so that skipping a level reads as a decision instead of a forgotten field.
    /// </summary>
    public bool HasMidUnit { get; set; }

    public string? MidUnitName { get; set; }

    public int? BasePerMid { get; set; }

    public bool HasLargeUnit { get; set; }

    public string? LargeUnitName { get; set; }

    /// <summary>
    /// Units per bulk pack — <b>mid units when a middle level exists, base units when it does
    /// not.</b> The form's question text changes to say which, because the number a person
    /// types depends entirely on that and nobody reads a tooltip.
    /// </summary>
    public int? MidPerLarge { get; set; }

    // ── Pricing and inventory ───────────────────────────────────────────────────────────

    /// <summary>
    /// Nullable so an unpriced product can be loaded into this form to be completed. The
    /// single-product path still requires it — see ProductWritePageModel.
    /// </summary>
    public decimal? PricePerBase { get; set; }

    public decimal? PricePerMid { get; set; }

    public decimal? PricePerLarge { get; set; }

    /// <summary>
    /// Where the low-stock alert fires. <b>No initialiser since Module 10</b> - a new product's
    /// starting value comes from the pharmacy's <c>default_reorder_level</c> setting, applied by
    /// the factories below, and an existing product's comes from the product.
    /// </summary>
    public int ReorderLevel { get; set; }

    public string? ShelfLocation { get; set; }

    /// <summary>Set only on the import path, and never editable afterwards.</summary>
    public int? CatalogMedicineId { get; set; }

    public bool IsMedicine => ProductType == ProductType.Medicine;

    /// <summary>
    /// Normalises the form into what the API expects: a level unticked means its name and
    /// count are null, whatever is still sitting in the disabled inputs.
    ///
    /// <para>This matters because a browser posts disabled-then-enabled fields inconsistently,
    /// and a stale "strip / 10" left behind by someone who changed their mind would otherwise
    /// be sent as real configuration.</para>
    /// </summary>
    public void Normalise()
    {
        BrandName = BrandName?.Trim() ?? string.Empty;
        BaseUnitName = BaseUnitName?.Trim() ?? string.Empty;
        Company = Blank(Company);
        Category = Blank(Category);
        ShelfLocation = Blank(ShelfLocation);

        if (!IsMedicine)
        {
            // The corruption this module exists to prevent. Cleared here as well as rejected
            // by the API, so a type change on the form cannot leave a stale strength behind.
            GenericName = null;
            Strength = null;
            DosageForm = null;
            IsAntibiotic = false;
        }
        else
        {
            GenericName = Blank(GenericName);
            Strength = Blank(Strength);
            DosageForm = Blank(DosageForm);
        }

        if (!HasMidUnit || string.IsNullOrWhiteSpace(MidUnitName))
        {
            HasMidUnit = false;
            MidUnitName = null;
            BasePerMid = null;
            PricePerMid = null;
        }
        else
        {
            MidUnitName = MidUnitName.Trim();
        }

        if (!HasLargeUnit || string.IsNullOrWhiteSpace(LargeUnitName))
        {
            HasLargeUnit = false;
            LargeUnitName = null;
            MidPerLarge = null;
            PricePerLarge = null;
        }
        else
        {
            LargeUnitName = LargeUnitName.Trim();
        }
    }

    public CreateProductRequest ToCreateRequest() => new(
        ProductType, BrandName, Company, Category,
        GenericName, Strength, DosageForm, IsAntibiotic,
        BaseUnitName, MidUnitName, LargeUnitName, BasePerMid, MidPerLarge,
        PricePerBase, PricePerMid, PricePerLarge, ReorderLevel, ShelfLocation,
        CatalogMedicineId);

    public UpdateProductRequest ToUpdateRequest() => new(
        ProductType, BrandName, Company, Category,
        GenericName, Strength, DosageForm, IsAntibiotic,
        BaseUnitName, MidUnitName, LargeUnitName, BasePerMid, MidPerLarge,
        PricePerBase, PricePerMid, PricePerLarge, ReorderLevel, ShelfLocation);

    /// <summary>Fills the form from an existing product, for editing.</summary>
    public static ProductFormInput FromProduct(ProductModel product) => new()
    {
        ProductType = product.ProductType,
        BrandName = product.BrandName,
        Company = product.Company,
        Category = product.Category,
        GenericName = product.GenericName,
        Strength = product.Strength,
        DosageForm = product.DosageForm,
        IsAntibiotic = product.IsAntibiotic,
        Preset = UnitPreset.Custom,
        BaseUnitName = product.BaseUnitName,
        HasMidUnit = product.MidUnitName is not null,
        MidUnitName = product.MidUnitName,
        BasePerMid = product.BasePerMid,
        HasLargeUnit = product.LargeUnitName is not null,
        LargeUnitName = product.LargeUnitName,
        MidPerLarge = product.MidPerLarge,
        PricePerBase = product.PricePerBase,
        PricePerMid = product.PricePerMid,
        PricePerLarge = product.PricePerLarge,
        ReorderLevel = product.ReorderLevel,
        ShelfLocation = product.ShelfLocation,
        CatalogMedicineId = product.CatalogMedicineId,
    };

    /// <summary>Pre-fills from a catalogue entry, for the import review step.</summary>
    /// <param name="defaultReorderLevel">From settings; see <see cref="NewFor"/>.</param>
    public static ProductFormInput FromCatalog(
        CatalogMedicineSearchItem entry, int defaultReorderLevel)
    {
        var input = FromCatalogShape(entry);
        input.ReorderLevel = defaultReorderLevel;

        return input;
    }

    private static ProductFormInput FromCatalogShape(CatalogMedicineSearchItem entry) => new()
    {
        ProductType = ProductType.Medicine,
        BrandName = entry.BrandName,
        GenericName = entry.GenericName,
        Company = entry.Manufacturer,
        Strength = entry.Strength,
        DosageForm = entry.DosageForm,
        // Pre-set from the catalogue's own provisional flag. The form asks a pharmacist to
        // confirm it, which is where a machine-derived guess becomes a human decision.
        IsAntibiotic = entry.IsAntibiotic,
        Preset = UnitPreset.ThreeLevel,
        BaseUnitName = "piece",
        HasMidUnit = true,
        MidUnitName = "strip",
        HasLargeUnit = true,
        LargeUnitName = "box",
        CatalogMedicineId = entry.Id,
    };

    /// <summary>A blank form for a new product of the given type, with a sensible preset.</summary>
    /// <param name="defaultReorderLevel">
    /// From settings. Passed in rather than read here because this is a plain input record with
    /// no services - and because that keeps "where does the default come from" a question with
    /// one answer.
    /// </param>
    public static ProductFormInput NewFor(ProductType type, int defaultReorderLevel)
    {
        var input = NewForShape(type);
        input.ReorderLevel = defaultReorderLevel;

        return input;
    }

    private static ProductFormInput NewForShape(ProductType type) => type switch
    {
        ProductType.Medicine => new ProductFormInput
        {
            ProductType = type,
            Preset = UnitPreset.ThreeLevel,
            BaseUnitName = "piece",
            HasMidUnit = true,
            MidUnitName = "strip",
            HasLargeUnit = true,
            LargeUnitName = "box",
        },
        // A sanitiser or a tin of formula is a unit plus a bulk pack, not a strip.
        ProductType.BabyCare or ProductType.PersonalCare or ProductType.MedicalSupply =>
            new ProductFormInput
            {
                ProductType = type,
                Preset = UnitPreset.UnitAndBulk,
                HasLargeUnit = true,
                LargeUnitName = "carton",
            },
        _ => new ProductFormInput { ProductType = type, Preset = UnitPreset.SingleUnit },
    };

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// The "how is this sold?" shortcuts.
///
/// Offered instead of five raw inputs because five raw inputs is where someone types a strip
/// count into a carton field. The preset sets up the shape; Custom exposes everything.
/// </summary>
public enum UnitPreset
{
    /// <summary>piece → strip → box. The standard medicine shape.</summary>
    ThreeLevel = 0,

    /// <summary>A unit plus a bulk pack: bottle → carton, tin → carton.</summary>
    UnitAndBulk = 1,

    /// <summary>One unit and nothing else: a saline bag.</summary>
    SingleUnit = 2,

    /// <summary>Everything shown, nothing assumed.</summary>
    Custom = 3,
}
