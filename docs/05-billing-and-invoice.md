# 05 — Billing & Invoice

The counter. Everything before this module described what a pharmacy holds; this is where it
sells it.

Two things in here are worth reading before anything else, because they are the two that are
expensive to correct after the fact:

- **The price snapshot.** What was charged is copied onto the sale line at sale time. A price
  change next month must not alter last month's invoices or last month's profit figures.
- **The discount split.** A bill-level discount is apportioned across the lines and stored per
  line. Without it, a partial return refunds the pre-discount price and overpays the customer —
  quietly, on every discounted sale, and more generously the bigger the discount was.

Both are implemented in `PMS.Domain/Billing/SaleMath.cs` and pinned by
`tests/PMS.UnitTests/Domain/Billing/SaleMathTests.cs`.

---

## 1. Overview

A sale takes a cart of products and quantities, deducts the stock from batches in FEFO order,
and writes one invoice. Because FEFO can split a single cart item across several batches, the
customer's one line becomes several `SaleLine` rows — and the invoice groups them back together
for display.

The whole of it — the invoice number, the sale, its lines, and every batch quantity — commits in
one transaction. There is no state in which stock has moved and no invoice exists, or an invoice
exists against stock that never left the shelf.

Nothing about a completed sale is editable. The corrective paths are a **return** (part of it
came back) and a **cancellation** (the whole thing was a mistake), both of which leave the
original readable. See §10.

### What the client sends, and what it does not

`POST /api/sales` carries product ids, quantities and unit levels. It carries **no prices, no
line totals and no batch ids**. Prices come from the product, batches from the FEFO helper, and
every total from the domain's own arithmetic. A client that could name its own prices could name
lower ones; a client that could name batches could sell stock that was never on the shelf.

The billing screen computes the same figures live so the cashier sees a bill update as they
type, but none of those numbers are transmitted. That code being wrong would be a display bug,
never a pricing one.

---

## 2. Access control

| Action | Admin | Pharmacist | Employee |
|---|---|---|---|
| Complete a sale | ✓ | ✓ | ✓ |
| Sell an antibiotic | ✓ | ✓ | ✗ (403) |
| Discount | unlimited | ≤ 10% | ≤ 5% |
| List sales | all | all | **own only** |
| Open an invoice | any | any | **own only** (403) |
| Take a return | ✓ | ✓ | ✗ |
| Cancel a sale | ✓ | ✗ (403) | ✗ |

Three of these are policies on the endpoints (`TenantUserPolicy`, `TenantWriterPolicy`,
`TenantAdminPolicy`). The other three cannot be, because a policy cannot see what is in the
cart or whose sale is being read:

- **Antibiotics** — `CompleteSaleCommandHandler.Blocked` returns `Error.Forbidden` for each
  antibiotic product when the caller is an Employee. The till itself is open to every role; it
  is the cart that gets refused.
- **Discount caps** — `BillingPolicy.IsDiscountAllowed`, checked in the completion handler
  before the discount is applied.
- **Own sales only** — `GetSalesQueryHandler` passes a `restrictTo` user id into the query, and
  `GetSaleQueryHandler` returns 403 for another cashier's invoice.

All three are declared on the platform access page (`/platform/access`) so they appear in an
access review rather than only in this document. Module 5 is what made
`WithheldFieldCatalog` grow a `WithheldKind`: two of these restrictions hide *rows* and *refuse
actions* rather than hiding a column, and a table that called all three "withheld field" would
leave a reader believing the sales list returns everybody's rows with some columns blanked.

### Why Employees may sell at all

Refusing them the till would leave a pharmacy unable to staff a counter. What they cannot do is
dispense an antibiotic, discount beyond 5%, see a colleague's takings, take a return or reverse
a sale — which is to say, everything that either needs professional judgement or moves money
back out of the drawer.

### Where the caps live

`PMS.Application/Common/Billing/BillingPolicy.cs`, and nowhere else. A Settings module will make
them per-pharmacy, and its work is then to replace that one class rather than to hunt for
constants across six handlers and four Razor pages.

The billing screen shows the cashier their own cap ("Max discount: 10%"). It reads it from
`GET /api/sales/limits`, which reads `BillingPolicy` — because `PMS.Web` references no other
project, so a number written into the web app would be a second copy that goes stale the first
time somebody changes the real one.

---

## 3. Schema

### Sale

One completed transaction. `InvoiceNumber` is unique **per pharmacy**, not globally: two
pharmacies both have an INV-000001 and neither knows about the other's.

