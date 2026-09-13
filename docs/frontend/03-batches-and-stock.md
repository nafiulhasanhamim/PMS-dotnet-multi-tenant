# Frontend 03 — Batches & Stock

The third frontend module. It follows the layout, design system, API client pattern and auth
mechanism from [01-auth-and-layout.md](01-auth-and-layout.md) and the table, badge, form, modal
and pagination conventions from [02-product-master.md](02-product-master.md).

Backend: [../03-batches-and-stock.md](../03-batches-and-stock.md).

---

## 1. Page inventory

| Route | Page | Authorization | Purpose |
|---|---|---|---|
| `/stock` | `Stock/Index` | TenantUser | Stock by product: search, three filters |
| `/stock/product/{productId}` | `Stock/Detail` | TenantUser | One product, batch by batch |
| `/stock/add` | `Stock/Add` | **TenantWriter** | Record a delivery |
| `/stock/batch/{id}/edit` | `Stock/EditBatch` | **TenantWriter** | Correct a batch's details |
| `/stock/batch/{id}/adjust` | `Stock/AdjustBatch` | **TenantWriter** | Change a quantity, with a reason |
| `/stock/batch/{id}/history` | `Stock/BatchHistory` | **TenantWriter** | Every change to one batch |

`Program.cs` authorizes the `/Stock` folder to `TenantUser`; each write page then carries its own
`[Authorize(TenantWriter)]`. Verified: an Employee hitting any of the four write pages gets one
redirect to `/denied`, not a loop.

The page for a product's stock is `Detail.cshtml`, not `Product.cshtml`. Razor names a page model
after its file, and `ProductModel` would have collided with the `ProductModel` API contract —
the first attempt was a class called `ProductModel_Page`, which is the kind of name that only
exists to dodge a collision. Renaming the page was the better fix.

One sidebar entry, in `NavRegistry` — not by editing `_Layout.cshtml`:

```csharp
new NavItem("Stock", "/Stock/Index", "layers", MatchPrefix: "/stock"),
```

Visible to every role. An Employee has to be able to answer "have we got any?" without being able
to change the answer.

---

## 2. Why the list groups by product and the detail page lists batches

**A pharmacist scanning the shelf thinks "how much Napa do I have", not "how much of batch
B-2451".** A batch-level list would be the same information in the order least useful for the
question actually being asked — several rows per product, none of them answering it, and the
answer only available by adding them up.

So `/stock` is one row per product with aggregates across its batches, and the batch-level view
is one click away. The batch becomes the unit of attention exactly when it starts to matter:
which one to sell next, which one is about to expire, and what each of them cost.

### The list columns

Product name, generic name, total quantity, batch count, nearest expiry, status.

**Total quantity is sellable stock**, and expired stock appears beneath it in red as
"+ 40 pieces expired" when there is any. This is the one place the two numbers have to be shown
separately rather than summed — see the backend note; the short version is that summing them
shows an unsellable shelf as healthy, and dropping them loses the fact that there is something
to dispose of.

**Nearest expiry** is red once expired, amber inside the 90-day window, plain otherwise, and an
em dash for products whose stock does not expire. **The state comes from the API**, not from a
comparison in the view: the amber window belongs to the pharmacy and moves to Settings later, so
a view that derived it would have to be found and changed again. Every cell also carries a
`title` — "Expires in 46 days", "Expired 12 days ago" — so the state is available to a screen
reader and to anyone who cannot tell the two warm colours apart.

**Status badge**: red "Out of stock", amber "Low", muted "OK". Also server-computed, for the same
reason plus one more: three clients would eventually disagree about whether *equal* to the
reorder level counts as low. It does.

### The detail page

Header with the antibiotic and type badges, a back link, then a summary panel — total, batch
count, nearest expiry, reorder level with a "Below" flag — and then two tables.

**Active batches, in FEFO order, with a sentence above them saying so:** "In the order they
should be sold — soonest expiry first, so the top row is the one to sell next. Stock that does
not expire comes last." The ordering is invisible unless it is named, and a pharmacist who does
not know the top row is the one to sell has a sorted table and no rule.

This list is **not** paginated. A product holds a handful of live batches at a time.

**Depleted batches** are in a collapsed `<details>` section below: "N depleted batches — sold
out, kept as the record behind past sales", paginated at 10. Kept and shown rather than hidden
because they are the cost and expiry behind sales that already happened, and a recall names their
number. Collapsed because they accumulate for as long as the pharmacy trades. The two lists are
ordered differently on purpose — active by expiry (what to sell next), depleted newest first
(recent history is what anyone opening that section is looking for).

An expired batch that still holds stock sits at the **top** of the active list, in red. That is
why `Fefo` separates its filter from its ordering: this page wants the ordering without the
sellable filter.

### Role-dependent rendering, verified

