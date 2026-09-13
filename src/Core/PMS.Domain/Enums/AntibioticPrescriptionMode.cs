namespace PMS.Domain.Enums;

/// <summary>
/// How strictly a pharmacy captures prescription details when it dispenses an antibiotic.
///
/// <para><b>Per pharmacy, and defaulting to Off, for a reason worth understanding before
/// changing it.</b> Antibiotics legally require a prescription in Bangladesh and pharmacies are
/// expected to keep a register. In practice most retail pharmacies sell them over the counter.
/// A system that hard-blocks that does not produce compliance: it produces invented patient
/// names, or staff working around the till entirely, and the second destroys stock accuracy for
/// every other module. Enforcement is tightening and Model Pharmacies do apply the rule
/// strictly, so the compliant path has to exist and work properly — it just cannot be the only
/// path.</para>
///
/// <para><b>What is never optional is the register.</b> Every antibiotic sale is recorded
/// whatever the mode. Only the prescription details vary. A pharmacy on the loosest setting
/// still has a real record of what went out of the door, which is most of what an inspector
/// asks for.</para>
/// </summary>
public enum AntibioticPrescriptionMode
{
    /// <summary>
    /// Antibiotics sell like anything else. No prescription panel, no prescription stored, and
    /// any role may dispense. The default, because it is what an unconfigured pharmacy is
    /// actually doing, and a default that lies about that is a default somebody switches off.
    /// </summary>
    Off = 0,

    /// <summary>
    /// The panel appears and every field is optional. Details are kept when somebody has them
    /// and the sale is never blocked for their absence.
    /// </summary>
    Optional = 1,

    /// <summary>
    /// Every antibiotic sale needs a complete, verified prescription, and only an Admin or a
    /// Pharmacist may dispense one. For a pharmacy under Model Pharmacy rules or preparing for
    /// an inspection.
    /// </summary>
    Required = 2,
}
