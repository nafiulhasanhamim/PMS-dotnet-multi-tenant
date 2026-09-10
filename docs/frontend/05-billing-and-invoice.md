# Frontend 05 — Billing & Invoice

Four pages, one of which matters more than every other screen in the product put together. A
cashier uses `/billing` for hours; everything else here is used a few times a day.

Backend rules, the two exact calculations and the access matrix are in
[`docs/05-billing-and-invoice.md`](../05-billing-and-invoice.md). This document is about the
screens.

---

## 1. Page inventory

| Route | Page | Access | Purpose |
|---|---|---|---|
| `/billing` | `Pages/Billing/Index` | Any tenant user | The till. Search, cart, discount, cash, complete. |
| `/sales` | `Pages/Sales/Index` | Any tenant user | Every invoice, 25/page. Employee sees only their own. |
| `/sales/{id}` | `Pages/Sales/Detail` | Any tenant user | The invoice. Print-friendly. |
| `/sales/{id}/return` | `Pages/Sales/Return` | Admin, Pharmacist | Take goods back against one line. |
| — | Cancel modal | Admin | On the sales list, using the shared confirm modal. |

### Navigation

Two entries, added to `NavRegistry.Tenant` immediately after the dashboard:

```csharp
new NavItem("New sale", "/Billing/Index", "cart", MatchPrefix: "/billing"),
new NavItem("Sales", "/Sales/Index", "receipt", MatchPrefix: "/sales"),
```

"New sale" sits at the top of the module list deliberately: it is the most-used screen in the
system and a cashier should not have to look for it. It is also rendered as the primary button on
the sales list and on the invoice, so the loop back to the till is one click from anywhere in the
module.

Both are visible to every role. `NavRegistry`'s visibility has never been a security boundary —
what an Employee cannot do is refused by the API, not hidden from the sidebar — and here the
distinction matters: an Employee genuinely does sell.

Two new icon keys in `_NavIcon`: `cart` and `receipt`. They sit next to each other in the
sidebar, so they had to be distinguishable at 18px — two variations on a document would not have
been.

---

## 2. The billing screen

### Layout

Two columns: search and cart on the left, a **sticky** bill summary on the right. The summary's
`top` clears the fixed topbar, so scrolling a long cart never hides the total.

Below 992px it becomes one column and the summary moves under the cart rather than shrinking.
Net payable and change are the two figures a cashier reads out loud, and squeezing them into a
narrow column is how they get misread.

### Keyboard-first, and what that means concretely

A cashier with a queue should not be reaching for a mouse. So:

- The search box is focused on page load.
- Two characters trigger a debounced search (180ms — long enough that a fast typist makes one
  request instead of eight, short enough that the list feels like it is keeping up).
- Arrow keys move through results; Enter adds the highlighted one; Escape closes the list.
- Enter with **nothing** highlighted takes the first sellable row, which is what somebody who
  typed an exact brand name expects.
- Enter while the list is closed runs the search rather than submitting the form — which is what
  a bare Enter in a text input inside a form would otherwise do.
- Adding an item focuses and selects its quantity box, so the next keystroke is the quantity.
- Removing a row returns focus to the search box.

The results list is a real `role="listbox"` with `aria-activedescendant`, so a screen reader
announces the highlighted option rather than the cashier hearing nothing as they arrow through.

### The search dropdown surfaces unsellable products with reasons

This is the design decision most worth defending on this page. Each row shows the brand and
strength, the generic name, an "AB" badge for antibiotics, and the available quantity. A product
that **cannot** be sold appears dimmed, is not selectable, and carries its reason inline:

| Reason | What the row says |
|---|---|
| Setup incomplete | "This product needs prices set before it can be sold." |
| All stock expired | "All available stock of X has expired. No sale possible until new stock arrives." |
| Out of stock | "No stock available." |
| Antibiotic, caller is an Employee | "Requires a pharmacist." |

