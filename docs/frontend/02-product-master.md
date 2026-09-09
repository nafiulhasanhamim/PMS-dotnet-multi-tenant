# Frontend 02 — Product Master

The second frontend module. It follows the layout, design system, API client pattern and auth
mechanism set out in [01-auth-and-layout.md](01-auth-and-layout.md) and **introduces no new
patterns** — the page headers, tables, badges, forms, modals and pagination are the ones
already there.

Backend: [../02-product-master.md](../02-product-master.md).

---

## 1. Page inventory

| Route | Page | Authorization | Purpose |
|---|---|---|---|
| `/medicines` | `Medicines/Index` | TenantUser | Medicines list: search, status, antibiotics-only |
| `/medicines/import` | `Medicines/Import` | **TenantWriter** | Step 1 of import — search the shared catalogue |
| `/medicines/create` | `Medicines/Create` | **TenantWriter** | Add a medicine; step 2 of import with `?catalogId=` |
| `/other-items` | `OtherItems/Index` | TenantUser | Non-medicine list: search, type, status |
| `/other-items/create` | `OtherItems/Create` | **TenantWriter** | Add a non-medicine |
| `/products/{id}` | `Products/Detail` | TenantUser | One product in full |
| `/products/{id}/edit` | `Products/Edit` | **TenantWriter** | Edit either kind |

`TenantWriter` is Admin **or** Pharmacist — a new policy this module adds, mirroring the API's.
Deactivate is Admin-only and checks `TenantAdmin` at the button.

Folder conventions in `Program.cs` authorize `/Medicines`, `/OtherItems` and `/Products` to
`TenantUser`; each write page then carries its own `[Authorize(TenantWriter)]`. An Employee
hitting `/medicines/create` gets one redirect to `/denied` — verified, not a loop.

---

## 2. Two screens, one table

Both lists read the same `Product` table through the API's `type` filter. They are separate
screens because **the useful columns differ completely**:

| Medicines | Other items |
|---|---|
| Brand name, Generic name, Company, Dosage form, Strength, Price, **Antibiotic**, Status | Name, **Type**, Company, **Base unit**, Price, Status |

Generic name, strength, dosage form and the antibiotic flag are all empty for a diaper.
Rendering four dead columns on every row is noise, and it also invites someone to try filling
them in. The non-medicine screen gains a product-type dropdown instead, and drops the
antibiotics-only toggle, which would always be empty.

Both are registered in `NavRegistry` — the reusable component from Module 1 — not by editing
`_Layout.cshtml`:

```csharp
new NavItem("Medicines",   "/Medicines/Index",  "pill", MatchPrefix: "/medicines"),
new NavItem("Other items", "/OtherItems/Index", "box",  MatchPrefix: "/other-items"),
```

Both visible to every role. An Employee sees them and can look; what changes by role is what
the page offers, and the API is what enforces it.

### The antibiotic badge

A red `AB` pill in the list, a full `Antibiotic` badge on detail. Deliberately prominent: this
flag decides who may sell the product and whether a prescription must be captured, so it is
not a subtle piece of metadata. Never colour alone — the text says `AB`, and the cell carries a
`title`.

### Role-dependent rendering, verified

| | Price column | Header buttons | Row actions |
|---|:---:|---|---|
| Admin | ✓ | Import from catalog, Add manually | View, Edit, Deactivate |
| Pharmacist | ✓ | Import from catalog, Add manually | View, Edit |
| Employee | **absent** | none | View |

The Price column is absent for an Employee because **the API withholds the value** — the
column is omitted so there is no empty column, not as the control. Prices *are* shown on the
detail page to every role: someone at the counter needs to answer "how much is this?".

---

## 3. Unit setup — presets and custom mode

The hardest part of this form to get right, because a wrong number here is invisible and
expensive.

### Presets

A "How is this sold?" dropdown rather than five raw inputs, pre-selected by product type:

| Preset | Shape | Pre-selected for |
|---|---|---|
| Piece → strip → box (standard medicine) | base + mid + large | Medicine |
| Unit + bulk pack | base + large | MedicalSupply, BabyCare, PersonalCare |
| Single unit only | base | everything else |
| Custom | all fields, nothing assumed | — |

