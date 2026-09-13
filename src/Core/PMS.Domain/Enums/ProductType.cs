namespace PMS.Domain.Enums;

/// <summary>
/// What kind of thing a product is.
///
/// <para>Bangladeshi pharmacies do not only sell medicines. They stock saline, syringes,
/// bandages, diapers, baby formula, handwash, sanitiser, soap and supplements. A system that
/// only understands "medicine" gets diapers entered as medicines with invented generic names
/// and strengths, which corrupts the catalogue and makes every downstream report
/// meaningless.</para>
///
/// <para>So <see cref="Medicine"/> is one type among several, and the medicine-specific
/// fields on a product are nullable and only apply to it.</para>
/// </summary>
public enum ProductType
{
    /// <summary>
    /// A pharmaceutical product. The only type for which generic name, strength, dosage form
    /// and the antibiotic flag are meaningful.
    /// </summary>
    Medicine = 0,

    /// <summary>Saline, syringes, bandages, gloves, thermometers.</summary>
    MedicalSupply = 1,

    /// <summary>Diapers, infant formula, baby wipes.</summary>
    BabyCare = 2,

    /// <summary>Handwash, sanitiser, soap, shampoo.</summary>
    PersonalCare = 3,

    /// <summary>Vitamins, protein powders, food supplements.</summary>
    Supplement = 4,

    /// <summary>Anything that does not fit the above.</summary>
    Other = 5,
}