A cashier who types "napa" and sees nothing concludes the pharmacy does not stock it and tells
the customer so. One who sees it greyed with "needs prices set" fetches whoever can fix that in
fifteen seconds; one who reads "requires a pharmacist" calls a pharmacist over. Silence is the
only outcome that loses the sale and teaches nobody anything.

Clicking a greyed row does not silently do nothing — the reason is written into the live region
under the search box, so it is announced rather than merely visible.

The one product that *is* absent is a deactivated one. Deactivating a product is how a pharmacy
says it no longer sells the thing at all, and offering it greyed out at the till would invite
somebody to reactivate it mid-sale.

### The cart

One row per product: name, quantity, unit dropdown, unit price, line total, remove.

- **The unit dropdown is built from that product's own configuration**, not from the `UnitLevel`
  enum — same rule as Module 3's forms. A product with no strip does not offer one. The options
  and their prices arrive with the search result, so no second request is needed.
- **Changing the unit changes the price** and recomputes the line, because a strip and a piece
  are different prices for the same thing.
- Available stock is a hint beside the quantity: "142 pieces available".
- A quantity exceeding available stock shows an inline error, tints the row, and disables
  completion. The check is in base units, so "2 boxes" of a 100-per-box product is measured as
  200.

### Cart rows are real form inputs

The cart is not a JSON body assembled by script. Rows are `<input>` elements named
`Input.Items[i].*`, added and removed by `billing.js` and posted by an ordinary form submit. The
antiforgery token, the validation redisplay and the redirect-on-success all work exactly as they
do on every other form in this app, which is worth more than the elegance of a fetch.

The template lives in a `<template>` element so its inputs are inert until cloned — a `name`
attribute in the live DOM would post an empty row on every sale.

### Why a rejected sale keeps its cart

Each row also posts the product's name, unit name, price, available quantity and unit
configuration as hidden fields. Those are **display only**; the server never reads them as money.
They exist so that if the sale is refused — a batch sold out mid-sale, a discount over the cap —
the page can redraw the cart exactly as the cashier left it, with no round trip per row.

Losing a cart of fifteen items because of one recoverable problem would be a far worse failure
than the one being reported.

### The live bill, and what is not sent

Subtotal, discount, net payable and change update on every keystroke, because a round trip per
digit would make the screen feel broken. `billing.js` mirrors `SaleMath`: two decimals, away from
zero, discount capped at the subtotal.

**None of it is transmitted.** The form posts product ids, quantities and unit levels. The server
prices the sale, splits the discount and computes the change. This file being wrong would be a
misleading preview, never a wrong charge — which is the property that makes it safe to have the
arithmetic in two languages at all.

### The discount control

A type toggle (% / Taka) and a value, with the cashier's own cap as helper text: "Max discount:
10%". Exceeding it shows an inline error and disables completion *before* submission.

The cap comes from `GET /api/sales/limits`, which reads the same `BillingPolicy` the server
enforces. It is not written into this app — `PMS.Web` references no other project, so a number
here would be a second copy that goes stale the first time somebody changed the real one.

The cap is evaluated in taka for both discount types. A percentage cap that only applied to
percentages would be worked around by pressing the other button.

### Complete sale, and saying why it is disabled

The button is disabled until the cart has an item, quantities are valid, the discount is within
the cap, cash covers the net total, and — if an antibiotic is present — every prescription field
is filled and the box ticked.

Underneath it, always, a line saying **which** of those is missing:

> Add an item to start. / Fix the quantities marked above. / The discount is over your limit. /
> Enter the cash received. / Fill in the prescription details and tick verified.

A disabled control with no explanation is the most common way a fast screen becomes a slow one:
the cashier clicks, nothing happens, and they start hunting. The note is in an `aria-live` region.

On submit the button disables itself on a deferred timeout — after the browser has collected the
form, so its value still posts — which guards against a double-tap on a slow connection producing
two sales.

### The prescription panel

Appears below the cart only while the cart holds an antibiotic. Amber-tinted, because it is a
legal record rather than a preference, and because a panel that appears and disappears as the
cart changes is disorienting unless it looks distinctly different.