Five raw inputs is where somebody types a strip count into a carton field. The preset sets up
the shape; every field stays editable.

### Enabling checkboxes

Each optional level has a checkbox, and unticking one **disables and clears** its fields:

```
Smallest unit you sell *        [ bottle          ]
The individual item a customer can buy.

☐ This product also comes in a middle-size pack
   Middle unit name             [ ............. ]  (disabled)
   How many bottles in one?     [ ..... ]           (disabled)

☑ This product also comes in a bulk pack
   Bulk unit name               [ carton        ]
   How many bottles in one carton?  [ 24 ]

ℹ  1 carton = 24 bottles
```

A checkbox says "leaving this off is a valid answer" where an empty text box looks like a field
someone forgot — and a saline bag genuinely has no strip and no carton.

Clearing on untick matters too: a stale "strip / 10" left behind by someone who changed their
mind would otherwise be posted as real configuration. `ProductFormInput.Normalise()` repeats
the clearing server-side, because a browser's handling of disabled fields is not something to
rely on.

### The live summary line

Recalculates on every keystroke: **"1 box = 10 strips = 100 pieces"**, "1 carton = 24 bottles",
"Sold as individual bags only".

This is the module's best defence against the two-level trap. Typing **240** instead of 24 is
invisible in a number input and unmissable in that sentence.

The question text also names the unit actually being counted, and changes with the middle-level
checkbox:

- mid level on → "How many **strips** in one carton?"
- mid level off → "How many **bottles** in one carton?"

Whether the answer is strips or bottles decides the number typed, and nobody reads a tooltip.

> **The rule in `wwwroot/js/product-form.js` must match the server's
> `Product.BaseUnitsPerLarge`:** with no middle level, the bulk count is *already* base units
> and must not be multiplied. Both copies carry the same comment. `ProductPresentation.
> DescribePacking` is a third copy for the server-rendered initial state — the detail page uses
> the API's own `PackingSummary` instead, so there is one authority once a product exists.

### Pricing and inventory labels

Every label uses the product's own unit names, live: "Price per bottle", "Price per carton",
"Reorder level (in bottles)". Generic labels ("Price per mid unit") are how someone prices a
carton as if it were a bottle.

---

## 4. The catalogue import flow

Two steps, on purpose.

### Step 1 — `/medicines/import`

A prominent search box over 21,714 shared entries. Results show brand, generic, manufacturer,
strength, dosage form, and one action per row.

Three distinct states:

**Exact** — results rendered plainly, no framing.

**Suggestion** — an amber banner above the table:

> **No exact match for 'sergal'. Did you mean:**
> These are close matches, not exact ones. Check the generic name and strength before adding.

Driven by the response's `matchType`, not guessed from result count. Presenting a fuzzy guess
as a confirmed match is how someone imports the wrong medicine.

**Nothing found** — an empty state, *not* a list of irrelevant near-misses:

> No matches found.
> If this medicine isn't in the catalog, you can add it manually. → **[Add manually]**