| Column | Notes |
|---|---|
| `InvoiceNumber` | `NVARCHAR(40)`, e.g. `INV-000452`. See §7. |
| `SaleDate` | UTC. Distinct from `CreatedOnUtc` — see below. |
| `CashierUserId` | FK → Users. The column an Employee's own-sales-only rule filters on. |
| `Subtotal` | Sum of line totals, before discount. |
| `DiscountType` / `DiscountValue` | How the cashier expressed it: `Percent` + 5, or `Flat` + 62. Both null or both set. |
| `DiscountAmount` | The taka actually deducted. Computed and stored. |
| `NetTotal` | `Subtotal − DiscountAmount`. |
| `CashReceived`, `ChangeGiven` | Cash only in this module. |
| `Status` | `Completed` or `Cancelled`. There is no `Edited`. |
| `CancelledReason`, `CancelledByUserId`, `CancelledAt` | All three together, or all three null. |
| `CustomerName`, `CustomerPhone` | Optional plain text. No customer table — see §10. |
| Prescription block | Six columns, populated only when the sale contained an antibiotic. Module 7's register reads them. |

`SaleDate` and `CreatedOnUtc` hold the same value today and would not if a held bill or an
offline counter were ever added. Reports index and range-filter on `SaleDate`.

### SaleLine

One product, out of one batch. A cart item that FEFO split across two batches is two rows here
and one row on the invoice.

| Column | Notes |
|---|---|
| `BatchId` | Required. What a return reverses, rather than guessing. |
| `QuantityInBaseUnits` | Integer. May not be a whole number of `UnitSold` — 5 pieces of a 3-per-strip product can split 4 and 1. |
| `UnitSold` | What the customer bought in, so the invoice reads "2 strips". Presentational; every calculation runs on base units. |
| `UnitSalePrice` | **The price snapshot.** `DECIMAL(18,4)`. |
| `LineTotal` | Pre-discount. Stored. |
| `DiscountShare` | This line's share of `Sale.DiscountAmount`. Stored. |
| `NetLineTotal` | `LineTotal − DiscountShare`. What the customer actually paid for this line. |

### Why all three of those are snapshotted rather than derived

**`UnitSalePrice`** — the obvious one. Read the product's current price to render a historical
invoice and the invoice changes when the product is re-priced. Every profit figure ever reported
would move with it.

**`LineTotal` and `NetLineTotal`** — the less obvious one, and the reason is rounding. Both
splits in §4 absorb a rounding residual into their last element, so a line's figures depend on
the *set* of lines it was part of. Recomputing one line in isolation cannot reproduce that and
would disagree with the invoice the customer is holding — by a paisa, on some sales, which is
exactly the kind of discrepancy that costs an afternoon to chase.

`DiscountShare` is the one that is not just an optimisation: without it there is nothing for a
partial return to refund from except the sticker price.

### Money precision is deliberately not uniform

| | Precision | Why |
|---|---|---|
| Sale and line cash totals, refunds | `DECIMAL(18,2)` | These are cash. There is no third of a paisa of change, and storing one would let the columns stop summing to each other. |
| `UnitSalePrice` | `DECIMAL(18,4)` | Matches `Products`. A price per base unit derived from a pack genuinely is fractional — a strip of three at ৳10 — and rounding the snapshot would make it disagree with what was charged. |

The migration also enforces the arithmetic with CHECK constraints:
`NetTotal = Subtotal − DiscountAmount`, `ChangeGiven = CashReceived − NetTotal`,
`DiscountAmount ≤ Subtotal`, `NetLineTotal = LineTotal − DiscountShare`. A hand-written UPDATE
does not go through the domain, and a sale whose columns disagree with each other is one nobody
can reconcile.

### SalesReturn

Attached to a **line**, not to a sale: the line is what knows the batch to restore and the
discounted price the goods were sold at.

| Column | Notes |
|---|---|
| `SaleLineId` | FK, cascade — a return has no meaning without its line. |
| `QuantityReturnedInBaseUnits` | `> 0`, and cannot exceed the line's quantity minus prior returns. |
| `Reason` | Required, free text. Quick-picks on the screen; a fixed list alone gets answered "Other". |
| `RefundAmount` | Computed from `NetLineTotal`. Stored — see §4. |
| `ReturnedByUserId` | FK → Users. |

