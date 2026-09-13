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

`BrandName` + `Strength` + `DosageForm`, unique **per pharmacy** — two pharmacies may both
stock Napa 500.

> **Changed by migration 010.** It was brand + strength until the bulk import showed that a
> pharmacy could then hold only one of the six dosage forms of Nyclobate 0.05%. See
> [6d](#6d-what-identifies-a-product) for the reasoning, and for why the three parts are
> materialised as one persisted computed column rather than covered by four filtered indexes.

```sql
[IdentityKey] AS (CONCAT([BrandName], CHAR(31),
                         ISNULL([Strength],   N''), CHAR(31),
                         ISNULL([DosageForm], N''))) PERSISTED NOT NULL

UX_Products_Tenant_Identity   UNIQUE ([TenantId], [IdentityKey])
```

Script 007 originally needed *two* filtered indexes rather than one, because SQL Server treats
NULLs as *equal* in a unique index: a single index over `(TenantId, BrandName, Strength)` would
allow only **one** product with no strength per pharmacy, and every non-medicine has no
strength. With two nullable parts in the key that approach needs four.

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

## 6b. Bulk import from the reference catalogue

Onboarding is the reason this exists. A pharmacy joining the platform has to enter two hundred
or more products before the system is usable at all, and doing that one form at a time is the
difference between an afternoon and a fortnight. The single-product path stays — it is the
right one for adding one thing to an established catalogue.

**`POST /api/products/bulk-import`**, Admin or Pharmacist, up to **200 rows** per request.
The screens are `/medicines/import` (search, tick), `/medicines/import/setup` (shared units,
review grid, save) and `/medicines/complete-setup` (fill in prices later).

### All-or-nothing, and why

Every row is validated independently. If **any** row fails, nothing is written and the response
carries a result per row saying which.

A partly imported catalogue is the outcome worth ruling out. The pharmacy would have no way to
tell which of two hundred medicines arrived without checking each one, and a second attempt at
the same selection would then collide with whatever the first attempt managed — turning one
clear failure into a hunt. Refusing the batch leaves exactly one action: fix the named rows and
send it again.

The transaction is a single `SaveChanges` over all the inserts, which EF wraps in its own
transaction. Not an explicit `BeginTransaction`: the context enables retry-on-failure and
`SqlServerRetryingExecutionStrategy` refuses a user-initiated transaction — the same trap
documented in [03-batches-and-stock.md](03-batches-and-stock.md).

### `Succeeded` and `Errors` mean different things

`Succeeded` on a row means *a product was created*. When the batch is refused, that is false
for **every** row, including the ones that were perfectly fine. The rows to *fix* are the ones
with `Errors`, and the review grid marks those — keying the markers off `Succeeded` would tell
somebody to fix rows that need no fixing.

### What is checked per row

| Check | Why it cannot be a validator rule |
|---|---|
| The catalogue id exists | Needs the catalogue |
| Not already imported by this pharmacy | Needs this tenant's products |
| The identity — brand + strength + dosage form — does not collide | Same |
| Not duplicated **within the batch** | Two rows would both pass the database check and then lose to the unique index, reporting a race that was really a duplicate selection |

Unit pairing and the medicine-only field rules are **not** re-implemented: a small adapter
presents each row as an `IProductWriteRequest` so `ProductWriteRules` — the same rules the
single-product form uses — validates it. Two copies would drift.

Three batch reads serve the whole request rather than three per row: the catalogue entries, the
existing brand+strength keys, and the already-imported catalogue ids. Two hundred rows would
otherwise be six hundred round trips.

Identity fields come from the catalogue row, never from the request, so a caller cannot import
under one catalogue id with another medicine's name. The **antibiotic flag is the exception**
and is taken from the request: the catalogue's flag is machine-derived and provisional, and the
grid pre-ticks it and tints those rows so a pharmacist confirms it. That is the moment a guess
becomes a decision.

---

## 6c. `IsSetupComplete`, and "Save without prices"

### The field

`Product.IsSetupComplete` is true when every unit level the product defines has a price:

```csharp
IsSetupComplete =
    PricePerBase is not null
    && (!HasMidUnit || PricePerMid is not null)
    && (!HasLargeUnit || PricePerLarge is not null);
```

A product sold only in bags needs one price, not three. Note it asks whether the price is
**present**, not whether it is positive — zero is a legitimate price for a sample.

**`PricePerBase` is nullable**, which it was not before this feature. The alternative was
storing zero for "not priced yet", and that was rejected: zero is a real price, and a column
that cannot tell "free" from "nobody has decided" will eventually be asked to. Sooner or later
something sells for nothing. Migration `009_AddProductSetupComplete.sql` makes the column
nullable, adds the flag, and backfills it with the rule above rather than assuming every
existing row is complete.

### Computed and stored, not derived on read

It is a pure function of columns already in the row, so a computed property would always agree.
It is stored because the medicines list **filters** on it, a banner **counts** it, and Module 5
will **check** it — and none of those can put a C# expression in a `WHERE` clause. Stored, it is
one predicate against a filtered index holding only the incomplete rows; derived, it means
loading every product to ask.

The cost of storing it is that it can go stale, so nothing outside the entity writes it:
`RecomputeSetupComplete` runs in the constructor and after every change to a price **or a unit
level**. That last part is the non-obvious half — adding a bulk pack to a priced product makes
it incomplete again, so `SetUnits` recomputes too. `ProductSetupCompleteTests` pins all of it,
including that the property has no public setter.

### Forward dependency on Module 5

**A product with `IsSetupComplete = false` must not be sellable, and nothing enforces that
yet** — there is no sale path to enforce it in. Module 5 (Billing) is where the check belongs,
at the point a sale line is added.

Until then the flag is advisory, and the UI carries the weight: an amber "Setup incomplete"
badge on every list row, a "Setup incomplete" option in the status filter, and a dismissible
banner on the medicines list with the count and a link to `/medicines/complete-setup`. That is
deliberately noisy. An unpriced product that looked ordinary would be sold at whatever price the
till invented.

### Why "Save without prices" is an option and not a validation failure

The bulk setup screen offers two buttons: **Save all**, which requires every price, and **Save
without prices**, which creates the products with `IsSetupComplete = false` after a confirmation
naming the consequence.

A pharmacy onboarding two hundred medicines frequently does not have its price list to hand —
prices come from the supplier's invoice, which arrives with the stock, not with the decision to
stock it. Refusing the import until every price is known leaves the catalogue **empty**, which
is worse than a catalogue that is complete except for prices and says so on every screen. An
empty catalogue means nothing can be received, nothing can be searched, and the pharmacy cannot
start using the system at all.

So the choice is offered explicitly rather than being reached by leaving fields blank and
hoping. The confirmation says what it costs, the badge says it on every row afterwards, and the
banner keeps saying it until the prices are in.

One rule follows from this and is worth stating: **the pack prices are required only once a base
price is given.** Either you are pricing a product — in which case every level it has needs a
price, or it would be sellable at some levels and not others — or you are not pricing it at all.
A half-priced product is the state genuinely worth forbidding, and that is what
`ProductWriteRules` now forbids. The single-product form still requires the base price outright:
somebody filling in one product knows what it sells for.

### Completing setup later

`/medicines/complete-setup` renders the same grid component as the bulk setup screen — the same
copy-down affordance, the same keyboard order — because it is the same problem at a different
moment. It posts to **`POST /api/products/prices`**, which touches prices and nothing else.

Not `PUT /api/products/{id}` in a loop: that would be one request per row, each carrying the
product's entire definition — every unit name, count, category and shelf location — round-tripped
for the sake of one number, with any of them corruptible by a stale form. It is all-or-nothing
too, for the same reason as the import: a half-applied grid is one nobody can read.

---

## 6d. What identifies a product

**Brand name, strength and dosage form**, unique per pharmacy.

Dosage form was not part of it until the bulk import made the consequence impossible to
ignore. The reference catalogue holds **548 brand+strength groups with more than one entry**,
differing only by dosage form. The extreme case is Nyclobate 0.05%, which exists six times:

| Catalogue id | Brand | Strength | Dosage form |
|---|---|---|---|
| 13881 | Nyclobate | 0.05% | Lotion |
| 13882 | Nyclobate | 0.05% | Topical Spray |
| 13883 | Nyclobate | 0.05% | Shampoo |
| 13884 | Nyclobate | 0.05% | Scalp Solution |
| 13885 | Nyclobate | 0.05% | Ointment |
| 13886 | Nyclobate | 0.05% | Cream |

A pharmacy stocks several of those at different prices. Under the old identity it could hold
exactly one, and a bulk import that included two of them was refused outright — which is how
this surfaced. Importing one at a time hid it: you would hit it occasionally and blame the
catalogue. Ticking twenty rows from one search made it near-certain.

### One definition, two places that must agree

`ProductKeys.Identity(brandName, strength, dosageForm)` lower-cases and trims each part and
joins them with a unit separator. The database materialises the same expression as a
**persisted computed column** and puts one unique index over it:

```sql
[IdentityKey] AS (CONCAT([BrandName], CHAR(31),
                         ISNULL([Strength],   N''), CHAR(31),
                         ISNULL([DosageForm], N''))) PERSISTED NOT NULL

CREATE UNIQUE NONCLUSTERED INDEX [UX_Products_Tenant_Identity]
    ON [dbo].[Products] ([TenantId], [IdentityKey])
```

The bulk import checks up to two hundred rows against a key set held in memory, so the two
implementations have to reach the same verdict. Sharing one expression means they agree by
construction rather than by coincidence — two copies would eventually differ over a trailing
space and report a clash the constraint does not, or miss one it does.

The separator is a unit separator, not a space or a pipe: all three parts can contain those,
and without a distinct separator `"Napa" + "500 mg"` would collide with `"Napa 500" + "mg"`.
Case-insensitivity comes from the database collation (`SQL_Latin1_General_CP1_CI_AS`), which is
why the C# side calls `ToLowerInvariant` — invariant, because a Turkish locale lower-cases I
differently and whether two products collide must not depend on where the server runs.

### Why a computed column rather than more filtered indexes

SQL Server treats NULLs as **equal** in a unique index. That is why script 007 needed *two*
filtered indexes rather than one: an index over `(TenantId, BrandName, Strength)` would have
allowed only one product with no strength per pharmacy, and every non-medicine has no strength.

Adding a second nullable column to the key doubles that. Covering every combination without a
hole takes **four** filtered indexes — both present, strength only, form only, neither — and a
third nullable component later would need eight. The computed column collapses the absent parts
with `ISNULL` and needs one index, with no combinations to enumerate and no hole possible.

The column is deliberately **not mapped in the EF model**: nothing in the application reads it,
and the duplicate check compares the three real columns instead, which keeps the query seekable
on `IX_Products_Tenant_Brand_Strength`.

### Migration 010

`010_ProductIdentityIncludesDosageForm.sql` adds the column and the index, then drops the two
from script 007 — in that order, so the table is never briefly unprotected. It also recreates
the brand-name lookup as a **plain** index, because the unique index it dropped was what made a
product search seekable.

The new rule is strictly weaker than the old one: it permits everything the old one did plus
rows differing by dosage form, so no existing data can violate it. The script checks anyway and
raises rather than proceeding, because a migration that silently could not create its own index
would leave the table with no uniqueness guarantee at all.

### What the messages say now

Every conflict names all three parts, through `ProductKeys.Describe`:

> You already have 'Nyclobate' at 0.05% (Cream).

The dosage form is in there because without it the message was actively misleading: somebody
looking at a cream and a lotion could see they differed and had no way to learn why the second
was refused. The bulk import's batch-duplicate message got simpler for the same reason — two
rows now collide only when they are genuinely the same product, which does happen (Milk of
Magnesia 400 mg/5 ml appears four times, all Oral Suspension).

---

## 7. Out of scope

- Batches, stock quantities, expiry dates, purchase prices — **Module 3**.
- Bulk import from a **CSV file**. Bulk import from the reference catalogue is
  built — see 6b — but reading a spreadsheet is a different problem: it has no
  catalogue ids to trust and would need column mapping and its own error report.
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
