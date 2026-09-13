# 04 — Suppliers & Purchase Management (frontend)

Eight Razor pages, plus three retrofits to pages that already existed.

Server logic, the balance formula and the access rules: `docs/04-suppliers-and-purchase.md`.

---

## 1. Page inventory

```
/suppliers                          list, with each supplier's outstanding balance
/suppliers/{id}                     detail: totals, purchase history, payment history
/suppliers/edit                     add
/suppliers/edit/{id}                edit
/suppliers/{id}/payments/create     record a payment          (Admin only)

/purchases                          list, filterable
/purchases/create                   the multi-line delivery form
/purchases/{id}                     detail: lines, payments, what is due
/purchases/{id}/return              send goods back

/reports/supplier-dues              the Module 8 retrofit     (Admin only)
```

Two sidebar entries — **Purchases** then **Suppliers** — both hidden from an Employee. Purchases
comes first because recording a delivery is the daily act and a supplier is usually reached by
following a link from one.

Visibility is a convenience. `[Authorize(Policy = WebPolicies.TenantWriter)]` on every page model
is what actually stops anybody, and `web_smoke_purchases.py` asserts a redirect to `/denied` for an
Employee on all eight pages, and for a Pharmacist on the payment page and the dues report.

---

## 2. The multi-line purchase form

The most complex form in the application.

### 2.1 Rows are the form, and they post as one request

Rows are added and removed in the browser and posted as an indexed collection
(`Input.Lines[0].ProductId`, `Input.Lines[1].…`). The whole delivery is **one request and one
transaction**: a failure on the third row leaves nothing behind. Posting row by row would create
batches that a later failure could not take back.

`renumber()` rewrites every row's `name` and `id` after an add or a remove. Without it, removing
the middle of three rows leaves `Lines[0]` and `Lines[2]` — and the model binder stops at the first
gap, silently dropping the third row from the purchase.

The server drops empty slots rather than validating them, so a removed row cannot fail the form.

### 2.2 Per-row unit dropdowns

Each row has **two** unit dropdowns — one for quantity, one for price — and both are rebuilt from
the chosen product's own configuration:

| Product | Offers |
|---|---|
| Napa (piece / strip of 10 / box of 10 strips) | piece, strip, box |
| Handwash (bottle / carton of 24) | bottle, carton |
| Saline (bag only) | bag |

A single shared unit list would offer a strip of handwash. The catalogue goes to the browser once
as JSON on `data-products`, so a five-row form makes one request rather than one per row per
keystroke.

The product list is the **billing screen's search**, reused: it already returns every active
product with its full unit configuration, *including* ones that are out of stock or not yet priced.
Both of those are right here — a product with no stock is the most likely thing somebody is buying.
Its `Status` field, which describes whether a product can be *sold*, is ignored.

### 2.3 Live helpers, and what they are not

Beside the quantity: `= 200 pieces`. Beside the price: `= 0.80 per piece`. A read-only line total,
and a running purchase total at the foot.

**None of it is authoritative.** Quantity and price are posted in the unit the person chose,
exactly as Add Stock posts them, and the server converts through Module 2's helpers. If the helper
text ever disagreed with the server, the server would still be right, and nothing computed in the
browser is stored. Doing the packing arithmetic client-side would be a second implementation of
the one calculation in this system most likely to be got wrong.

### 2.4 Expiry, reflected per row

Once a product is chosen the row marks its expiry field required for a medicine and optional
otherwise — Module 3's rule, reflected so somebody is not told about it only after pressing save.
The server enforces it either way, and its refusal names the row: *"Line 2: …"*.

### 2.5 The loss warning

Per row, non-blocking: shown when the cost per base unit exceeds the product's sale price per base
unit. Bought above list price is a real decision — the pharmacy may simply be about to reprice —
so it is worth saying once and not worth refusing over. The server produces the same warnings on
save and the purchase detail page shows them.

### 2.6 Without JavaScript

The form still works, less pleasantly: the rows the server rendered still post, the selects still
submit, and every rule that matters is enforced server-side. What is lost is adding rows, the live
totals, and the per-row unit filtering.

---

## 3. The returnable cap, and how it is surfaced

`/purchases/{id}/return` lists **every** line, not only the returnable ones.

For each: what was delivered, what has already gone back, what can still go, and what that would
credit. The returnable figure is
`min(delivered − already returned, what the batch holds now)` — computed on the server, never in
the page.

