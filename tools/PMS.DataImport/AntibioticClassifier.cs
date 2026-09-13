using PMS.Domain.Entities.Catalog;

namespace PMS.DataImport;

/// <summary>
/// Decides, provisionally, whether a generic is an antibacterial.
///
/// <para><b>This is a worklist generator, not an authority.</b> The regulatory module blocks
/// employees from selling antibiotics, forces prescription capture on each sale, and builds a
/// register from the flag. The source dataset has no antibiotic field, so everything below is
/// inference from names, and every run writes a report for a human to check.</para>
///
/// <para><b>Why two signals rather than the drug class alone.</b> Classifying on the class
/// name was the original plan, and testing it against the real data showed it failing on
/// drugs that matter:</para>
///
/// <list type="table">
/// <item><term>Ciprofloxacin</term><description>class is "Anti-diarrhoeal Antimicrobial
/// drugs" — names the indication, not the family</description></item>
/// <item><term>Metronidazole</term><description>class is "Amoebicides"</description></item>
/// <item><term>Isoniazid</term><description>class is "Anti-Tubercular
/// Chemotherapeutics"</description></item>
/// <item><term>Azithromycin Dihydrate</term><description>no class at all</description></item>
/// <item><term>Sulphonamides and Trimethoprim</term><description>British spelling, so a
/// "sulfonamide" keyword misses it</description></item>
/// </list>
///
/// <para>The source is also plainly wrong in places: it files both Linezolid (an
/// oxazolidinone) and Clindamycin (a lincosamide) under "Macrolides". So the generic's own
/// name is used as a second, independent signal, and either firing is enough.</para>
///
/// <para><b>Why "either" and not "both".</b> The two errors are not symmetrical. A false
/// positive costs a pharmacist one unnecessary prescription capture. A false negative lets an
/// antibiotic be sold by an employee with no prescription recorded and leaves it off the
/// regulatory register. Given that asymmetry, the classifier is deliberately biased towards
/// flagging, and the report shows exactly which rows only one signal supports.</para>
///
/// <para><b>Antibacterials only.</b> Antifungals and antivirals are not flagged — an
/// antibiotic is an antibacterial, and Bangladesh's stewardship rules are about those. Those
/// classes are listed in the report under a separate heading so the decision is visible
/// rather than implicit.</para>
/// </summary>
public static class AntibioticClassifier
{
    /// <summary>
    /// Drug-class terms that indicate an antibacterial.
    ///
    /// The first ten are the list from the specification. The rest were added after checking
    /// all 421 distinct classes in the source by hand — British spellings, families the list
    /// omitted, and classes that name a use rather than a chemistry.
    /// </summary>
    public static readonly string[] ClassTerms =
    [
        // From the original specification.
        "antibiotic", "penicillin", "cephalosporin", "macrolide", "quinolone",
        "tetracycline", "aminoglycoside", "sulfonamide", "carbapenem", "glycopeptide",

        // British spellings the above miss.
        "sulphonamide", "sulphamethoxazole",

        // Families the list omitted.
        "lincosamide", "oxazolidinone", "monobactam", "polymyxin", "rifamycin",
        "nitroimidazole", "nitrofuran", "chloramphenicol",

        // Classes naming a use rather than a family. These are where the specification's
        // list failed: each of them holds real antibacterials in this dataset.
        "antibacterial", "anti-bacterial", "antimicrobial", "anti-microbial",
        "anti-infective", "anti infective", "amoebicide", "antiprotozoal",
        "tubercular", "tuberculosis", "leprosy", "trimethoprim", "urinary anti",
    ];

    /// <summary>
    /// Class terms that look antimicrobial but are not antibacterial, checked before the
    /// terms above so they cannot be dragged in by a broader match.
    ///
    /// "Anti-fungal or anti-bacterial ear drops" is the awkward one: it contains
    /// "anti-bacterial" and genuinely may contain an antibacterial, so it is NOT excluded —
    /// it is left to the class match and appears in the report for review.
    /// </summary>
    public static readonly string[] ClassExclusions =
    [
        "antifungal", "anti-fungal", "antiviral", "anti-viral", "mycoses", "anthelmintic",
    ];