Removing the antibiotic hides it again and leaves the typed values alone. A cashier who removed
the wrong row should not have to retype a prescription.

### Customer details

A collapsed `<details>`, open only if it already has values. It is skipped on most sales, and a
screen optimised for speed should not make the common path scroll past two empty fields.

### Empty state

> Search for a product above to start a bill.

---

## 3. The invoice (`/sales/{id}`)

### FEFO-split lines are grouped back together

The invoice table shows **one row per thing the customer bought**. A cart item that FEFO split
across two batches was one purchase, and which batch a tablet came from is the pharmacy's
bookkeeping.

Grouping is on `(ProductId, UnitSold)`, done server-side in `SaleQueries.GroupForInvoice`:

- Quantities and totals are **summed**; no money figure is recomputed, so the invoice cannot
  drift from the sale.
- The merge is arithmetically clean because the sale price is copied from the *product*, not the
  batch — so every line of a split carries the same `UnitSalePrice` and nothing is averaged.
- The displayed quantity divides the base-unit total back by the units per sold unit. It is exact
  by construction: the total is a whole number of sold units, because that is what was asked for
  before FEFO divided it up.
- The same product sold at two levels on one bill — two strips and three loose tablets — stays
  two rows, because that is what the customer asked for and what the prices differ on.

Lines with returns against them carry a badge showing the returned quantity.

### Batch detail is separate, and staff-facing

Below the invoice, a collapsed `<details>` lists the underlying `SaleLine` rows: batch number,
expiry, quantity, line total, discount share, net, returned. It is what a recall or a stock
reconciliation needs, and the only place the FEFO split is visible at all.

It carries `pms-no-print`, so it is not on the customer's copy.

### A cancelled invoice says so, including on paper

The cancellation banner is inside the printable region deliberately. A cancelled invoice that
printed identically to a live one is a document somebody can present as proof of purchase.

### Print approach

**A4 by default.** The `@media print` block hides the sidebar, topbar, action buttons and alerts,
flattens the shell's layout, and drops the invoice's border and padding. A receipt with a sidebar
printed down the left of it is the classic sign of a web app whose print stylesheet was never
written. The prescription panel loses its tint and keeps its border — a tinted block costs the
customer toner and reproduces as grey mush on a cheap printer.

**Thermal roll via a media query.** Most pharmacy counters print to an 80mm roll. There is no
media feature for "is a receipt printer", so `@media print and (max-width: 100mm)` keys off the
narrow page width the driver reports, which is what the printer's own paper size produces. In
that block the header stacks, the totals stop being right-aligned in a 20rem box, the font drops
to 11px, and columns marked `pms-invoice__optional` (strength, the AB badge) are hidden — a roll
has no room for a five-column table, and quantity, description and amount are what a customer
checks against their change.

Setting up the driver and the paper size is the user's environment. What the stylesheet
guarantees is that the page does not arrive laid out for a sheet four times the width.

---

## 4. The sales list (`/sales`)

Filters: date range, cashier, status. Columns: invoice number, date and time, cashier, item
count, net total, status, actions. 25 per page.

- **The cashier filter is absent for an Employee**, not disabled. The API returns an empty
  cashier list for them, and the page renders no control. A filter whose only options are "me"
  and other people's names, most of which return nothing, is worse than no filter.
- The subtitle changes too: "Every invoice this pharmacy has issued" for Admin and Pharmacist,
  "The invoices you have rung up" for an Employee. Better to say what the list is than to let
  somebody wonder why it is short.
- **Item count is distinct products, not sale lines.** A FEFO split is not two things bought, and
  a column reading "3 items" for a two-item sale would look like a bug to the person who rang it
  up.
- Cancelled rows are dimmed with a red badge. A sale with returns against it carries a "Part
  returned" badge — not a status on the sale, but the thing somebody scanning the column wants to
  know, because it changes what the totals mean.
- Actions are role-filtered from `GET /api/sales/limits`: *Invoice* always, *Return* for Admin
  and Pharmacist, *Cancel* for Admin. Hiding them is a courtesy; the API is the control.