| | Cost column | Header button | Row actions | History page |
|---|:---:|---|---|:---:|
| Admin | ✓ | Add stock | Edit, Adjust | ✓ |
| Pharmacist | ✓ | Add stock | Edit, Adjust | ✓ |
| Employee | **absent** | none | none | **refused** |

The cost column is absent for an Employee because **the API withholds the value** — the column is
omitted so there is no empty column, not as the control.

---

## 3. How the unit dropdowns are populated

**From the selected product, never from a fixed list.** For Napa the dropdown offers piece,
strip and box; for handwash, bottle and carton; for a saline bag, bag alone.

`StockPresentation.UnitOptions(product)` builds the list from `BaseUnitName`, `MidUnitName` and
`LargeUnitName`, skipping the levels a product does not define. Each `<option>` carries the
number of base units it represents as a data attribute:

```html
<option value="Mid" data-unit-name="strip" data-base-units="10">strip</option>
<option value="Large" data-unit-name="box" data-base-units="100">box</option>
```

Those counts come from `Product.BaseUnitsPerLarge`, so the two-level trap is resolved server-side
and the page never multiplies `BasePerMid` by `MidPerLarge` itself. Offering a level the product
does not have would produce a quantity the server has to reject, and offering "strip" for a
saline bag invites somebody to guess what one is.

`wwwroot/js/stock-form.js` reads those attributes for the live helper text:

- **"= 200 pieces total"** under the quantity, recalculated on every keystroke. This is the
  module's best defence against a mistyped pack size: 20 strips and 200 strips look almost
  identical in a number input and unmistakable in that sentence.
- **"= 0.80 per piece"** under the price.
- A quantity that does not divide into whole base units says so before the round trip, rather
  than being rounded.

### Changing the product reloads the page

Deliberately, rather than rebuilding the form in JavaScript. The unit dropdowns, whether an
expiry date is required, and the panel showing what is already in stock all come from the server
for that specific product. Rebuilding them client-side would be a second implementation of rules
the server owns — including the packing arithmetic, which is the piece of this system most likely
to be got wrong.

The product picker is a **filter box over a real `<select>`**, not a custom autocomplete widget:
keyboard behaviour and screen-reader support come for free, and a pharmacy holds hundreds of
products rather than thousands. The filter searches the server (`?pq=`) so it scales past the
100 the picker shows at once, and when the list is partial the helper text says so instead of
silently truncating. Arriving from a product's stock page locks the picker, so the context cannot
change underneath the form.

---

## 4. The conditional expiry requirement

Whether an expiry date is required depends on the product, so the form cannot know until one is
chosen — and once it is, three things change together:

| | Medicine | Everything else |
|---|---|---|
| Asterisk | shown | absent |
| Helper text | "Napa 500 is a medicine, so this is required." | "Leave blank if this product doesn't expire." |
| Client validation | enforced | skipped |

All three come from `Model.ExpiryRequired`, which is `Product.ProductType == Medicine` — one
source, so the asterisk cannot disagree with the validation.

**The server decides.** `CreateBatchCommandHandler` enforces the same rule from the product type
and returns a field error against `ExpiryDate`, which lands under the input through
`PmsPageModel.ApplyProblem`. The client check only saves a round trip.

A diaper or a tin of formula saves with a blank expiry and shows "—" in the column. A medicine
with a blank expiry is refused with a field-level error, not a banner. Both verified.

The edit page differs in one respect and says so on screen: **a past expiry is allowed there.**
A pharmacy recording stock already on its shelves will have packs that expired last month, and
that is exactly the stock somebody has to pull. On a *new* delivery a past date is a typo and is
refused.

---

## 5. The write pages

### Add stock (`/stock/add`)

Three cards — the delivery, quantity and cost, where it came from — then a panel showing what is
**already in stock** for the chosen product, with its existing batch numbers. Somebody entering a
delivery of Napa should be able to see that B-100 already exists *before* they type it, rather
than being refused after.

The **loss warning** is an amber panel with a "continue anyway" checkbox, shown by JavaScript
when the entered cost per base unit exceeds the product's sale price. Non-blocking, because a
pharmacy really does buy above its own list price and reprice afterwards — refusing the entry
would leave the stock unrecorded, which is worse than recording it flagged. The server returns
its own `sellsAtALoss` flag regardless, which catches the case where the product was repriced
between the form loading and the post; that comes back in the success message.

On save: redirect to the product's stock page with "Batch added. 200 pieces of Napa 500
recorded." The message names the quantity in **base units**, because that is what the pharmacy now
holds and it is not the number that was typed — "2 cartons" saved as 48 bottles is exactly the
conversion somebody wants confirmed.

`data-pms-guard` disables the submit with a spinner: a second POST of "add batch" is a second
delivery.

