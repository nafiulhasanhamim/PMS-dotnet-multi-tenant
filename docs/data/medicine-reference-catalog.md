# Medicine reference catalog

A shared, platform-owned list of ~21,700 medicines sold in Bangladesh, loaded from a Kaggle
dataset. Tenants search it during onboarding and copy entries into their own catalog.

> **Status: imported and verified.** 21,714 medicines, 1,711 generics, 240 manufacturers, 421
> drug classes, 113 dosage forms. Import takes 7.3s, re-import is a no-op, zero rejected rows.
> The antibiotic flag on 173 generics is **provisional and needs human review** — see
> [Antibiotic classification](#antibiotic-classification).

---

## 1. Purpose

A pharmacy signing up would otherwise have to type in every medicine it stocks — brand,
generic, manufacturer, strength, dosage form — several hundred entries before the software
does anything useful. That is the single largest onboarding barrier in the product.

So the platform ships the catalog. A pharmacy searches for "nap", gets Napa 500 mg by Beximco
with the generic already attached, and only has to set **its own price and pack
configuration**. Nothing else.

### Why platform-level rather than tenant-scoped

Every other business table in this system carries a `TenantId` and is invisible across
pharmacies. These five deliberately do not. They sit outside tenant isolation exactly as the
`Tenants` table itself does.

The reasoning is short: this data belongs to nobody. Napa is Napa at every pharmacy in
Bangladesh. Copying 21,714 rows into each new tenant would multiply the same facts by the
number of customers, make a catalog correction a per-tenant migration, and gain no isolation
— there is nothing here to leak.

The other half of that bargain is the standing rule at the end of this document: **tenant
users only ever read these tables.**

### The failure mode to know about

If any catalog entity were made tenant-scoped by accident, nothing would throw. Query filters
would resolve the unset tenant to `Guid.Empty`, every pharmacy would see an empty catalog, and
the importer would write rows nobody could read. That silence is why
`tests/PMS.SchemaTests/Tenancy/CatalogIsPlatformLevelTests.cs` asserts the absence of a
`TenantId`, of `ITenantEntity`, and of a query filter on all five entities, and proves a query
returns identical rows under two different tenants and under none.

---

## 2. Source

| | |
|---|---|
| Dataset | **Assorted Medicine Dataset of Bangladesh** |
| Author | Ahmed Shahriar Sakib (Kaggle) |
| Upstream origin | Scraped from medex.com.bd |
| Local copy | `C:\Projects\bd-medicine-scraper-dev\kaggle_data` |
| Imported | 2026-09-08 |
| Files used | `medicine.csv`, `generic.csv`, `manufacturer.csv`, `drug class.csv`, `dosage form.csv` |
| Files not used | `indication.csv` — see below |

> ### ⚠ Licensing must be checked before this ships to paying customers
>
> This has **not** been verified. The dataset is a third-party scrape of a commercial website,
> which raises two separate questions: the Kaggle dataset's own licence terms, and whether
> medex.com.bd's terms permit redistribution of the underlying data in a commercial product.
> Confirm both, in writing, before the first paying customer. If either is restrictive the
> catalog needs a different source — the schema and importer survive that, the data does not.

**`indication.csv` (2,043 rows) is deliberately not imported.** The target schema has no
indication table; `CatalogGeneric.IndicationSummary` is filled from `generic.csv`'s own
`indication` column instead. Importing the separate file would create a lookup nothing
references.

### Actual file contents vs. the specification

Read from the files, not from the dataset's documentation, which differs:

| File | Rows found | Expected | Note |
|---|---|---|---|
| medicine.csv | 21,714 | ~21,000 | **also has a `dosage form` column**, undocumented |
| generic.csv | 1,711 | 1,700–1,800 | 22 columns; 5 used |
| manufacturer.csv | **240** | 245 | |
| drug class.csv | **453** | ~400 | 421 distinct once case-folded |
| dosage form.csv | **113** | ~120 | |
| indication.csv | 2,043 | ~2,000 | not imported |

That `dosage form` column on `medicine.csv` is a small win: a medicine's form is read directly
from its own row rather than inferred.

---

## 3. Schema

```
CatalogDrugClass ──┐
                   │ (nullable)
                   ▼
             CatalogGeneric ◀────┐
                                 │ (nullable)
CatalogManufacturer ─────────────┤
                                 │
CatalogDosageForm ───────────────┴──── CatalogMedicine
```

**None of these has a `TenantId`. None implements `ITenantEntity`. None has a query filter.**

| Table | Rows | Key columns |
|---|---|---|
| `CatalogDrugClasses` | 421 | `Name` (unique), `IsAntibioticClass` |
| `CatalogDosageForms` | 113 | `Name` (unique) |
| `CatalogManufacturers` | 240 | `Name` (unique) |
| `CatalogGenerics` | 1,711 | `Name` (unique), `DrugClassId?`, `MonographUrl?`, `IndicationSummary?`, `IsAntibiotic`, `AntibioticSignal` |
| `CatalogMedicines` | 21,714 | `SourceBrandId` (unique), `BrandName`, `GenericId?`, `ManufacturerId?`, `DosageFormId?`, `Strength?`, `MedicineType?`, `PackageInfo?`, `SourceUnitPrice?`, `IsActive`, `ImportedAt`, `LastUpdatedAt` |

DDL: `database/scripts/006_CreateCatalogTables.sql`.
Entities: `src/Core/PMS.Domain/Entities/Catalog/`.
EF config: `src/Infrastructure/PMS.Persistence/Configurations/CatalogConfigurations.cs`.

### `SourceBrandId` — an addition to the specified schema

The brief specified the medicine upsert key as **brand name + strength + manufacturer**. That
key does not hold in the real data:

```
brand + strength + manufacturer                → 21,182 distinct  (532 rows collapse)
brand + strength + manufacturer + dosage form  → 21,648 distinct  ( 66 rows collapse)
brand id  (the source's own primary key)       → 21,714 distinct  (  0 collapse)
```

The collisions are distinct products, not duplicate rows. `Glarine 100 IU/ml` by ACI Limited
exists as a **cartridge (৳600)**, a **vial (৳600)** and a **biopen (৳950)** — three products a
pharmacy stocks and prices separately.

Using the specified key would have silently discarded 532 products and made "run it twice"
report meaningless updates. `SourceBrandId` is the source's stable identity, so re-import is
exactly idempotent, and it is unique-indexed so the database would refuse a duplicate even if
the logic tried.

### Indexes

| Index | Purpose |
|---|---|
| `UX_CatalogMedicines_SourceBrandId` | the upsert key |
| `IX_CatalogMedicines_BrandName` | prefix search, `INCLUDE`s the search-grid columns so the seek needs no key lookups |
| `IX_CatalogMedicines_GenericId` | browse by ingredient; antibiotic register joins |
| `UX_CatalogGenerics_Name`, `IX_CatalogGenerics_IsAntibiotic` | generic search; register filter |

**No full-text index.** A B-tree index serves `LIKE 'nap%'` as a seek, which is what the
onboarding search does. Measured on the full 21,714 rows:

```
SELECT BrandName, Strength, SourceUnitPrice FROM CatalogMedicines WHERE BrandName LIKE 'nap%'
  → Index Seek, SEEK:([BrandName] >= N'nap' AND [BrandName] < N'naQ')
  → 93 rows, CPU 0 ms, elapsed 0 ms
```

The limit worth knowing: this index **cannot** serve infix search (`'%nap%'`), which degrades
to a scan. If searching mid-word becomes a requirement, that is what a full-text index is for.

---

## 4. Import procedure

A standalone console tool. **Not an API endpoint** — it rewrites shared data for every
pharmacy at once, and there is no good version of a tenant user triggering that over HTTP.

```bash
# 1. Create the tables (once)
sqlcmd -S .\SQLEXPRESS -E -C -i database/scripts/006_CreateCatalogTables.sql

# 2. Dry run first. Parses, validates, reports, writes NOTHING.
dotnet run --project tools/PMS.DataImport -- --dry-run

# 3. Read the summary and the antibiotic review file, then import for real.
dotnet run --project tools/PMS.DataImport
```

| Option | Meaning |
|---|---|
| `-s, --source <folder>` | Folder holding the five CSVs. Default: `C:\Projects\bd-medicine-scraper-dev\kaggle_data` |
| `-o, --output <folder>` | Where reports are written. Default: `./import-reports` |
| `-c, --connection <string>` | Override the connection string. Otherwise `ConnectionStrings__DefaultConnection` |
| `-n, --dry-run` | Validate and report only |

Import order is enforced in code and is not optional: **drug classes → dosage forms →
manufacturers → generics → medicines.** Generics reference a drug class; medicines reference
all three lookups.

### Reading the output

Three files per run, timestamped, `-dryrun` suffixed when applicable:

- `import-summary-*.txt` — per-table counts and the antibiotic totals
- `antibiotic-review-*.txt` — **the file that needs a human**
- `rejected-rows-*.txt` — rows the importer refused, with reasons

Per-table counts mean:

| Column | Meaning |
|---|---|
| `read` | rows read from the CSV |
| `inserted` | new rows |
| `updated` | existing rows whose values changed |
| `unchanged` | matched an existing row that was already correct |
| `deduped` | duplicate of a row already handled **in the same file** |
| `rejected` | refused; written to the rejects file |

A first run is all `inserted`. A second run is all `unchanged` — that is the idempotency
check:

```
first run                                        second run
  CatalogDrugClasses    inserted    421             inserted   0   unchanged    421
  CatalogDosageForms    inserted    113             inserted   0   unchanged    113
  CatalogManufacturers  inserted    240             inserted   0   unchanged    240
  CatalogGenerics       inserted  1,711             inserted   0   unchanged  1,711
  CatalogMedicines      inserted 21,714             inserted   0   unchanged 21,714
  7.3s                                              3.3s
```

---

## 5. Data quality notes

### Cleaning applied

- **Trim and collapse** — every text field trimmed, internal runs of whitespace collapsed.
- **Empty becomes NULL, never `""`.** An empty string in a nullable column is a value that
  looks present to every query: `WHERE Strength IS NULL` misses it.
- **Case-folded deduplication** — lookups are keyed on the lower-cased name, so "Beximco",
  "beximco" and "BEXIMCO " resolve to one row. The **first spelling seen is kept for display**,
  so the catalog reads naturally while the dedupe stays exact. Invariant culture, so the result
  does not depend on the machine's locale.
- **UTF-8 explicitly.** The price fields carry the Bengali taka sign (৳) and manufacturer
  names carry accents. Reading as Windows-1252 would write convincing-looking mojibake into
  `nvarchar`.
- **Truncation to column width** rather than failing a whole batch on one long outlier.

### Row-count variances, explained

| Table | Source rows | Imported | Why |
|---|---|---|---|
| Drug classes | 453 | **421** | 32 rows are case-only duplicates — `Proton Pump Inhibitor` appears 5× |
| Everything else | — | 1:1 | no deduplication needed |

### Known gaps in the source

| Gap | Count | Handling |
|---|---|---|
| Medicines with no strength | 849 (3.9%) | `NULL` |
| Medicines with no usable price | 78 | `NULL` — 42 blank, 36 literally `"Price Unavailable"`. **Not** zero: zero is a number a pharmacy could act on |
| Medicines with no generic | 2 | `GenericId` NULL |
| Generics with no drug class | 61 | `DrugClassId` NULL |
| Generics with no monograph link | 512 (30%) | `NULL` |
| Generics with no indication text | 84 | `NULL` |
| `Package Size` column | 35.8% blank | not imported; `PackageInfo` keeps `package container` verbatim instead |

### Rejected rows: **zero**

Every one of the 21,714 medicine rows and 1,711 generic rows parsed and validated. The rejects
file is still written on every run, saying so explicitly — a run that drops rows amongst
thousands of progress lines has dropped them silently in every practical sense.

### Unmatched lookups: **zero**

Every generic, manufacturer and dosage form named by a medicine row exists in its lookup file.
Referential integrity in this dataset is perfect.

The importer's policy for when that stops being true: **create the lookup row on the fly**,
consistently, and count it. A null FK would make the medicine unfindable by that dimension,
and "we don't know the manufacturer" is worse than one lookup row the source forgot to list.
The summary reports these separately (`Lookups created from medicine rows`), so a non-zero
count is a signal the source has drifted.

### Not parsed on purpose

`PackageInfo` keeps the raw text — `"Unit Price: ৳ 5.98,(100's pack: ৳ 598.00),"`. It is not
decomposed into pieces-per-strip and strips-per-box because the source has no reliable pack
structure, and a guessed one would present itself to every tenant as authoritative. Pack
configuration is per-tenant. (This is also why a real CSV parser is non-negotiable: those
commas sit inside a quoted field.)

---

## 6. Antibiotic classification

> **This is a worklist, not a result. It requires human verification before it is relied upon
> for regulatory purposes.**

The regulatory module depends on this flag: employees are blocked from selling antibiotics,
every such sale must capture a prescription, and the antibiotic register is generated from it.
Getting it wrong has compliance consequences.

**The source dataset has no antibiotic field.** Everything below is inference.

### Why two signals, not the drug class alone

Deriving from the drug class was the specified approach. Tested against the real 421 classes,
the specified keyword list fails on drugs the acceptance criteria themselves name:

| Drug | Its class in the source | Class-only verdict |
|---|---|---|
| **Ciprofloxacin** | `Anti-diarrhoeal Antimicrobial drugs` | **missed** |
| **Metronidazole** | `Amoebicides` | **missed** |
| **Isoniazid** | `Anti-Tubercular Chemotherapeutics` | **missed** |
| **Azithromycin Dihydrate** | *(no class at all)* | **missed** |
| `Sulphonamides & Trimethoprim` | — | **missed** (British spelling) |

The source is also plainly wrong in places: it files both **Linezolid** (an oxazolidinone) and
**Clindamycin** (a lincosamide) under `Macrolides`.

So a second, independent signal is used — **stems in the generic's own name** (`-cillin`,
`-oxacin`, `cef-`/`ceph-` at a word start, `-cycline`, `-penem`, `metronidazole`, `isoniazid`,
…). Either signal firing sets the flag.