"The sum of returns against a line cannot exceed what it sold" spans rows, which a CHECK cannot
see. A trigger could, and would put a business rule where nobody reading the handler would look
for it. It is enforced in `SalesReturn.Record`, the only way one can be constructed.

### Indexes

`(TenantId, SaleDate DESC)` and `(TenantId, CashierUserId, SaleDate DESC)` — both named in the
module brief for Module 8, and the second is also what makes an Employee's own-sales-only list a
seek rather than a scan. Plus `(TenantId, SaleId)`, `(TenantId, ProductId)` and
`(TenantId, BatchId)` on lines, the last for the profit join to batch purchase cost.

---

## 4. The two calculations that have to be exact

Both are built on one primitive, `SaleMath.Distribute`, which splits an amount across weights so
that the parts sum back to it exactly.

### Rounding

`SaleMath.Round` is `MidpointRounding.AwayFromZero`, **not** .NET's default `ToEven`. A ৳13.125
refund is ৳13.13, not ৳13.12. A customer handed back two paisa less than the arithmetic says is
a customer who is right and a receipt that is wrong; away-from-zero is also what a person does
with a pencil. `SaleMathTests.Round_is_not_the_dotnet_default` pins the difference so nobody
"simplifies" it back.

### The discount split

For each line, in order:

```
DiscountShare = round((LineTotal / Subtotal) × DiscountAmount, 2)
NetLineTotal  = LineTotal − DiscountShare
```

**The residual rule.** Rounding each share independently does not always add up. Three lines of
৳111 sharing a ৳10 discount give ৳3.33 three times, which is ৳9.99. The missing paisa goes onto
the **last** line, so `sum(DiscountShare) == DiscountAmount` holds exactly. The reconciliation is
signed, because rounding overshoots as readily as it undershoots. The residual is bounded by half
a paisa per line, so the last line is never visibly distorted.

`Sale.ApplyDiscount` asserts the invariant after applying it and throws if it fails. If that ever
fires, a future partial return would refund the wrong amount — a silent and expensive failure,
so it is worth being loud about at the point it becomes wrong.

