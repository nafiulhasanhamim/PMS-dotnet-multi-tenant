# 03 — Batches & Stock

The module that holds the actual physical stock. Every module after this one reads from it:
Purchase (4) creates batches, Billing (5) deducts from them, Alerts (6) watches their expiry
dates, and Reports (7) values them.

Frontend: [frontend/03-batches-and-stock.md](frontend/03-batches-and-stock.md).

---

## 1. What a batch is

**One delivery of one product**: a quantity that arrived together, with its own expiry date and
its own cost.

A pharmacy holds Napa 500 bought in March at ৳0.80 expiring next January, and more of it bought
in July at ৳0.85 expiring the following June. Those are two different physical things on the
same shelf, and that is the whole reason this table exists.

### Why cost and expiry are here and not on the product

Putting either on `Product` forces a single answer to a question that has several:

| | With cost on the product | With cost on the batch |
|---|---|---|
| "What did this cost?" | whichever price was entered last | what *that pack* cost |
| Margin on a sale | computed against the wrong figure | computed against the batch sold |
| "When does this expire?" | one date for stock with several | the date on the pack |
| Expiry alerts | cry wolf, or stay silent | accurate per pack |

The failure mode is quiet. Nothing errors; the numbers are simply wrong, and they stay wrong
until somebody reconciles stock against money and cannot.

### Quantities are integer base units