### The cancel modal

Admin only. The shared `_ConfirmModal` grew an **optional required-reason field** for this, rather
than the page carrying a second modal of its own:

> **Cancel INV-000452?**
> This will cancel invoice INV-000452 and restore all stock from this sale. The sale will be
> excluded from all reports. This cannot be undone.
>
> Reason for cancelling * `[____________]`
>
> [Cancel] [**Cancel the sale**]

A trigger opts in with `data-pms-confirm-prompt="Reason for cancelling"`, and the form it submits
carries an input marked `data-pms-confirm-prompt-value` for the reason to land in. Every existing
caller of the modal is unchanged.

Focus normally goes to the modal's *Cancel* button so a reflexive Enter takes the safe path.
When a reason is required, focus goes to the field instead — nothing can happen until the person
types, so it is both more useful and still safe. An empty reason shows an inline error rather
than submitting.

---

## 5. The return screen (`/sales/{id}/return`)

### Why it shows the refund before confirming

This is the point of the screen. A refund on a discounted sale is **lower** than the price printed
on the invoice: ৳52.50 back on a line that reads ৳60.00. A cashier who meets that at the moment of
opening the till will hesitate in front of the customer, or worse, argue with them.

So the figure appears as soon as a line and a quantity are chosen, with the reason it is what it
is:

> Refund: 13.13. This is the discounted price the customer paid.

It comes from the API, computed by the same `SaleMath.RefundFor` the command uses — so what is
displayed is what gets paid. For the common case, returning everything still outstanding on a
line, the number is the server's own `RefundIfAllReturned` and is not computed in the browser at
all.

### The rest of the screen

- A table of the sale's lines with sold, still returnable, and the refund if all of it came back.
  Fully-returned lines are dimmed and show "Fully returned" instead of a radio button.
- Quantity plus a unit dropdown built from the selected line's own product, so a strip can be
  handed back as one strip.
- Reason: required, free text, with quick-picks for *Customer changed mind*, *Wrong item given*
  and *Defective*. A fixed list alone gets answered with whichever option is least trouble to
  click; the quick-picks fill the field and move focus into it, so they are a shortcut through it
  rather than a destination.
- The confirmation modal states the outcome in full: "Return 10 pieces of Napa 500? This restores
  stock to the batch it was sold from and refunds 13.13."
- When everything on the sale has already come back, the page is an empty state rather than a
  form with nothing to select.
- A cancelled sale never reaches this page: the API answers 409 and the page redirects to the
  invoice with the explanation, because "nothing left to return" and "this sale was reversed
  entirely" are different things to tell somebody.

---

## 6. New shared components

| Component | Change |
|---|---|
| `_ConfirmModal.cshtml` | Optional required-reason field, opt-in per trigger. |
| `site.js` | Honours `data-pms-confirm-prompt`; writes the reason into the form's `data-pms-confirm-prompt-value` input; focuses the field when present. |
| `_NavIcon.cshtml` | `cart` and `receipt`. |
| `site.css` | `pms-card__title`, `pms-card__summary`, `pms-card--prescription`, the `pms-billing` grid, `pms-search`, `pms-cart`, `pms-bill`, `pms-invoice`, and the two print blocks. |
| `billing.js` | New. Search, cart, live bill. |
| `returns.js` | New. Refund preview and the unit dropdown. |

`pms-card__title` is new because Module 5 is the first place a card needs a heading inside its
body rather than in a bordered `__header` strip: the billing screen stacks four cards, and a
strip on each would turn the page into a grid of boxes.

---

## 7. Out of scope

- Barcode scanning. The search box is the only way in.
- Held or parked bills. A cart lives in the page and is lost on navigation, apart from the
  redisplay after a rejected submit.
- Editing a completed sale. There is no screen because there is no endpoint — see the backend
  doc for why.
- Non-cash payment. The cash field is required and must cover the net total.
- Thermal printer hardware setup. CSS only.