**Worked example** (the module brief's, and a test):

| Line | Quantity | Unit price | LineTotal | DiscountShare | NetLineTotal |
|---|---|---|---|---|---|
| Napa | 40 pieces | ৳1.50 | ৳60.00 | (60/160) × 20 = **৳7.50** | ৳52.50 |
| Azin | 2 pieces | ৳50.00 | ৳100.00 | (100/160) × 20 = **৳12.50** | ৳87.50 |
| | | | **৳160.00** | **৳20.00** ✓ | **৳140.00** |

Subtotal ৳160, flat discount ৳20. The shares sum to exactly ৳20.00.

### The line-total split — the subtler one

A cart item priced at one level and split across batches needs the same treatment, and this one
is easy to miss. Selling 5 pieces of a product priced ৳10 a strip of 3 is ৳16.67. If FEFO takes 4
from one batch and 1 from another, pricing each line on its own gives ৳13.33 + ৳3.33 = ৳16.66 — a
paisa short of what the screen showed, on a bill the customer is holding.

So `SaleMath.SplitLineTotals` computes the item's total once from the quoted price and apportions
it by base-unit quantity. Which batch a tablet came out of is the pharmacy's business, not the
customer's, and this is what keeps it invisible on the invoice.

### The return refund

```
RefundAmount = round((QuantityReturned / SaleLine.QuantityInBaseUnits) × SaleLine.NetLineTotal, 2)
```

**From `NetLineTotal`, never `LineTotal`.** The customer paid the discounted price, so that is
what comes back. Returning all 40 Napa from the example above refunds **৳52.50**, not ৳60.00.
Returning 10 of the 40 refunds (10/40) × 52.50 = **৳13.13**.

**The completing return is exact.** A line returned in four parts of ten would otherwise refund
৳13.13 four times against a ৳52.50 line — an overpayment of two paisa, and a breach of the
property that returning everything on a sale refunds exactly `Sale.NetTotal`. So the return that
empties a line pays the *remainder* rather than its own proportion. This is the same
reconciliation idea as the discount residual, applied at the other end of the sale, and it is an
extension of the formula above rather than a departure from it: every partial return still uses
the proportional figure.

The return screen shows the computed refund before the cashier confirms, taken from the API and
computed by this same function — so what is displayed is what gets paid.

---

## 5. FEFO deduction and multi-batch splitting

`IStockQueries.GetActiveBatchesFefoAsync` (Module 3) returns a product's sellable batches in the
order they must be sold: soonest expiry first, non-expiring stock last, oldest delivery breaking
a tie. Module 5 does not re-implement or re-sort that — a second implementation of "earliest
expiry first" is exactly what the helper exists to prevent.

`FefoAllocator.Allocate` walks that list and plans the deduction:

```
take = min(needed, batch.QuantityInBaseUnits), repeat until satisfied
```

**All or nothing.** A cart item that cannot be filled completely produces no takes at all.
Half-selling an item and telling the cashier afterwards is worse than refusing, because the stock
has already moved. When it fails, the allocation still reports the total available, because that
is the number the cashier needs:

> Only 39 pieces of 'Napa 500' available across all batches.

**Worked example** (a test): Napa has B-101 with 30 pieces expiring sooner and B-100 with 100
expiring later. Selling 40 produces two `SaleLine` rows — 30 from B-101, 10 from B-100 — leaving
B-101 at 0 and B-100 at 90. The invoice shows one line: "Napa 500 × 40 pieces".

### Batches are read once per product, not once per cart item

A cart holding the same product twice would otherwise allocate the same batch stock twice: the
second read would not see the first deduction, because the first has not been saved yet. The
handler reads each product's FEFO list once and applies each allocation to the in-memory batches
immediately, so the second item sees what is actually left.

### Re-validation at submit

Stock is checked when an item goes into the cart, and **again** inside the sale transaction
against freshly read batches. Between the two, another till may have sold the same batch or a
stock correction may have reduced it. A shortfall at submit refuses the whole sale with the
message above and leaves the cart intact so the cashier can adjust it.

### A sale writes no StockAdjustment

`Batch.DeductForSale` is the only quantity change in the system that produces no adjustment row,
and that is deliberate — see the remarks on `AdjustmentType`. A sale is audited by its own sale
line, which carries the price charged, the cashier and the customer: everything an adjustment row
would say and more. Recording both would give the pharmacy two competing accounts of the same
event and bury the handful of adjustments that genuinely need explaining under one row per item
sold.

Stock going the *other* way — a cancellation or a return — **does** write one. See §8.

---

## 6. Sale-blocking rules

All five are enforced server-side. The billing screen shows the same messages against the same
products, which is a convenience; a direct API call hits exactly the same wall.

| Rule | Response | Enforced in |
|---|---|---|
| `IsSetupComplete = false` | 400 — "'X' needs prices set before it can be sold." | `CompleteSaleCommandHandler.Blocked` |
| All stock expired | 400 — "Only 0 pieces of 'X' available across all batches." | `FefoAllocator` (the FEFO helper excludes expired batches) |
| Out of stock | 400 — same shape, with the available figure | `FefoAllocator` |
| Antibiotic + Employee | **403** — "Antibiotics require a pharmacist. Please call one over." | `CompleteSaleCommandHandler.Blocked` |
| `IsActive = false` | Absent from search; 400 if an id is sent anyway | `SaleQueries.SearchSellableAsync`, then `Blocked` as a backstop |

Out-of-stock and all-expired are not checked before the allocation, deliberately: the allocator
knows how much was asked for, and "only 12 available" is a better message than "no sellable
stock".

The search endpoint returns the first four of those as a `SellableStatus` **with the reason
attached**, rather than filtering the product out. A cashier who types "napa" and sees nothing
concludes the pharmacy does not stock it and tells the customer so. One who sees it greyed with
"needs prices set" fetches somebody who can fix it in fifteen seconds. Silence is the only
outcome that loses the sale and teaches nobody anything.

The single exception is an inactive product, which is genuinely absent: deactivating a product is
how a pharmacy says it does not sell the thing at all, and offering it greyed out at the till
would invite somebody to reactivate it mid-sale.

---

## 7. Invoice numbering

`INV-` plus six digits, sequential per pharmacy, allocated by `InvoiceNumberGenerator` with a
single atomic statement:

```sql
UPDATE [dbo].[InvoiceSequences]
SET [NextNumber] = [NextNumber] + 1
OUTPUT deleted.[NextNumber] AS [Value]
WHERE [TenantId] = @tenantId
```

The statement reads and writes under one update lock, so two cashiers completing a sale in the
same instant get 452 and 453 — never both 452. A read followed by a write would not do this
however carefully it were written, because the gap between the two is where the second cashier
fits. `UX_Sales_Tenant_InvoiceNumber` sits behind it as a backstop: if the allocation were ever
replaced by something racy, a duplicate becomes a failed insert rather than two invoices with the
same number.

The allocation happens **inside** the sale transaction, so a sale that fails rolls its number
back too and the sequence has no gaps.

`InvoiceSequences` is deliberately not an EF entity: it holds no business data, only this class
reads or writes it, and mapping it would give it a global tenant query filter that the SQL would
then have to bypass — establishing exactly the pattern worth avoiding. The tenant id is an
explicit parameter on every statement instead.

### The transaction, and a correction to Module 3

Module 3 concluded that transactions were unusable here, because
`SqlServerRetryingExecutionStrategy` refuses a user-initiated transaction while retry-on-failure
is enabled. That is true of `BeginTransactionAsync`, and it was too broad a conclusion: the
supported way to have both is to hand the whole block to the execution strategy.

`IUnitOfWork.ExecuteInTransactionAsync` does that, and Module 5 is its first caller. Module 3's
single-`SaveChanges` atomicity is still correct where there are two rows to write; a sale
allocates an invoice number with its own statement, so one save is not enough.

The implementation clears the change tracker before each attempt. Without that, a transient
failure that rolled the transaction back would leave the previous attempt's entities still
tracked as `Added`, and the retry would insert them a second time — two sales, two invoice
numbers, stock deducted twice. The cost is that the delegate must load all of its own state,
which is stated on the interface.

---

## 8. Prescription capture for antibiotics

When the cart contains any product with `IsAntibiotic = true`, six fields become required:
`PatientName`, `PatientPhone`, `DoctorName`, `PrescriptionNumber`, `PrescriptionDate`, and
`PrescriptionVerified` must be `true`.

**Client.** The panel appears below the cart only when an antibiotic is in it, amber-tinted so it
reads as a different kind of thing from the rest of the form, and *Complete sale* stays disabled
until every field is filled and the box is ticked. Removing the antibiotic hides the panel again
and leaves the typed values alone — a cashier who removed the wrong row should not have to retype
them.

**Server.** `CompleteSaleCommandHandler.ValidatePrescription` refuses the sale regardless of what
the client sent, naming each missing field individually so the form can put the message under the
input rather than in a banner. An unticked verification box is its own refusal with its own
message.

In practice only an Admin or a Pharmacist ever sees the panel, because an Employee cannot add an
antibiotic to a cart at all. The server-side check does not rely on that: the two rules are
independent, and a direct API call with an antibiotic and no prescription is rejected whoever
sends it.

---

## 9. How cancellation and return both restore stock, and how they differ

Both put units back into **the batch the line came out of**, not into whichever batch is currently
first in FEFO order, and both write a `StockAdjustment` of type `Add` so a shelf count next week
has an explanation for the extra units.

Physically nobody is reading batch stickers on returned goods. This is a bookkeeping convention —
but it is the convention that keeps each batch's expiry date and purchase cost attached to the
right units, which is what expiry alerts and profit reporting both depend on.

| | Cancellation | Return |
|---|---|---|
| Scope | The whole sale | One line, part or all of it |
| Who | Admin | Admin or Pharmacist |
| Sale status | → `Cancelled`; excluded from all reports | Unchanged; the sale stays `Completed` |
| Money | The whole `NetTotal` is reversed | `RefundAmount` per return row |
| Adjustment reason | `Sale cancelled: {reason}` | `Sales return: {reason}` |
| Repeatable | No — 409 on a second attempt | Yes, up to the line's quantity |

### Two edges worth knowing

**A cancellation does not restore stock a return already put back.** The brief says a
cancellation restores the stock from every line, and it does — but a line that has had ten of its
forty units returned already had those ten put back, with their own adjustment row. Adding forty
would invent ten tablets. Only each line's outstanding quantity comes back, and the response
reports how many lines were affected by that so it is visible rather than surprising.

**A cancelled sale accepts no returns.** The cancellation already restored the stock and reversed
the payment, so a return on top would restore twice and refund twice. Both the command and the
returnable-lines query refuse with 409 and say why — the query too, so the screen explains it
rather than showing an empty list that reads as "nothing left to return".

---

## 10. API endpoints

### Queries

| Endpoint | Access | Notes |
|---|---|---|
| `GET /api/products/sellable?search=&limit=` | Any tenant user | Type-ahead. Returns unsellable products with a `SellableStatus` and reason; excludes inactive ones. Carries unit configuration and current prices so a cart row prices itself without a second request. |
| `GET /api/sales` | Any tenant user | 25/page. Filters: `from`, `to`, `cashier`, `status`. Employee restricted to their own inside the query, so the row count is theirs too. |
| `GET /api/sales/{id}` | Any tenant user | Grouped invoice lines **and** per-batch lines. 403 for an Employee reading another cashier's. |
| `GET /api/sales/{id}/returnable-lines` | Admin, Pharmacist | Per line: sold, returned, still returnable, and what returning it all would refund. |
| `GET /api/sales/cashiers` | Admin, Pharmacist | Filter options, drawn from the sales rather than the staff list — so it never offers somebody with no sales and never omits a cashier who has left. Empty for an Employee. |
| `GET /api/sales/limits` | Any tenant user | The caller's own discount cap and permissions, for the billing screen's helper text. Reads `BillingPolicy`. |

### Commands

| Endpoint | Access | Notes |
|---|---|---|
| `POST /api/sales` | Any tenant user | 201. One transaction: invoice number, sale, lines, batch deductions. |
| `POST /api/sales/{id}/cancel` | **Admin only** | Body `{ reason }`. One transaction: status, adjustments, batch restores. 409 if already cancelled. |
| `POST /api/sales/{id}/returns` | Admin, Pharmacist | Body `{ saleLineId, quantity, quantityUnit, reason }`. One transaction: return row, adjustment, batch increment. |

Every command and query carries `ITenantScopedRequest`, so the pipeline refuses to run one
without a resolved pharmacy.

---

## 11. Out of scope

- **Non-cash payment.** Cash only; `CashReceived` must cover `NetTotal`.
- **Customer accounts, loyalty, purchase history.** Optional plain-text name and phone on the
  sale. A customer entity would invite duplicate records for the same person under three
  spellings and buy nothing this module needs.
- **Stacked or per-item discounts.** One bill-level discount per sale. Either alternative would
  break the single proportional split that makes returns honest.
- **Barcode scanning.**
- **Held or parked bills.** `SaleDate` exists separately from `CreatedOnUtc` partly so this
  remains addable without a migration.
- **Thermal printer integration.** CSS print media only; the driver and paper size are the
  user's environment.
- **Editing a completed sale.** There is no `Update` on `Sale`, no endpoint, and no screen.

### Why a completed sale cannot be edited

The money has moved and the customer is holding the paper. An edit would mean the pharmacy's copy
of a transaction no longer matches the customer's, with nothing to say which is right — and every
report that had already counted the original would silently change. So the corrective paths are a
return and a cancellation, both of which *add* a record rather than altering one: the original
stays readable, and the day's takings stay reconcilable.

The practical consequence, worth saying plainly to whoever runs the counter: a mistyped sale is
cancelled and rung up again. The cancellation carries the reason.

---

## 12. What the next modules depend on

**Module 6 — Expiry & low-stock alerts.** Reads `Batch` quantities, which sales now move.
Nothing new is required of it, but note that a sale is the only quantity change with no
`StockAdjustment` behind it, so an alert that reconstructed quantity history from adjustments
alone would be wrong.

**Module 7 — Antibiotic register.** Filters sales whose prescription columns are populated.
`Sale.HasPrescription` is the predicate; the six prescription columns and `SaleLine.ProductId`
joined to `Product.IsAntibiotic` are the data. Cancelled sales must be excluded.

**Module 8 — Reports.**

- Revenue is `Sale.NetTotal`, and **profit must use `SaleLine.NetLineTotal`**, not `LineTotal`.
  Using the pre-discount figure would overstate margin on every discounted sale.
- Cost comes from `Batch.PurchasePricePerBaseUnit` through `SaleLine.BatchId` — which is why the
  line is per batch rather than per cart item, since two batches of the same product may have
  cost different amounts.
- `Status = Cancelled` must be excluded from every total.
- Returns reduce revenue and profit: `SalesReturn.RefundAmount` against the line's own figures.
- The indexes it was promised are in place: `(TenantId, SaleDate)` and
  `(TenantId, CashierUserId)`.

---

## 13. Verified

- 71 unit tests over `SaleMath`, `Sale`, `SalesReturn`, `FefoAllocator` and `BillingPolicy`,
  including the brief's worked example, the three-line rounding residual, and the property that
  a line returned in four parts refunds exactly its net line total.
- 20 schema tests over migration 011: money precision, every CHECK constraint, the per-tenant
  unique invoice index, the reporting indexes, and the drop-order dependency.