**Lines with nothing returnable are shown disabled with the reason**, not hidden:

- *"No stock remaining from this batch to return."* — it has been sold or written off.
- *"Everything on this line has already been returned."*

Hiding them would leave somebody hunting for a product they can plainly see on the purchase. The
radio is disabled and the row is greyed.

Where a line *is* returnable but the **batch** rather than the bill is the limit, the row says
*"limited by what the batch still holds"*. The two are different problems: "you already sent most
of it back" is a records question; "it has been sold" is a stock one.

Quantity is entered in **base units** on this page, deliberately. The lines on one purchase can be
different products with different packing, and a single dropdown cannot offer strips for one row
and cartons for another. The returnable column is formatted in each product's own units so the two
can be read together.

The reason field offers quick-picks (Near expiry, Damaged on arrival, Wrong item, Other) through a
`<datalist>`, so free text is still accepted — a closed list gets filled in as "Other" for exactly
the cases that matter.

Below the form, the page states the two effects plainly: stock comes off the batch with an
adjustment recording why, and the supplier's balance comes down — **but what has already been paid
does not change.**

---

## 4. Payment linking

`/suppliers/{id}/payments/create`, Admin only.

**The current outstanding balance sits above the amount field**, in a stat card. It is the number
the amount is being decided against, and somebody who has to scroll to find it will type from
memory instead.

The "Against" dropdown offers **General payment (against the account)** as its *first* option, not
an afterthought — "here's fifty thousand, put it against what we owe" is how a great many pharmacy
settlements happen. Below it, only unpaid and partially paid bills; settled ones are omitted,
because paying one is an overpayment recorded in the least visible possible place.

The difference is visible everywhere afterwards. On the supplier's payment history a general
payment renders as *"General payment"* rather than a blank cell — a blank reads as missing data,
and this is a deliberate kind of payment. On the purchase detail page, general payments are
explicitly *absent* from the payments table, with a note saying they reduce the supplier's overall
balance and are listed on the supplier's page instead.

### 4.1 They are still shown against the bills they cover

A general payment reduces what the supplier is owed, so a bill it covers must not read **Unpaid**
on a page whose headline says nothing is owed. That pairing shipped once and was plainly wrong:

> **Outstanding 0.00 — nothing owed**, above **PUR-000007 · Unpaid · 765.00 due**

Unallocated payments are now applied **oldest bill first** at display time. The Due column and the
status badge include them, so a supplier's bills add up to their outstanding balance. Each row
that received some says *"after 750.00 from general payments"* beneath the figure; the purchase
page carries the same note on its Due card and an info banner above the payments table explaining
why that table can be empty while the bill reads covered.

Under the purchase history, when any row was affected:

> *Some payments to this supplier went against the account rather than a specific bill. They are
> shown above against the oldest bills still owing, so the Due column adds up to the outstanding
> balance. **That is a view, not a record** — nothing is stored saying which bill an unallocated
> payment settled.*

That last sentence is the important one. `AmountPaid` on the bill is untouched and still reads
0.00; the allocation exists only on the way to the screen. See `docs/04-suppliers-and-purchase.md`
§4.4.1.

### 4.2 Settling a credit

When a supplier is holding a credit — goods returned after a bill was paid, or a payment that
overshot — the payment form grows a **direction** chooser. It appears *only* when there is a
credit, because offering "record a refund" on an account that owes money invites somebody to
record a payment in the wrong direction, which is the one mistake on this form that silently
doubles a debt.

Three options, as radios rather than a dropdown, because they change what the rest of the form
means and should be visible at once:

| | What it records | Money moves |
|---|---|---|
| **Paying them** | the ordinary case | out |
| **They are refunding me** | the supplier hands the credit back | in |
| **Write the credit off** | nobody is going to collect it | no |

A write-off requires a reason. Without one the dues report loses a figure with no explanation.

Choosing either incoming option hides the "Against a bill" row — `supplier-payment.js`, which
also clears the select, because a hidden one still posts. A credit belongs to the account, not to
a bill, and the server refuses one that names a purchase with both a validator and a CHECK
constraint. Without the script the row stays visible and any selection is dropped server-side, so
nothing breaks; it is just less obvious why.

The amount pre-fills with the credit, since settling it in full is what somebody came to do nine
times out of ten — still editable, because a supplier may hand back part.

