# Product Master (Module 2)

Each pharmacy's own catalogue of what it sells — medicines and non-medicines alike — with
one-click import from the platform medicine reference catalogue.

> **Status: working end to end.** Verified against SQL Express through the running API and UI:
> catalogue import (including the "already in your catalogue" flag), manual entry, all four
> unit shapes, every validation rule, the full role matrix, and two-tenant isolation.
> **210 tests pass.**

---

## 1. Overview — why Product, not Medicine

Bangladeshi pharmacies do not only sell medicines. They stock saline, syringes, bandages,
diapers, baby formula, handwash, sanitiser, soap and supplements. A system that only
understands "medicine" gets diapers entered **as** medicines, with invented generic names and
nonsense strengths — which corrupts the catalogue and makes every downstream report
meaningless.

So this module manages **products**, of which a medicine is one type:

| `ProductType` | Examples |
|---|---|
| `Medicine` (0) | Napa, Seclo, Ciprocin |
| `MedicalSupply` (1) | saline, syringes, bandages, gloves |
| `BabyCare` (2) | diapers, infant formula, wipes |
| `PersonalCare` (3) | handwash, sanitiser, soap |
| `Supplement` (4) | vitamins, protein powders |
| `Other` (5) | anything else |

Medicine-specific fields — generic name, strength, dosage form, antibiotic flag — are nullable
and **only** valid when the type is `Medicine`. Everything else is rejected, not quietly
nulled, because a caller sending a strength for a diaper has misunderstood something and a
400 says so where a silent null does not.

### One table, not two

Everything downstream — batches, FEFO, billing, reports — works on **integer quantities of a
product's base unit** and never needs to know whether that unit is a tablet, a bottle or a
tin. Splitting `Medicine` and `Product` into two tables would duplicate all of that machinery
and force every join in the system to know which table a line refers to.

---

## 2. Access control

| Action | Admin | Pharmacist | Employee |
|---|:---:|:---:|:---:|
| View lists and detail | ✓ | ✓ | ✓ |
| Search the reference catalogue | ✓ | ✓ | — |
| Create (manual or import) | ✓ | ✓ | — |
| Edit | ✓ | ✓ | — |
| Deactivate / reactivate | ✓ | — | — |
| **Prices in list responses** | ✓ | ✓ | **withheld** |
| Prices on the detail page | ✓ | ✓ | ✓ |

A Pharmacist may add and edit — they are the person who knows what the pharmacy stocks — but
only an Admin may deactivate, because that changes what everyone else can sell. This is the
one action the two roles differ on, which is why there is a `TenantWriterPolicy` (Admin **or**
Pharmacist) alongside the existing `TenantAdminPolicy`.

### Employee price-hiding

Enforced **in the API's response projection**, not in Razor:

```csharp
// ProductQueries.ListAsync
includePrices ? p.PricePerBase : null
```

`includePrices` is decided by the controller from the token's role, so for an Employee the
price column is never read and no price crosses the wire. Razor also omits the column, but
that is cosmetic — the guarantee is that the data is absent. Sending it and asking the UI to
hide a column is not a control: anyone can open the network tab.

**Precisely:** the JSON still carries the *key* `"pricePerBase": null`, because the DTO is one
record shared by both roles. The value is what is withheld.

Detail pages deliberately do show prices to an Employee — someone at the counter needs to tell
a customer what something costs. What they must not have is the whole price list in one
download.

---

## 3. Schema

`Product` implements `ITenantEntity`, so the tenant filter and the insert-time stamping are
both automatic. **No handler in this module filters by tenant, and none may.**

DDL: `database/scripts/007_CreateProductsTable.sql`.
Entity: `src/Core/PMS.Domain/Entities/Product.cs`.

| Column | Notes |
|---|---|
| `Id`, `TenantId` | Guid. `TenantId` stamped by `TenantEntityInterceptor`, never by a handler |
| `ProductType` | int, 0–5, check-constrained |
| `BrandName` | required |
| `Company`, `Category` | free text, nullable. Category is deliberately not a managed table yet |
| `IsActive` | soft delete |
| `CatalogMedicineId` | nullable FK → platform `CatalogMedicines`. See below |
| `GenericName`, `Strength`, `DosageForm`, `IsAntibiotic` | **medicine only** |
| `BaseUnitName`, `MidUnitName`, `LargeUnitName`, `BasePerMid`, `MidPerLarge` | unit configuration |
| `PricePerBase`, `PricePerMid`, `PricePerLarge` | `decimal(18,4)`. Current defaults, not history |
| `ReorderLevel`, `ShelfLocation` | inventory settings |

### The FK across the tenant / platform boundary