**Why "either" and not "both":** the two errors are not symmetrical. A false positive costs a
pharmacist one unnecessary prescription capture. A false negative lets an antibiotic be sold by
an employee with nothing recorded, and leaves it off the regulatory register. The classifier is
deliberately biased towards flagging.

**Antibacterials only.** Antifungals, antivirals and anthelmintics are **not** flagged — an
antibiotic is an antibacterial, and the stewardship rules this serves are about those. Those
classes appear in the report's "not flagged" section so the decision is visible rather than
implicit.

### Results

| | Count |
|---|---|
| Drug classes flagged antibacterial | **39** of 421 |
| Generics flagged antibiotic | **173** of 1,711 |
| ├ both signals agreed | 129 |
| ├ drug class only — **review** | 16 |
| └ generic name only — **review** | 28 |
| Medicines inheriting the flag | **4,742** of 21,714 (22%) |

Sanity check: 173 of 1,711 generics is the right order of magnitude for a Bangladeshi retail
catalog. Single digits or many thousands would mean the classifier is broken.

### The 39 drug classes flagged as antibacterial

```
4-Quinolone preparations                    Ophthalmic and Topical antibacterial products
Aminoglycosides                             Ophthalmic antibacterial drugs
Amoebicides                                 Ophthalmic steroid - antibiotic combined preparations
Anti-diarrhoeal Antimicrobial drugs         Other antibacterial preparation
Anti-diarrhoeal Antiprotozoal               Other antibiotic
Anti-fungal or anti-bacterial ear drops     Other beta-lactam Antibiotics
Anti-infective & Anesthetic combined prep.  Other Systemic Anti-infective ... Urinary tract infections
Anti-Tubercular Antibiotics                 Oxidising agent with antibacterial and antiviral properties
Anti-Tubercular Chemotherapeutics           Penicillinase-resistant penicillins
Aural Anti-bacterial preparations           Second generation Cephalosporins
Aural steroid & antibiotic combined prep.   Sulphonamides & Trimethoprim
Benzylpenicillin & Phenoxymethyl penicillin Systemic Urinary Anti- infective
Broad spectrum penicillins                  Tetracycline group of drugs
Combined anti- Tubercular Preparations      Third generation Cephalosporins
Ear Anti-Infectives & Antiseptics           Topical antibiotic & retinoid preparations
Eye Anti-Infectives & Antiseptics           Topical Antibiotic preparations
First generation Cephalosporins             Topical antibiotics for Acne
Fourth generation Cephalosporins
Glycopeptide
Intracellular antibiotic
Long acting penicillin
Macrolides
```

