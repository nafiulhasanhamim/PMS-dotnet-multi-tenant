using PMS.Web.Api;

namespace PMS.Web.ViewModels;

/// <summary>
/// The mode indicator shown on the register.
///
/// <para><b>The explanation under Off is the important part.</b> A register whose patient and
/// doctor columns are entirely dashes reads as a system that lost the data, or as a pharmacy in
/// breach — unless the page says plainly that this pharmacy does not record those details and
/// that the sales themselves are all present. Somebody reading the register may be an inspector,
/// and leaving them to guess is the worst option available.</para>
/// </summary>
public sealed record AntibioticModeNoticeModel(
    string Label,
    string? Explanation,
    bool SettingsLink)
{
    public static AntibioticModeNoticeModel For(
        AntibioticPrescriptionMode mode, bool canChangeMode) => mode switch
    {
        AntibioticPrescriptionMode.Off => new AntibioticModeNoticeModel(
            "Off",
            "This pharmacy does not currently record prescription details for antibiotic "
            + "sales. All antibiotic sales are still tracked below.",
            canChangeMode),

        AntibioticPrescriptionMode.Optional => new AntibioticModeNoticeModel(
            "Optional",
            "Staff record prescription details when they have them. Rows without them are "
            + "expected, not errors.",
            canChangeMode),

        _ => new AntibioticModeNoticeModel(
            "Required",
            "Every antibiotic sale must carry a verified prescription. Rows flagged below "
            + "predate this setting or indicate a problem — see the note beneath the table.",
            canChangeMode),
    };
}