`Product.CatalogMedicineId` points at `CatalogMedicines`, which is **platform-level, has no
`TenantId`, and is not an `ITenantEntity`** (Module 1's data import). That is expected and
correct: the catalogue is shared reference data, and two pharmacies importing Napa 500 produce
two separate `Product` rows pointing at the same catalogue row.

**Nothing about this reference is a reason to put a query filter on the catalogue tables.**
`CatalogIsPlatformLevelTests` asserts they stay unfiltered; `ProductIsTenantScopedTests`
asserts `Product` stays filtered. Both failures are silent in opposite directions — a filtered
catalogue shows nobody anything, and an unfiltered `Product` table shows every pharmacy
everyone else's stock and prices.

The column also distinguishes imported from hand-entered products, which is what a future
"the reference data changed, review your product?" flow would need. Not built.

### Unit configuration rules

Configurable per product, because "piece / strip / box" only fits tablets. A sanitiser is sold
in bottles and cartons, formula in tins and cartons, saline in bags and nothing else.

- `BaseUnitName` is always required.
- If `MidUnitName` is set, `BasePerMid` is required and > 1. If null, `BasePerMid` must be null.
- If `LargeUnitName` is set, `MidPerLarge` is required and > 1. If null, `MidPerLarge` must be null.
- Valid shapes: **base**; **base + mid**; **base + large**; **base + mid + large**.
- A pack may not share a name with the unit it contains — that makes every quantity ambiguous.

Enforced by FluentValidation *and* by check constraints (`CK_Products_MidUnitPairing`,
`CK_Products_LargeUnitPairing`, `CK_Products_MedicineOnlyFields`), so a bypassed validator
cannot write a row nothing can interpret.

#### ⚠ The two-level edge case

**When `MidUnitName` is null but `LargeUnitName` is set, `MidPerLarge` holds BASE units per
large unit** — 24 *bottles* per carton, not 24 strips.

```
with a mid level:     BasePerMid × MidPerLarge     10 pieces × 10 strips = 100 pieces per box
WITHOUT a mid level:  MidPerLarge alone            24 bottles per carton
```

This is the single most likely place in the module to introduce an off-by-a-factor bug: read
as a mid count and multiplied, a carton of 24 silently becomes a carton of 240, and it only
surfaces weeks later as an inventory discrepancy.

It is resolved in **exactly one place**:

```csharp
// Product.BaseUnitsPerLarge
!HasLargeUnit || MidPerLarge is null ? null
    : HasMidUnit ? (BasePerMid ?? 1) * MidPerLarge.Value
                 : MidPerLarge.Value;
```

**No caller multiplies those two columns itself.** Every conversion goes through that
property, and the form's live-summary JavaScript carries the same rule with the same comment.

### Uniqueness

`BrandName` + `Strength`, unique **per pharmacy** — two pharmacies may both stock Napa 500.

Two filtered indexes rather than one, because SQL Server treats NULLs as *equal* in a unique
index: a single index over `(TenantId, BrandName, Strength)` would allow only **one** product
with no strength per pharmacy, and every non-medicine has no strength.

```sql
UX_Products_Tenant_Brand_Strength    WHERE [Strength] IS NOT NULL
UX_Products_Tenant_Brand_NoStrength  WHERE [Strength] IS NULL
```

### Indexes

`IX_Products_Tenant_BrandName` (covering) and `IX_Products_Tenant_GenericName` — both led by
`TenantId`, so a seek lands inside one pharmacy's rows before doing anything else. Plus
`IX_Products_Tenant_CatalogMedicineId` for the import screen's "already imported?" check.

---

## 4. Unit conversion helpers

`src/Core/PMS.Application/Common/Units/UnitConversion.cs`. **Batches, Billing and Reports all
reuse these** — three copies of this arithmetic would drift.

| Signature | Behaviour |
|---|---|
| `int ToBaseUnits(decimal quantity, UnitLevel unit, Product product)` | Converts to base units. Throws `InvalidOperationException` if the product does not define that level, `ArgumentOutOfRangeException` if negative or not a whole number of base units |
| `string FromBaseUnits(int baseUnits, Product product)` | "1 box + 2 strips + 3 pieces", "2 cartons + 5 bottles", "30 bags". Levels contributing zero are omitted |
| `decimal PricePerBaseUnit(decimal price, UnitLevel unit, Product product)` | Unrounded — a strip of 3 at ৳10 is ৳3.333…, and rounding here would lose money on every line |
| `string DescribePacking(Product product)` | "1 box = 10 strips = 100 pieces", "Sold as individual bags only" |
| `int BaseUnitsIn(UnitLevel unit, Product product)` | The multiplier, via `BaseUnitsPerLarge` |

`UnitLevel` is `Base | Mid | Large`.

**Why a missing level throws rather than returning 0.** A UI must only offer levels the
product defines, so reaching it is a programming error — and returning 0 would silently record
a sale of nothing.

**Why a fractional quantity is rejected rather than rounded.** Half a piece is not 0 and not 1;
rounding either way loses or invents stock. Half a *box* of 100 is 50 pieces and is accepted,
because it lands on whole base units.

### The five required test cases

`tests/PMS.UnitTests/Application/Units/UnitConversionTests.cs` — 32 tests:

| Shape | Fixture | Key assertion |
|---|---|---|
| Three-level | Napa, piece/strip/box, 10 and 10 | 123 base → "1 box + 2 strips + 3 pieces"; 207 → "2 boxes + 7 pieces" (empty middle omitted) |
| Base + mid | Monas, 3 per strip | 4 strips → 12; 39 → "13 strips" |
| **Base + large, no mid** | Savlon, 24 bottles/carton | `BaseUnitsPerLarge == 24`, **not 24 × anything**; ৳4,320/carton → ৳180/bottle |
| Base only | saline bag | "30 bags"; "Sold as individual bags only" |
| Undefined level | saline bag | `ToBaseUnits(1, Mid, …)` and `(1, Large, …)` both throw, naming the product |

---

## 5. API endpoints

All require an authenticated tenant user; every command and query carries
`ITenantScopedRequest`, so the pipeline refuses to run one without a resolved pharmacy.

### Queries

| Endpoint | Policy | Notes |
|---|---|---|
| `GET /api/products` | TenantUser | Paginated. `type` (**required**: `Medicine`\|`Other`), `search`, `status` (`Active`\|`Inactive`\|`All`, default Active), `antibioticOnly`, `productType`, `page`, `pageSize` (default 25, capped 200) |
| `GET /api/products/{id}` | TenantUser | 404 for another pharmacy's id — genuinely absent behind the filter, and a 404 reveals nothing about whether it exists elsewhere |
| `GET /api/catalog/medicines/search?q=` | **TenantWriter** | Two-stage forgiving search, below |
| `GET /api/catalog/medicines/{id}` | TenantWriter | One entry, to pre-fill the import form |

The list search uses `LIKE '%term%'` on brand **and** generic — the opposite trade-off from the
catalogue search, on purpose: a pharmacy holds hundreds of products, not 21,714, so the scan is
cheap, and someone typing "paracetamol" expects to find Napa while "handwash" must match
mid-name.

### Commands

| Endpoint | Policy |
|---|---|
| `POST /api/products` | TenantWriter |
| `PUT /api/products/{id}` | TenantWriter |
| `PATCH /api/products/{id}/deactivate` | **TenantAdmin** |
| `PATCH /api/products/{id}/reactivate` | **TenantAdmin** |

`POST` serves **both** paths: a manual entry sends no `CatalogMedicineId`, an import sends one.
Same validation, same write path, so an imported product differs from a hand-entered one only
by the link it carries. `PUT` deliberately has no `CatalogMedicineId` — the link records where
a product came from, and an edit does not rewrite that history.

### The two-stage catalogue search

`SearchCatalogMedicinesQueryHandler`. The catalogue has 21,714 entries with brand names that
are easy to misspell (Ciprocin, Azithral, Sergel). A strict `LIKE` returns nothing on a typo,
which pushes people into manual entry and defeats the point of shipping a catalogue at all.

**Stage 1 — direct match.** Prefix first (`LIKE 'nap%'`), because that is a seekable range on
`IX_CatalogMedicines_BrandName`; brand prefix, then generic prefix, then `%contains%` as a last
resort since a leading wildcard cannot use the index. If this returns ≥ 3 results the answer is
`Exact` and **stage 2 never runs**.

**Stage 2 — fuzzy fallback.** Only when stage 1 finds almost nothing.

```
Bounded in SQL, never loaded whole:
  1. first two characters must match  →  21,714 rows down to tens or low hundreds
  2. name length within ±4            →  cheapest possible proxy for edit distance
  3. TAKE 800                         →  hard ceiling, so no prefix can be unbounded
```

Measured: `napaa` → 190 candidates, worst realistic prefix (`pa`) → 101. The ceiling is the
guarantee; the filters are what make reaching it rare.

Scoring is `FuzzySharp.Fuzz.WeightedRatio`, best of brand and generic, in
`CatalogFuzzyScorer`.

> **Case folding is load-bearing.** FuzzySharp compares the raw strings it is given, and every
> brand name in the catalogue is capitalised — so `napaa` vs `Napa` scored **67**, under the
> threshold, and the fallback returned unrelated medicines instead of the obvious one. The API
> still answered 200 with a plausible list, which is why `CatalogFuzzyScorerTests` exists.

#### The `matchType` contract

| Value | Meaning | UI obligation |
|---|---|---|
| `0` `Exact` | Direct indexed match | Render plainly |
| `1` `Suggestion` | A fuzzy guess contributed | **Must** be labelled "No exact match for '{term}'. Did you mean:" |

Presenting a guess as a match is how somebody imports the wrong medicine, so this is part of
the contract, not a hint.

> **Deviation from the specification:** it serialises as an **integer**, not the string
> `"exact"`/`"suggestion"`. Every enum in this API serialises numerically — the convention
> Module 1 set, which the Razor client and any future mobile client mirror — and making one
> field a string would be a worse inconsistency than the wording. Say the word and I will flip
> the whole API to string enums instead.

#### Threshold tuning

`SimilarityThreshold = 70`, the recommended starting point, and it does real work in both
directions. Verified against real data:

| Term | Result |
|---|---|
| `napaa` | Napa @ **89** ✓ |
| `sergal` | Sergel @ **83** ✓ (Segal @ 91 also, legitimately — one edit away too) |
| `ciproccin` | Ciprocin @ **94** ✓ |
| `xyzabc123`, `zzzzzz` | **0 results** ✓ |

Too low and garbage returns a page of unrelated medicines — worse than nothing, because
someone may import one. Too high and `napaa` returns nothing, which is the problem stage 2
exists to solve. Re-tune against real search logs once there are some.

#### Already-imported entries: **flagged, not hidden**

Each result carries `alreadyImported` and `existingProductId`. Hiding them would leave someone
searching for Napa, finding nothing, and concluding the catalogue lacks it — when in fact
their own earlier import succeeded. The flag links straight to the existing product.

Implemented as a **second small query**, not a join in the projection: `Products` is
tenant-filtered and the catalogue is not, and keeping them apart makes the two regimes obvious
rather than looking as though the catalogue were tenant-scoped.

### Performance measured

| Search | Time | Stage 2 |
|---|---|---|
| `napa`, `seclo`, `monas`, `paracetamol` | **7–12 ms** | did not run |
| `napaa`, `ciproccin` | 144–175 ms | ran |

---

## 6. Key decisions

**One table rather than separate Medicine and Product tables.** Everything downstream works on
integer base-unit quantities and does not care what the unit is. Two tables would duplicate
batches, FEFO, billing and reporting, and every join would need to know which table a line
refers to. The cost is nullable medicine-only columns plus the validation and check constraints
that police them.

**Configurable unit names rather than hardcoded pieces/strips/boxes.** "Piece / strip / box"
only fits tablets. Hardcoding it is what makes staff enter a sanitiser as 24 "strips" of
"pieces", and every stock report inherits the lie. The cost is the two-level edge case, which
is why `BaseUnitsPerLarge` exists.

**A catalogue FK across the tenant / platform boundary.** The alternative — copying catalogue
fields into `Product` with no link — loses the ability to tell imported from hand-entered, and
forecloses a future review-changes flow. The FK is `RESTRICT`, so a catalogue refresh can never
delete a pharmacy's products.

**Prices as current defaults, with the snapshot coming in Billing.** `Product.PricePerBase` is
what to charge *next*. The price actually charged is snapshotted onto the sale line at sale
time, so changing a price here never rewrites what a customer was charged last month. The edit
form says so in as many words.

**Employee price-hiding in the projection, not the view.** See §2.

**The antibiotic flag is confirmed by a human at import.** The catalogue's flag is
machine-derived and provisional (Module 1's classifier, 173 of 1,711 generics, needing review).
The import form pre-sets the checkbox from it and asks a pharmacist to confirm — which is the
point at which a guess becomes a decision, and the reason import is a two-step flow rather than
one click.

---

## 7. Out of scope

- Batches, stock quantities, expiry dates, purchase prices — **Module 3**.
- Bulk CSV import of products.
- Barcode scanning.
- Product images.
- `Category` as a managed table with CRUD — free text for now.
- "Reference catalogue changed, review your product?" sync. The FK makes it possible later.

---

## 8. What Module 3 (Batches) inherits

1. **`Batch` FKs to `Product.Id`.** `Batch` implements `ITenantEntity` too; it does not carry
   its own copy of the product's details.
2. **Quantities are integer base units.** Use `UnitConversion.ToBaseUnits` at the edges — when
   a purchase is entered in cartons, when a sale is rung up in strips — and store base units.
   Never store a decimal quantity, and never a mid- or large-unit count.
3. **`Batch.ExpiryDate` must be nullable.** A non-medicine product may not expire at all —
   diapers, syringes, a thermometer — and forcing a date would have staff invent one.
4. **`Product.IsActive` is a soft delete.** A batch may legitimately reference an inactive
   product: the pharmacy stopped stocking it but still holds stock and history.
5. **Reorder level is in base units** (`Product.ReorderLevel`), so low-stock comparisons need
   no conversion.
6. Deactivating a product does **not** cascade. Existing stock and records are preserved, which
   the confirmation modal states explicitly.