The remaining 382 are listed in full in `antibiotic-review-*.txt`.

### What to review first

**The 16 flagged by drug class only.** This list mixes genuine antibacterials with antiseptics
that arguably are not stewardship antibiotics, and contains one misfiled antifungal:

| Generic | Class | Comment |
|---|---|---|
| Colistimethate Sodium | Other antibacterial preparation | genuine antibiotic |
| Retapamulin | Topical Antibiotic preparations | genuine antibiotic |
| Sodium Fusidate (Topical) | Topical Antibiotic preparations | genuine antibiotic |
| Sulfacetamide sodium | Ophthalmic antibacterial drugs | genuine antibiotic |
| Nitrofurazone | Topical Antibiotic preparations | genuine antibiotic |
| Chlorhexidine Gluconate ×3 | Other antibacterial preparation | **antiseptic — probably should not be flagged** |
| Hydrogen peroxide | Oxidising agent … | **antiseptic** |
| Isopropyl alcohol | Other antibacterial preparation | **antiseptic** |
| Dequalinium Chloride | Other antibacterial preparation | **antiseptic** |
| **Natamycin** | Ophthalmic antibacterial drugs | **antifungal, misfiled by the source** |
| Nitazoxanide, Diloxanide Furoate | Antiprotozoal / Amoebicides | judgement call |
| Clioquinol + Flumetasone | Aural steroid & antibiotic | judgement call |
| Lyophilized Bacterial Lysate | Other antibacterial preparation | **not a drug in this sense** |