### Edit batch (`/stock/batch/{id}/edit`)

Editable: batch number, expiry, manufacture date, purchase price, supplier, notes.

**Quantity is rendered read-only, with the hint and a link:** "Quantity can only be changed
through a stock adjustment, so the change is recorded with a reason. → Adjust stock". Initial
quantity is read-only too, with "What arrived. Never changes — new stock is a new batch."

That is the whole point of this page. A quantity editable here would be a quantity that changed
with no reason recorded, and the reason is the only part anybody wants six months later. The form
posts the batch's *current* quantity, which the server accepts as the no-op it is — and refuses
anything else, so the read-only field is a rule rather than a decoration.

### Adjust stock (`/stock/batch/{id}/adjust`)

Header shows the context: product, batch number, current quantity.

A radio group with three options, each labelled with *when* to use it rather than just what it
does — "Add stock — a miscount on arrival, or a pack coming back", "Remove stock — damage,
breakage, expired disposal", "Set correct quantity — you have counted the shelf".

**"Set correct quantity" changes what the number means**, so the label and the live preview change
with it: the field becomes "What is the correct quantity?" and the preview reads
"180 → 175 (a change of −5)". Without that, somebody types 175 meaning "remove 175". The preview
also refuses in advance when a removal exceeds what the batch holds.

**Reason** is a required text input with quick-pick buttons — Damage, Breakage, Counting
correction, Expired disposal, Other. Buttons that *fill an editable field*, not a dropdown that
replaces it: a fixed list gets "Other" selected for exactly the cases worth reading later, and the
detail somebody adds after clicking a quick pick is the part worth keeping.

A **confirmation modal** guards the save, repeating the product, the batch, the current quantity
and that adjustments cannot be deleted. This is the one screen that changes a number nobody else
will re-check.

### The wrong-screen guard, and why it exists

When the batch has expired or expires within 30 days, an amber panel appears **above** the form,
before the confirmation:

> **This batch expired on 30 Aug 2026.**
> If new stock has arrived, don't add it here — record it as a **new batch**, so it keeps its own
> expiry date and cost. Removing stock from this batch (disposal) needs no confirmation.

It catches one specific, common and expensive mistake: fresh stock arrives and, instead of
creating a batch, somebody adds the quantity to the batch already on the screen. The new stock
then silently inherits the old batch's expiry date and cost. It will be flagged for disposal
months early or sold as expired, and the margin on it is wrong either way — and nothing about the
screen afterwards looks wrong.

Three deliberate details:

- **The link goes to the add form with the product pre-filled**, so the right thing to do is one
  click, not a navigation the person has to work out.
- **A checkbox is required**, and the API refuses the addition without it. A warning that lives
  only in the page is not a guard — the same mistake through the API would go straight through.
- **Removals are exempt.** Removing from an expired batch is the correct thing to do with it, and
  warning there would train people to click through warnings.

Thirty days rather than the ninety-day amber window: adding to a batch expiring in two months is
ordinary, a miscount corrected. Adding to one expiring this month almost always means the wrong
screen.

### Adjustment history (`/stock/batch/{id}/history`)

Date, type badge, change, before → after, reason, and who. Paginated at 20.

A separate route rather than a section on the edit page, for two reasons: the edit page is
already long, and this is the one page in the module an Employee may not see at all. A section
would have meant hiding part of a page they can otherwise open — and a separate route means the
`[Authorize]` attribute does the work.

The author's name is resolved at read time from the user id rather than copied onto the row when
it was written, so a corrected name is corrected everywhere. The id is the record; the name is
the display.

---

## 6. New shared components

| Component | Purpose |
|---|---|
| `StockPresentation` | Status and adjustment badges, the expiry cell (text + CSS + title), `UnitOptions`, `BaseUnitsIn`, `QuickReasons` |
| `wwwroot/js/stock-form.js` | Unit-aware live totals, price-per-base-unit, the loss warning, adjustment-mode labels and preview, quick-pick reasons, the product filter. No jQuery |
| `_NavIcon` addition | `layers` — stacked layers, several batches of one product |
| `Api/StockContracts.cs` | The wire contracts, declared locally like every other module's |

Nothing here replaces a Module 1 or 2 component: the alert, empty-state, badge, table, modal,
page-header and `_ApiPagination` patterns are used as they are.

The JavaScript is a convenience over a form that works without it. Every rule it expresses is
also enforced by the API; if the file fails to load, the form still submits and the server still
refuses what it should.

---

## 7. Out of scope

- Selling from a batch — Module 5. The detail page says so where it will appear.
- Purchase entry creating batches — Module 4.
- Expiry and low-stock alert panels, and a dashboard — Module 6. The states this module renders
  (`ExpiryState`, `StockStatus`) are the ones those panels will read.
- Barcode scanning; multi-location transfers.