The manual-entry link also stays visible in the *results* footer ("Not the medicine you
wanted? Add it manually"), because the medicine genuinely might not be in the catalogue.

**Already-imported rows** are shown muted with "Already in your catalog" and a **View yours**
link to the existing product — flagged, not hidden. Hiding them would leave someone searching
for Napa, finding nothing, and concluding the catalogue lacks it.

### Step 2 — `/medicines/create?catalogId={id}`

**Review before save.** The form is visually split so it is obvious what came from where:

```
┌─ From the reference catalog ──────────────────────────┐
│  (pre-filled, editable)                                │
│  Brand name / Generic name / Company / Strength /      │
│  Dosage form                                           │
│                                                        │
│  ☑ This is an antibiotic (regulated sale)              │
│     Suggested from the reference catalog — please      │
│     confirm this is correct.                           │
└────────────────────────────────────────────────────────┘

┌─ Your pharmacy's details ─────────────────────────────┐
│  (required — the catalog doesn't have these)           │
│  unit configuration · prices · reorder level           │
└────────────────────────────────────────────────────────┘
```

Plus a note at the top: *"Prices and pack sizes aren't in the reference catalog — please enter
yours below."*

**Why review rather than one-click import.** Two reasons, both real:

1. The catalogue has no idea what this pharmacy charges or how it packs anything, so a form is
   unavoidable regardless.
2. The antibiotic flag it carries is **machine-derived and provisional** — Module 1's classifier
   working from drug-class and generic-name keywords, explicitly needing human review. The
   checkbox is pre-set from it and the helper text asks a pharmacist to confirm. That is the
   moment a guess becomes a decision, and it is deliberately hard to skip past.

Catalogue fields stay editable: a pharmacy may know the manufacturer changed, or want a shorter
brand name on its own labels. The saved product records `CatalogMedicineId` either way, so
provenance survives the edits.

`/medicines/create` with no `catalogId` is the same page as a blank form — one page, one POST,
one set of validation.

---

## 5. Form behaviour

Following Module 1's conventions:

- Required fields marked with a red asterisk; labels above inputs.
- **Server-side validation is authoritative.** The API's field errors land under the matching
  input via `ProductWritePageModel.ModelStateKeyFor`, which maps the API's flat names
  (`GenericName`) onto the form's bound names (`Input.GenericName`). Without that translation
  the server's messages would arrive attached to nothing and render as a page-level banner.
- A duplicate brand + strength arrives as a **409 with no `errors` dictionary**, so
  `ConflictField` attaches it to the name input: *"You already have 'Napa 500' at 500 mg."*
- Client-side checks mirror the server's to save a round trip; they never replace it.
- Submit disables with a spinner via `data-pms-guard` — a second POST of "create product" is a
  second product.
- Success redirects to the detail page with a green banner.
- On edit: an amber inline warning when the antibiotic flag is toggled **away from its saved
  value** (toggling back hides it again), and a muted note that price changes apply to future
  sales only.

### Medicine-only fields are not rendered at all

On the other-items form, `GenericName`, `Strength`, `DosageForm` and the antibiotic checkbox
are **absent from the HTML** — not hidden, not disabled. A field that does not exist cannot be
filled in by accident, and a diaper with a strength is exactly the corruption this module
exists to prevent. Verified.

`Normalise()` also nulls them server-side, and the API rejects them outright, so there are
three layers and the outermost is the cheapest.

---

## 6. New shared components

Reusable by later modules:

| Component | Purpose |
|---|---|
| `_ApiPagination.cshtml` + `ApiPaginationModel.For<T>(…)` | Pagination over a **server-paged** `ApiPage<T>`. Visually identical to Module 1's `_Pagination`; differs only in where the totals come from — these endpoints page in the database |
| `_ProductForm.cshtml` | The whole product form. Driven by `ProductFormViewModel`; renders medicine fields conditionally |
| `ProductFormInput` + `UnitPreset` | The form's bound shape, with `Normalise()`, `ToCreateRequest()`, `ToUpdateRequest()`, and factories from a product / a catalogue entry / a blank type |
| `ProductWritePageModel` | Base for the three write pages: API-field-to-form mapping, conflict routing, local validation |
| `ProductPresentation` | Type labels, type/status badges, `DescribePacking`, `PluralUnit` |
| `wwwroot/js/product-form.js` | Presets, level enabling, live summary, unit-aware labels, antibiotic-change warning. No jQuery |
| `_NavIcon` additions | `pill`, `box` |
| `ApiPage<T>` | The API's paged envelope (`GridResult<T>` server-side) |

Nothing here replaces a Module 1 component; the alert, empty-state, badge, table, modal and
page-header patterns are used as they are.

---

## 7. Out of scope

- Bulk CSV import, barcode scanning, product images.
- Managing `Category` as a list — free text for now.
- Stock levels, batches and expiry on the detail page — **Module 3**. The detail page says so
  where they will appear.
- A "reference catalogue changed" review prompt.