    /// <summary>
    /// Stems in a generic's own name that indicate an antibacterial.
    ///
    /// Mostly drug-family suffixes, which is what makes this signal independent of how the
    /// source chose to classify anything.
    /// </summary>
    public static readonly string[] NameStems =
    [
        // Beta-lactams. The cephalosporin prefixes live in NameWordPrefixStems, because
        // "cephal" as a bare substring also matches "encephalitis".
        "cillin", "penem", "aztreonam", "sulbactam", "tazobactam", "clavulanic", "avibactam",

        // Macrolides and relatives. Spelled out rather than using "erythro", which also
        // matches erythropoietin — a hormone, and not remotely an antibiotic.
        "azithro", "clarithro", "erythromycin", "roxithro", "spiramycin", "josamycin",
        "telithro", "fidaxomicin",

        // Quinolones.
        "oxacin", "nalidixic",

        // Aminoglycosides.
        "gentamicin", "amikacin", "tobramycin", "netilmicin", "streptomycin", "kanamycin",
        "neomycin", "framycetin", "paromomycin", "capreomycin", "spectinomycin",

        // Tetracyclines.
        "cycline",

        // Others.
        "chloramphenicol", "clindamycin", "lincomycin", "linezolid", "tedizolid",
        "vancomycin", "teicoplanin", "daptomycin", "colistin", "polymyxin", "bacitracin",
        "gramicidin", "fosfomycin", "fusidic", "mupirocin", "nitrofurantoin",
        "furazolidone", "metronidazole", "tinidazole", "secnidazole", "ornidazole",
        "sulfamethoxazole", "sulphamethoxazole", "sulfadiazine", "sulphadiazine",
        "trimethoprim", "dapsone", "clofazimine",

        // Anti-tubercular.
        "rifampicin", "rifabutin", "rifapentine", "rifaximin", "isoniazid", "pyrazinamide",
        "ethambutol", "ethionamide", "cycloserine", "bedaquiline", "delamanid",
    ];

    /// <summary>
    /// Stems that must begin a word to count.
    ///
    /// The cephalosporins are all named cef- or ceph-, but those letters also sit inside
    /// ordinary words: "Encephalitis Vaccine" contains "cephal" and is not an antibiotic. A
    /// word-start rule keeps the family without the collateral damage.
    /// </summary>
    public static readonly string[] NameWordPrefixStems =
    [
        "cef", "ceph",
    ];

    /// <summary>
    /// Names that contain one of the stems above without being antibacterial.
    ///
    /// Kept as short as possible and each one justified, because an over-eager exclusion list
    /// is how a real antibiotic gets missed. An earlier draft of this list vetoed
    /// "Esomeprazole + Amoxicillin + Clarithromycin" — an H. pylori triple therapy that is
    /// unambiguously antibiotic-containing — simply because it contains "omeprazole". These
    /// are the cytotoxics and antifungals whose names end in -mycin and nothing else.
    /// </summary>
    public static readonly string[] NameExclusions =
    [
        "mitomycin", "bleomycin", "dactinomycin", "plicamycin",   // cytotoxics
        "natamycin", "nystatin", "hachimycin",                     // antifungals
    ];

    public sealed record Verdict(bool IsAntibiotic, AntibioticSignal Signal)
    {
        public static readonly Verdict No = new(false, AntibioticSignal.None);
    }

    /// <summary>True when a drug class name reads as antibacterial.</summary>
    public static bool ClassIndicatesAntibiotic(string? className)
    {
        var key = Cleaning.Key(className);

        if (key is null)
        {
            return false;
        }

        if (ClassExclusions.Any(x => key.Contains(x, StringComparison.Ordinal))
            && !key.Contains("bacterial", StringComparison.Ordinal))
        {
            return false;
        }

        return ClassTerms.Any(term => key.Contains(term, StringComparison.Ordinal));
    }

    /// <summary>True when a generic's own name reads as antibacterial.</summary>
    public static bool NameIndicatesAntibiotic(string? genericName)
    {
        var key = Cleaning.Key(genericName);

        if (key is null)
        {
            return false;
        }

        // An exclusion only wins if it is the ONLY reason the name matched. A combination
        // product containing both a false friend and a genuine antibiotic stays flagged.
        var stems = NameStems
            .Where(s => key.Contains(s, StringComparison.Ordinal))
            .Concat(NameWordPrefixStems.Where(s => StartsAWord(key, s)))
            .ToList();

        if (stems.Count == 0)
        {
            return false;
        }

        var exclusionsPresent = NameExclusions
            .Where(x => key.Contains(x, StringComparison.Ordinal))
            .ToList();

        if (exclusionsPresent.Count == 0)
        {
            return true;
        }

        // Keep the flag if any matched stem sits outside every matched exclusion — i.e. the
        // name contains an antibacterial the exclusion does not account for.
        return stems.Any(stem =>
            !exclusionsPresent.Any(x => x.Contains(stem, StringComparison.Ordinal)));
    }

    /// <summary>True when <paramref name="stem"/> begins a word within <paramref name="text"/>.</summary>
    private static bool StartsAWord(string text, string stem)
    {
        var at = text.IndexOf(stem, StringComparison.Ordinal);

        while (at >= 0)
        {
            var precededByBoundary = at == 0 || !char.IsLetterOrDigit(text[at - 1]);

            if (precededByBoundary)
            {
                return true;
            }

            at = text.IndexOf(stem, at + 1, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>Combines both signals and records which one fired.</summary>
    public static Verdict Classify(string? genericName, string? className)
    {
        var byClass = ClassIndicatesAntibiotic(className);
        var byName = NameIndicatesAntibiotic(genericName);

        return (byClass, byName) switch
        {
            (true, true) => new Verdict(true, AntibioticSignal.ClassAndName),
            (true, false) => new Verdict(true, AntibioticSignal.ClassOnly),
            (false, true) => new Verdict(true, AntibioticSignal.NameOnly),
            _ => Verdict.No,
        };
    }
}