**The 28 flagged by name only** is what class-based classification would have missed entirely,
and is the reason the second signal exists. It correctly contains `Azithromycin Dihydrate` (no
class), `Ampicillin Sodium`, `Ceftazidime + Avibactam`, `Dapsone`, and combination products
like `Esomeprazole + Amoxicillin + Clarithromycin` and `Betamethasone + Gentamicin`.

Verified against the acceptance criteria:

```
Amoxicillin Trihydrate                IsAntibiotic=1  signal=1 (both)
Azithromycin Dihydrate                IsAntibiotic=1  signal=3 (name only, no class)
Azithromycin Dihydrate (Ophthalmic)   IsAntibiotic=1  signal=1 (both)
Metronidazole                         IsAntibiotic=1  signal=1 (both)
Ciprofloxacin                         IsAntibiotic=1
Paracetamol                           IsAntibiotic=0  signal=0
Omeprazole                            IsAntibiotic=0  signal=0
```

### How to record a correction

Set `IsAntibiotic` to the reviewed value **and** `AntibioticSignal = 4`
(`ManuallyReviewed`). Later imports re-derive the flag for every other row but leave signal-4
rows alone, so a human decision is never overwritten by a re-run.

Classifier source, with each keyword and exclusion justified in comments:
`tools/PMS.DataImport/AntibioticClassifier.cs`.