Pieces for tablets, bottles for handwash, bags for saline, tins for formula. **Nothing in this
module knows which** — the unit vocabulary belongs to `Product`, and the conversion from what a
person typed ("2 cartons") happens in the application layer before the insert. See
[§7](#7-how-this-module-uses-module-2s-unit-helpers).

Prices are `decimal(18,4)`. Never floating point.

---

## 2. Access control

| | View stock list | View batches | See purchase price | Add batch | Edit batch | Adjust | Adjustment history |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Admin** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Pharmacist** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Employee** | ✓ | ✓ | **✗** | ✗ | ✗ | ✗ | **✗** |

Admin and Pharmacist are **identical** here. A pharmacist is the person who takes a delivery in
and who counts a shelf; a module they could only read would be unusable. Module 2 draws the line
differently — only an Admin may deactivate a product — because that changes what everyone else
can sell.

### Two things an Employee cannot have

**The purchase price.** Withheld by not being read: `GetProductStockQuery` and `GetBatchQuery`
carry an `includePurchasePrices` flag, decided in `StockController` from the token's role, and
the projection skips the column when it is false. The value does not cross the wire. The column
missing from the screen is a consequence, not the control — anyone can open a network tab.

This is stricter than Module 2, where an Employee *does* see the sale price on a detail page,
because somebody at the counter has to answer "how much is this?". Nobody at the counter needs
the cost, and the margin it reveals is the pharmacy's own business.

**The adjustment history.** The one *read* in this module an Employee cannot make. It is who
wrote off what and why — staff-conduct information rather than stock information.

---

## 3. Schema

### Batch

`Batch` implements `ITenantEntity`, so the tenant filter and the insert stamp are applied by
convention. No handler in this module filters by tenant, and none may.

| Column | Type | Notes |
|---|---|---|
| `Id` | uniqueidentifier | |
| `TenantId` | uniqueidentifier | stamped by the interceptor, never by a handler |
| `ProductId` | uniqueidentifier | FK → Products, **Restrict** |
| `BatchNumber` | nvarchar(100) | the manufacturer's number off the pack |
| `ExpiryDate` | **date, null** | see below |
| `ManufactureDate` | date, null | |
| `PurchasePricePerBaseUnit` | decimal(18,4) | unrounded |
| `QuantityInBaseUnits` | int | ≥ 0 by CHECK; changes only via an adjustment |
| `InitialQuantityInBaseUnits` | int | > 0 by CHECK; **set once, never updated** |
| `SupplierId` | uniqueidentifier, null | **no FK yet** — Module 4 |
| `SupplierNameText` | nvarchar(200), null | what the delivery note said |
| `Notes` | nvarchar(1000), null | |
| `IsActive` | bit | for a row entered in error — **not** depletion |
| audit + `RowVersion` | | as every other entity |

`date`, not `datetime2`: a pack prints a month and a year, nothing about an expiry happens at a
particular time of day, and a time component is one more thing a time-zone conversion could
shift across a day boundary.

**Constraints.** `CK_Batches_QuantityNotNegative` is the important one — the last line behind
the application check and the domain guard. `CK_Batches_InitialQuantityPositive` (a batch that
arrived empty is not a delivery, and zero would break every turnover calculation that divides by
it), `CK_Batches_PurchasePriceNotNegative`, and `CK_Batches_ManufactureBeforeExpiry` (only when
both dates are present — a pack showing only an expiry is normal).

**Indexes.** `UX_Batches_Tenant_Product_BatchNumber` (unique) and
`IX_Batches_Tenant_Product_ExpiryDate` (the FEFO read). The second does not remove the sort —
the ordering puts null expiries *last* with an explicit key and no index can be read in that
order. It does not need to: a product holds a handful of live batches. What it is for is finding
those rows without touching another product's, and answering the stock list's expiry filters
without scanning every batch in the pharmacy.

### Why `InitialQuantityInBaseUnits` exists, and why nothing may modify it

**It is the denominator.** Turnover, wastage as a share of a delivery, and "did this batch sell
before it expired" all need to know how much *arrived* — and by the time anyone asks, the live
quantity has been reduced by every sale and every write-off.

It is also the sanity check that catches a bug elsewhere. The live quantity must always equal
this figure plus the sum of the batch's adjustments minus what was sold. Nothing else can be
true, so a discrepancy is a defect, not a mystery.

**Nothing modifies it because stock arriving later is a new batch**, with its own expiry and its
own cost. Increasing this figure instead would blend two deliveries into one row and destroy
both. Enforced in three places: no method on `Batch` writes it after the constructor, `Adjust`
touches only the live quantity, and the tests assert it across a sequence of adjustments.

### Why `ExpiryDate` is nullable, with validation by product type

Diapers, syringes and dressings genuinely do not expire. A mandatory column would be filled in
with an invented date — which is **worse than no date**, because it looks correct and it drives
the expiry alerts.

So the column is nullable and the rule is conditional: **`ProductType = Medicine` requires an
expiry date.** That rule lives in the handler and not in the schema, because it depends on a
column of another table and a CHECK constraint cannot see one. `CreateBatchCommandHandler` and
`UpdateBatchCommandHandler` both enforce it and return a field error against `ExpiryDate`, so it
lands under the right input on the form.

One asymmetry worth knowing: **a new delivery cannot be given a past expiry, but an edit can.**
A pharmacy recording the stock already on its shelves will have packs that expired last month,
and that is precisely the stock somebody has to go and pull. Refusing the date would leave it
unrecorded or entered fictionally. Nobody, on the other hand, takes delivery of expired stock on
purpose — so on create it is a typo worth catching. Both directions are bounded by
`BatchWriteRules.MaxYearsAhead` (30), which catches a mistyped 2206.

### StockAdjustment

Every non-sale change to a batch quantity.

| Column | Type | Notes |
|---|---|---|
| `Id`, `TenantId` | uniqueidentifier | |
| `BatchId` | uniqueidentifier | FK → Batches, **Cascade** |
| `AdjustmentType` | int | 0 Add, 1 Remove, 2 Correction |
| `QuantityChangeInBaseUnits` | int | signed delta; ≠ 0 by CHECK |
| `QuantityAfterInBaseUnits` | int | ≥ 0 by CHECK |
| `Reason` | nvarchar(500) | required, non-blank by CHECK |
| `AdjustedByUserId` | uniqueidentifier | **no FK** — see below |
| audit + `RowVersion` | | |

**A sale is deliberately not an adjustment type.** Sales deduct stock in Module 5 and are audited
by their own sale lines, which carry the price charged and the customer. Folding them in would
produce two competing records of one event and drown the handful of entries that actually need
explaining.

**The delta is stored, not the new total,** because the delta is what sums: "how much did we
write off this quarter" is one `SUM` over that column. `QuantityAfterInBaseUnits` is redundant
with the deltas and kept anyway — replaying deltas only works if no row is ever missing, and this
column is what makes a gap *visible* rather than silently shifting every later figure.

**Cascade delete here, unlike everywhere else in this schema.** An adjustment has no meaning
without its batch, so an orphan would be an audit row nobody could interpret. Nothing deletes a
batch today; the constraint settles what happens if anything ever does.

**No foreign key to `Users`,** matching the entity. `Users` is a global table with no `TenantId`
— one person can work at two pharmacies — and this is a tenant-scoped row. The reference is by
id and the name is resolved by an explicit join in `ListAdjustmentsAsync`, which keeps that
crossing visible instead of burying it in a navigation that every read would traverse.

---

## 4. The FEFO helper

**First Expire, First Out.** The order in which stock must be sold, and the rule every deduction
in the system will follow. Built and tested here; Module 5 consumes it. **Not wired into any
sale — there are no sales yet.**

```csharp
// The pure form, over any collection. Used by the tests.
IEnumerable<Batch> Fefo.GetActiveBatchesFefo(IEnumerable<Batch> batches, DateOnly today)

// The composable parts, for EF and for memory.
IQueryable<Batch>  Sellable(this IQueryable<Batch>, DateOnly today)
IOrderedQueryable<Batch> InFefoOrder(this IQueryable<Batch>)

// The database form, the signature the spec asks for.
Task<IReadOnlyList<Batch>> IStockQueries.GetActiveBatchesFefoAsync(Guid productId, CancellationToken)
```

### Sellable

`IsActive` **and** `QuantityInBaseUnits > 0` **and** (`ExpiryDate is null` or `>= today`).

- `IsActive` — a row entered in error is not stock. Note this is *not* depletion: a sold-out
  batch stays active and stays on the detail page, because it is the cost and expiry behind
  sales that already happened.
- Expired stock is excluded from selling but still shown on screen in red, because somebody has
  to physically remove it.
- A batch expiring **today** is sellable. Stock is good until the end of the printed day, and
  excluding it would write off a day of stock every time.

### The ordering, and the trap in it

1. **`ExpiryDate IS NULL` last** — an explicit key, not a side effect.
2. `ExpiryDate` ascending.
3. `CreatedOnUtc` ascending as the tiebreaker.

Stock that never expires must be sold **after** anything with a date, however distant that date
is: stock with a deadline is the stock at risk. **SQL Server sorts NULL first on an ascending
order by, and LINQ-to-Objects does the same** — the exact opposite. So the ordering leads with a
`has no expiry` key rather than relying on either provider's null handling.

Getting this wrong does not throw and does not look wrong on any screen. It quietly sells the
non-expiring stock first and lets the dated stock expire on the shelf.

The tiebreaker earns its place too: two deliveries genuinely can share an expiry date, and
without it the order between them is whatever the query plan produced — stable enough to pass a
test once and to vary in production. Oldest delivery first, which is what a pharmacist does by
hand anyway.

**One definition, two providers.** The predicate and the three ordering keys are declared once as
`Expression` objects; the `IQueryable` overloads hand them to EF, and the `IEnumerable` overloads
use compiled copies. There is no second place to edit.

> **A translation trap worth knowing.** `Sellable` composes an expression, so calling it *inside*
> a lambda puts a method call in the expression tree that EF cannot translate. In
> `StockQueries.ListAsync` the sub-queryables are composed **outside** the lambdas for exactly
> this reason, and the comment there says so.

### The seven test cases

All in `tests/PMS.UnitTests/Application/Stock/FefoTests.cs` (12 tests in total).

| Case | Test |
|---|---|
| Several expiries → expiry order | `OrdersByExpiryAscending` |
| Depleted (qty 0) → excluded | `ExcludesDepletedBatches` |
| Expired → excluded | `ExcludesExpiredBatches` |
| Null expiry → included, last | `IncludesBatchesWithNoExpiry` |
| Mixed null and dated → dated first, nulls after | `SortsNullExpiriesLast` |
| Nothing sellable → empty | `ReturnsEmptyWhenNothingIsSellable` |
| Identical expiry → tiebroken by `CreatedOnUtc` | `BreaksAnExpiryTieByOldestFirst` |

Plus five that guard the edges: a batch expiring today is included, an inactive batch is
excluded, no batches at all returns empty, the result is independent of input order, and
`InFefoOrder` **without** the filter keeps an expired batch at the top — which is what the
detail page relies on.

---

## 5. API endpoints

All tenant-scoped via `ITenantScopedRequest`; none takes a tenant id.

### Queries

| Endpoint | Returns | Notes |
|---|---|---|
| `GET /api/stock` | `GridResult<StockListItemDto>` | one row **per product**, aggregated. Params: `search`, `stockStatus`, `expiryStatus`, `productType`, `page`, `pageSize` (25) |
| `GET /api/stock/product/{productId}` | `ProductStockDto` | summary + active batches in FEFO order + a page of depleted ones (`depletedPage`, `depletedPageSize` = 10) |
| `GET /api/stock/batch/{batchId}` | `BatchDto` | for the edit and adjust screens |
| `GET /api/stock/batch/{batchId}/adjustments` | `GridResult<StockAdjustmentDto>` | 20/page. **Admin/Pharmacist only** |

**The stock list reports sellable stock, with expired stock alongside rather than inside it.**
`TotalQuantityInBaseUnits` excludes expired batches; `ExpiredQuantityInBaseUnits` reports them
separately. Counting expired stock in the headline would show a shelf of unsellable packs as
healthy and keep the product off the reorder list; leaving it out of the row entirely would lose
the fact that there is something physically there to dispose of. Reporting both is the only
version that is true, and it is why a row can read "Out of stock" with a red expiry cell and a
quantity beside it.

`BatchCount` *does* include expired batches — they are on the shelf, and a count that omitted
them would disagree with the detail page for no reason a user could work out.

`NearestExpiryDate` excludes null expiries rather than treating them as far-future, so a product
whose stock never expires reports null — an em dash, not "safe for ever".

### Commands

| Endpoint | Notes |
|---|---|
| `POST /api/stock/batches` | quantity + unit level, price + unit level; converted before persisting |
| `PUT /api/stock/batches/{id}` | batch number, expiry, manufacture date, price, supplier, notes. **Quantity refused** |
| `POST /api/stock/batches/{id}/adjust` | one transaction: adjustment row + quantity change |

**The loss warning is non-blocking.** A cost above the product's sale price returns
`sellsAtALoss: true` with a message, and the batch is saved. A pharmacy really does buy above its
own list price and reprice afterwards; refusing the entry would leave the stock unrecorded, which
is worse than recording it flagged. The form confirms before posting, from the price it already
has on screen; the server flag is the authoritative answer for every other caller and catches the
case where the product was repriced in between.

**A duplicate batch number under a race** comes back as a 409 with a usable message, not a 500.
`ApplicationDbContext.SaveChangesAsync` translates SQL Server error 2601/2627 into
`DuplicateKeyException` — a type in SharedKernel, because the Application layer holds no
reference to EF Core or a database driver — and the handler catches it.

---

## 6. Key decisions

### Quantity is not editable on the edit form

Every change to a quantity has to arrive with a reason attached, and the edit endpoint has
nowhere to put one.

The endpoint accepts a `quantityInBaseUnits` field **only so that an attempt to change it can be
refused out loud**, with a message pointing at the adjustment path. A silently ignored field is
worse than one that errors: a client would post a new quantity, get a 200 and a batch back, and
reasonably conclude the change took effect. Sending the *current* value is accepted as the no-op
it is, so a client echoing the whole object back is not punished for it.

### Adjustments are a separate table, and the reason is mandatory

A quantity column tells you what the shelf holds now. It cannot tell you that eighty pieces were
written off as water damage in July, which is the question an owner asks when the numbers do not
match the money.

Reason is required and free text. A fixed list would be filled in as "Other" for exactly the
cases worth reading later; the form offers quick-pick buttons for the common ones without closing
the door on the unusual one.

**The audit row cannot be skipped, by construction.** `StockAdjustment`'s constructor is
`internal` to the domain, and the only caller is `Batch.Adjust`, which applies the quantity change
and returns the adjustment it just produced. There is no code path that mutates one without
constructing the other.

> **How they reach the database together — three attempts, all worth recording, because Module 5
> has to write a near-identical path.**
>
> 1. **An explicit `BeginTransaction` throws.** The context enables retry-on-failure, and
>    `SqlServerRetryingExecutionStrategy` refuses a user-initiated transaction: it cannot retry a
>    block whose boundaries it does not control. `IUnitOfWork.BeginTransactionAsync` is therefore
>    unusable as this context is configured, and now carries a remark saying so.
> 2. **Saving and letting EF discover the adjustment through the batch's navigation emits an
>    `UPDATE`, not an `INSERT`.** When change tracking finds an untracked entity hanging off a
>    tracked one it decides the state from whether the key is set — and this one carries a `Guid`
>    from its constructor, so EF concluded the row already existed. The result was an `UPDATE`
>    against nothing: nought rows affected, and a `TenantId` of all zeroes, because the tenant
>    interceptor only stamps inserts.
> 3. **What works:** add the adjustment explicitly, so its state is unambiguously `Added`. The
>    repository's `AddAsync` saves, and that one save carries the pending quantity change on the
>    tracked batch along with it. One `SaveChanges`, one transaction of EF's own making, both rows
>    or neither — atomic *and* retriable.

### The duplicate batch number policy

**Chosen: keep `(TenantId, ProductId, BatchNumber)` strictly unique. A depleted batch keeps its
number reserved.** The alternative the spec offered — relaxing the constraint to include
`CreatedAt` — was rejected.

Two outcomes, both refusals, differing in what they tell the person to do:

| Existing batch | Response | Message |
|---|---|---|
| has stock | 409 | "…already exists and still has stock. Edit that batch, or record this delivery under a different batch number." |
| depleted | 409 | "…is sold out, but its record is kept as the history behind those sales, so the number cannot be reused. Try 'B-101-2' for this delivery." |

**Why not relax the constraint.** A recall notice names a batch number. "Recall batch B-100 of
Napa" has to resolve to exactly one row — including a row that sold out, which is precisely the
stock that has already left the shop and may need chasing. With duplicates allowed, every
downstream module (returns, recalls, adjustment history, a supplier query) would have to
disambiguate by creation date, and nobody types a creation date. The cost of the strict rule is
one suffix on the rare occasion a manufacturer reuses a number; the cost of the relaxed rule is
an ambiguous recall.

The refusal for a depleted batch explains itself and suggests the suffix, so it is not a dead end
— the acceptance run confirms `B-101-2` is accepted immediately afterwards.

**The same batch number on a *different* product is allowed**, and so is the same number in
another pharmacy. Uniqueness is per product per tenant.

**Quantities are never auto-merged.** Two deliveries folded into one row would have one expiry
and one cost, and both would be wrong.

### The wrong-screen guard

Adding stock to a batch that has expired, or expires within
`StockPolicy.AddToExpiringBatchWarningDays` (30), is refused unless the caller sets
`acknowledgeExpiringBatch`.

It catches one specific, common and expensive mistake: fresh stock arrives, and instead of
creating a batch somebody adds the quantity to the batch already on screen. The new stock then
silently inherits the old batch's expiry date and cost — it gets written off months early, or
sold expired, and the margin on it is wrong either way.

Enforced in the API and not only in the page, because a warning that lives in one client is not a
guard. Three details:

- **Only additions.** Removing stock from an expired batch is the correct thing to do with it —
  that is disposal — and a warning there would train people to click through warnings.
- **Judged on the sign of the delta, not the adjustment type**, so a *correction upwards* is
  caught too. Keying off `AdjustmentType.Add` alone would leave an obvious hole.
- **Thirty days, not the ninety-day alert window.** Adding to a batch that expires in two months
  is ordinary — a miscount corrected. Adding to one that expires this month almost always means
  the wrong screen.

### Where the policy numbers live

`StockPolicy` holds `ExpiringSoonWindowDays` (90) and `AddToExpiringBatchWarningDays` (30). Both
belong to the pharmacy and both move to per-tenant Settings later; gathering them in one file
means that change edits one place and a handful of call sites that already pass the value as a
parameter, rather than hunting literal 90s through queries, pages and alerts.

---

## 7. How this module uses Module 2's unit helpers

`UnitConversion.ToBaseUnits`, `FromBaseUnits`, `PricePerBaseUnit` and `DescribePacking` are
called, never reimplemented. Nothing in this module hardcodes a pharmaceutical unit name.

- **Input.** The command carries `Quantity` + `QuantityUnit` and `PurchasePrice` +
  `PurchasePriceUnit`. The handler converts both before anything is persisted, so 20 strips at
  ৳8 becomes 200 pieces at ৳0.80. Accepting the entered unit rather than making the client
  convert is deliberate: a client doing its own arithmetic would be a second implementation of
  the two-level packing rule.
- **The two-level trap.** A product with a bulk pack but no middle pack stores *base* units in
  `MidPerLarge` — 24 bottles per carton, not 24 strips. Nothing here multiplies those fields;
  it all goes through `Product.BaseUnitsPerLarge`. Verified end to end: 2 cartons of handwash
  stores 48 bottles, and one box of Napa resolves to 100 pieces.
- **Output.** Every quantity on the wire carries a formatted string built from the product's own
  unit names, so the API, the web pages and any future mobile client phrase it identically. Doing
  it in the client would mean three phrasings of the same number.
- **Failure.** A unit level the product does not define throws from `UnitConversion`, and the
  handler turns that into a field error rather than letting it become a 500 — a stale form is a
  message, not a crash. A quantity that does not divide into whole base units is likewise a field
  error, never rounded: silently turning half a strip into 0 or 1 loses or invents stock.

---

## 8. Out of scope

- **Selling and stock deduction — Module 5.** This module does not sell anything. The FEFO helper
  is built and tested, and nothing calls it.
- **Purchase entry creating batches — Module 4.**
- **`SupplierId` as a real foreign key.** The column exists; the constraint arrives with the
  Supplier entity.
- **Automatic expiry and low-stock alerts, dashboard panels — Module 6.** The data and the
  server-computed states (`ExpiryState`, `StockStatus`) are ready for it.
- Barcode scanning; multi-location and warehouse transfers.

---

## 9. What the next modules depend on

**Module 4 (Purchase)** should send `CreateBatchCommand` rather than writing batches itself. A
purchase line *is* a delivery, and duplicating the unit conversion, the duplicate-number policy
and the loss check would guarantee the two paths drifted. It will need to set `SupplierId` and
add the foreign key.

**Module 5 (Billing)** needs two things from here:

1. `IStockQueries.GetActiveBatchesFefoAsync(productId)` — tracked entities, in sell order, so a
   sale can take quantity from each in turn.
2. The deduction path. `Batch.Adjust` is **not** it: a sale is audited by its own sale line, not
   by a stock adjustment. What Module 5 should copy is the *shape* — a domain method that changes
   the quantity and returns the record explaining it, so neither can exist without the other, and
   a single `SaveChanges` to land them together. Read the three-attempt note in
   [§6](#adjustments-are-a-separate-table-and-the-reason-is-mandatory) before writing it; a sale
   spanning several batches will need an execution-strategy-aware wrapper, because the retrying
   strategy still refuses a hand-rolled transaction.

Both will be filtered to one pharmacy without asking, because `Batch` implements `ITenantEntity`.

---

## 10. Verified

`database/scripts/008_CreateBatchesAndStockAdjustmentsTables.sql`, registered in `000_RunAll` and
`099_DropAllTables` (dependants first).

- **99 API acceptance checks**, all passing: unit conversion in both directions, the conditional
  expiry rule, every duplicate case, the adjustment paths including the wrong-screen guard, the
  full role matrix, tenant isolation of the aggregates, audit sums, and pagination of all three
  paged lists.
- **48 frontend checks** across all six pages and all three roles.
- **Database guards proven by direct SQL**, bypassing the application entirely:
  `CK_Batches_QuantityNotNegative`, `CK_StockAdjustments_ChangeNotZero`,
  `CK_StockAdjustments_ReasonNotBlank` and `UX_Batches_Tenant_Product_BatchNumber` each refuse
  the write.
- **Unit tests**: 12 FEFO, 14 `Batch` invariants. `FilterInventoryTests` pins both new entities as
  tenant-filtered — worth pinning, because the stock list aggregates across batches, so an
  unfiltered `Batch` would fold another pharmacy's quantities into this one's totals rather than
  merely showing an extra row somebody might notice.