**Afterwards**, the payment history grows a **Type** column: *Payment*, *Refund received* or
*Credit written off*. Every amount in that table stays positive and the type column carries the
direction; a column of mixed signs is read wrongly far more often than a column of labels. A fifth
stat card, **Refunded to us**, appears once money has come back — and only then, since a card
reading 0.00 on every ordinary account would be noise.

The bill that was holding the credit shows *"40.00 credit settled"* beneath its Due, the mirror of
the general-payment line above it, so the bills keep adding up to the balance.

### 4.3 "In credit", not "overpaid"

The word changed on the suppliers list, the dues report and the supplier page. A negative balance
arises just as often from returning goods after paying as from overpaying, and calling that an
overpayment describes one cause rather than the state. The supplier page's Outstanding card reads
*"in credit — they owe you"*.

**An amount above the balance warns and saves.** The warning travels through to the supplier page
in the success message. Refusing would leave somebody unable to record money that has genuinely
left the till.

---

## 5. Balances on screen

`Money.Signed` puts a real minus sign on a negative, and `Money.Tone` colours it — but colour is
never the only signal, so these read correctly in monochrome and to anyone who cannot distinguish
the two hues.

A supplier in credit shows a negative figure labelled **in credit**, on the list, on their detail
page, on the purchases list, and on the dues report. Nothing clamps to zero anywhere: a credit is
real money, and it clears only when the next delivery absorbs it, the supplier refunds it, or it
is written off (§4.2).

`StatusPresentation.ForPurchase` labels a bill — Paid (green), Partially paid (amber), Unpaid
(red) — so every screen describes the same bill the same way. A bill settled entirely by a return
reads **Paid**, because the status answers "is there anything left to settle" — and so does one
covered entirely by general payments, for the same reason.

---

## 6. The three retrofits

### 6.1 Reports landing + supplier dues page

The disabled card is gone; `/reports/supplier-dues` is a real page. It defaults to **all time**,
unlike every other report in that folder — "who do we owe" is a question about now.

When a date range *is* applied, an info banner says plainly that purchased / paid / returned cover
the selected dates while **outstanding always covers all time**. The two kinds of column answer
different questions and a reader who conflated them would act on the wrong one.

Supplier names link to their detail page, where the same outstanding figure appears — it comes from
the same service, so the two agree by construction. The page's closing note says so.

A bar chart shows the ten suppliers owed the most; suppliers in credit are excluded from it, since
plotting credits alongside debts would need a zero line to be readable, and the table carries those
rows anyway.

### 6.2 Expired stock

`/alerts/expired` offers **Return to supplier** for a batch that arrived on a recorded purchase,
linking straight to that purchase's return screen with the line pre-selected, and says *"Arrived on
PUR-000123 from …"*. Both buttons appear where a return is possible — a supplier who refuses it
still leaves somebody needing to clear the shelf.

A batch entered through Add Stock keeps **Adjust stock** alone, and where it carries a free-text
supplier name the page says so and explains there is no bill to return it on.

The page asks `GET /api/purchases/origins` once for its whole page of batches. A failure there
leaves every row offering Adjust stock, which is the correct fallback rather than a broken page.

### 6.3 Add Stock's supplier field

A searchable dropdown of active suppliers instead of free text, still optional, with
*"No supplier recorded"* as the first option. Below it, a pointer: *"Buying against a bill? Record
a purchase instead — it creates the batch and tracks what you owe."*

Batches created before Module 4 keep their free-text supplier name and display it. See the backend
doc's historical-data gap.

---

## 7. Files

**Pages** — `Pages/Suppliers/{Index, Detail, Edit, RecordPayment}.cshtml(.cs)`,
`Pages/Purchases/{Index, Create, Detail, Return}.cshtml(.cs)`,
`Pages/Reports/SupplierDues.cshtml(.cs)`

**Changed** — `Pages/Alerts/Expired.cshtml(.cs)`, `Pages/Stock/Add.cshtml(.cs)`,
`Pages/Reports/Index.cshtml`, `ViewModels/AlertPresentation.cs`, `ViewModels/UiModels.cs`

**Api** — `Api/{SupplierContracts, PurchaseContracts}.cs`, Module 4 methods on `Api/PmsApiClient.cs`

**Scripts** — `wwwroot/js/purchase-form.js`, `wwwroot/js/supplier-payment.js`

**Navigation** — `Navigation/NavRegistry.cs`, `Pages/Shared/_NavIcon.cshtml` (`truck`, `handshake`)

**Styles** — `wwwroot/css/site.css`, Module 4 block