---

## 7. Known limitations

1. **`SourceUnitPrice` is stale reference data only.** A point-in-time scrape of a published
   price, already out of date. It exists so a pharmacy browsing the catalog recognises a
   product by roughly the price it expects. **It must never reach billing, margin or profit
   calculations.** Tenants always set their own prices.

2. **Unit configuration is absent from the source.** Pieces per strip, strips per box, loose
   sale units — none of it is in the dataset, and `PackageInfo` is unstructured text. Every
   tenant sets its own pack configuration when it imports a medicine.

3. **The dataset is a snapshot.** Discontinued products stay; new products are missing; prices
   drift immediately. `CatalogMedicine.IsActive` exists so a future refresh can retire a
   product without deleting a row that a tenant may already have imported from.

4. **The antibiotic flag is provisional** — see above. It must be reviewed before the
   regulatory module relies on it.

5. **Prefix search only.** `LIKE 'nap%'` seeks the index; `LIKE '%nap%'` scans. Mid-word
   search needs a full-text index.

6. **No automated refresh.** This is a manual, occasionally-run import. There is no scheduled
   job and no live upstream connection.

7. **Licensing is unverified.** See [Source](#2-source). This is the item that could block
   shipping.

---

## 8. Standing rule

> **This catalog is read-only to tenant users. Only platform-level processes write to it.**

Writes come from `tools/PMS.DataImport` and whatever future process refreshes the catalog.
Tenant-facing code — API, UI, background jobs running in a tenant context — reads and copies.
A tenant's own medicine list is the tenant-scoped `Medicine` entity, which is populated *from*
here and owns its own prices and pack configuration.

The tenant-facing "search the catalog and import into my pharmacy" API and UI are the
**Medicine Master** module, not this task. This task loaded the data.
