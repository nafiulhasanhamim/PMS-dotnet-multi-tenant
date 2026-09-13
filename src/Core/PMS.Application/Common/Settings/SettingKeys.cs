using PMS.Domain.Enums;

namespace PMS.Application.Common.Settings;

/// <summary>
/// What kind of value a setting holds, so one validator and one caster can serve all of them.
/// </summary>
public enum SettingKind
{
    /// <summary>Free text. May be empty unless the key is marked required.</summary>
    Text = 0,

    /// <summary>A whole number, bounded by the key's <c>Minimum</c> and <c>Maximum</c>.</summary>
    Integer = 1,

    /// <summary>One of a fixed set of names — today only <see cref="AntibioticPrescriptionMode"/>.</summary>
    Enumeration = 2,
}

/// <summary>
/// One configurable value: its key, what it means, what it defaults to, and what counts as valid.
///
/// <para><b>This record is the fallback.</b> <c>ISettingsService</c> returns
/// <see cref="Default"/> when a key is missing from the database, which happens for a tenant
/// created before the settings migration or after a seed that failed halfway. The alternative —
/// throwing — would take down billing because a row was absent, and the value that was hardcoded
/// before Module 10 is by definition a safe answer.</para>
/// </summary>
/// <param name="Key">The database key. Snake case; see <c>AppSetting.Key</c>.</param>
/// <param name="Default">
/// The value this setting had as a hardcoded constant before Module 10 migrated it. Changing one
/// of these changes what a pharmacy gets when its key is missing — not what any existing pharmacy
/// gets, because seeding wrote the value into the row.
/// </param>
/// <param name="Required">
/// Whether an empty value is refused. Only for text: an integer and an enumeration are required by
/// their own nature.
/// </param>
public sealed record SettingDefinition(
    string Key,
    SettingKind Kind,
    string Default,
    string Description,
    bool Required = false,
    int Minimum = 0,
    int Maximum = 0,
    Type? EnumType = null);

/// <summary>
/// Every setting this system has, in one list.
///
/// <para><b>Adding a setting means adding a line here and nothing else structural</b> — the seed,
/// the validator, the API contract and the caster all read this catalogue. That was the argument
/// for key-value storage in the first place, and it only holds if this stays the single
/// declaration.</para>
///
/// <para><b>Every default below is the literal that used to be hardcoded</b>, with the module it
/// came from named in its description. That correspondence is the point of Module 10: the numbers
/// did not change, only where they live. See <c>docs/10-dashboard-and-settings.md</c> for the
/// migration checklist.</para>
/// </summary>
public static class SettingKeys
{
    // ── Pharmacy identity ────────────────────────────────────────────────────────────────
    //
    // These four are genuinely new. Before Module 10 the invoice and the salary slip carried
    // placeholder text in the Razor markup - "Address line one, Dhaka", "Phone 01700-000000" -
    // with a comment saying a settings module would replace them. This is that module.

    public const string PharmacyName = "pharmacy_name";
    public const string PharmacyAddress = "pharmacy_address";
    public const string PharmacyPhone = "pharmacy_phone";
    public const string PharmacyLicenseNumber = "pharmacy_license_number";

    // ── Alerts and reports ───────────────────────────────────────────────────────────────

    public const string ExpiryAlertWindowDays = "expiry_alert_window_days";
    public const string DeadStockThresholdDays = "dead_stock_threshold_days";
    public const string DefaultReorderLevel = "default_reorder_level";

    // ── Billing rules ────────────────────────────────────────────────────────────────────

    public const string DiscountCapEmployeePercent = "discount_cap_employee_percent";
    public const string DiscountCapPharmacistPercent = "discount_cap_pharmacist_percent";

    // ── Regulatory ───────────────────────────────────────────────────────────────────────

    public const string AntibioticPrescriptionMode = "antibiotic_prescription_mode";

    /// <summary>
    /// A day threshold cannot exceed ten years.
    ///
    /// <para>Not a business rule so much as a guard against a typo becoming a silent
    /// disablement: an expiry window of 36,500 days includes every batch the pharmacy will ever
    /// hold, which looks identical to the alert being broken.</para>
    /// </summary>
    public const int MaxDays = 3650;

    /// <summary>
    /// Every setting, in the order the settings screen groups them.
    /// </summary>
    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        new(PharmacyName, SettingKind.Text, "My Pharmacy",
            "The pharmacy's name, as it appears on every invoice and salary slip.",
            Required: true),

        new(PharmacyAddress, SettingKind.Text, "Address line one, Dhaka",
            "Printed under the pharmacy name on invoices and salary slips."),

        new(PharmacyPhone, SettingKind.Text, "01700-000000",
            "Printed on every invoice and salary slip. Required - a receipt a customer cannot "
            + "act on is barely a receipt.",
            Required: true),

        new(PharmacyLicenseNumber, SettingKind.Text, "",
            "Drug licence number, printed on the invoice header where the pharmacy has one. "
            + "Blank by default because not every shop has one to hand on day one."),

        // Module 6. Was a constant on the alerts query and the default selection on the
        // expiring-soon page's window dropdown.
        new(ExpiryAlertWindowDays, SettingKind.Integer, "90",
            "Batches expiring within this many days appear in the Expiring soon alert and on "
            + "the dashboard.",
            Minimum: 1, Maximum: MaxDays),

        // Module 8. Was the default period on the dead stock report.
        new(DeadStockThresholdDays, SettingKind.Integer, "90",
            "Products with no sales in this many days appear on the Dead stock report.",
            Minimum: 1, Maximum: MaxDays),

        // Module 2. Was the starting reorder level on the new-product form. Applies to new
        // products only - see the migration checklist for why it is not retroactive.
        new(DefaultReorderLevel, SettingKind.Integer, "100",
            "The starting reorder level when adding a new product. Existing products are not "
            + "affected.",
            Minimum: 1, Maximum: 1_000_000),

        // Module 5. Were two constants in BillingPolicy, enforced at sale completion and shown
        // as helper text on the billing screen.
        new(DiscountCapEmployeePercent, SettingKind.Integer, "5",
            "The largest discount an Employee may apply to a sale. Admins are never capped.",
            Minimum: 0, Maximum: 100),

        new(DiscountCapPharmacistPercent, SettingKind.Integer, "10",
            "The largest discount a Pharmacist may apply to a sale. Admins are never capped.",
            Minimum: 0, Maximum: 100),

        // Module 7. Lived on the Tenant row until Module 10 moved it here - see the module doc
        // for why one storage location beats two that can drift.
        new(AntibioticPrescriptionMode, SettingKind.Enumeration, nameof(Domain.Enums.AntibioticPrescriptionMode.Off),
            "How strictly this pharmacy captures antibiotic prescriptions: Off, Optional or "
            + "Required.",
            EnumType: typeof(AntibioticPrescriptionMode)),
    ];

    private static readonly Dictionary<string, SettingDefinition> ByKey =
        All.ToDictionary(d => d.Key, StringComparer.Ordinal);

    /// <summary>The definition for a key, or null if nothing declares it.</summary>
    public static SettingDefinition? Find(string key) =>
        ByKey.TryGetValue(key, out var definition) ? definition : null;

    /// <summary>Whether this key is one the system actually reads.</summary>
    public static bool IsKnown(string key) => ByKey.ContainsKey(key);
}
