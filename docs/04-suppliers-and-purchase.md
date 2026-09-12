# 04 — Suppliers & Purchase Management

Who the pharmacy buys from, what arrived, what it cost, what has been paid, and what is still
owed.

**Built out of order.** Modules 5–8 were built first and each had to stub something out that
needed this one. Part E below records the three retrofits that closed those gaps.

---

## 1. Overview: this and the Add Stock screen

Module 3's **Add stock** creates a batch directly — no supplier record, no money owed. That
remains the right screen for stock with no bill behind it: opening inventory, free samples,
a correction, a transfer.

**A purchase is the same delivery with an invoice attached.** It records who supplied it, what the
bill came to and what remains unpaid — and it creates exactly the same batches, by calling exactly
the same code.

| | Add stock | New purchase |
|---|---|---|
| Creates a batch | yes | yes, one per line |
| Records a supplier | optionally, by name | required |
| Records money owed | no | yes |
| Multiple products at once | no | yes |
| Can be returned to a supplier | no | yes |

Both go through `CreateBatchCommand`. Module 3's own doc comment anticipated this:

> *Module 4 (Purchase) will send this same command rather than writing batches itself. A purchase
> line is a delivery, and duplicating the conversion, the duplicate-number policy and the loss
> check would guarantee the two paths drifted.*

So unit conversion, the medicine-needs-an-expiry rule, the duplicate-batch-number policy and the
sells-at-a-loss warning are **not implemented in this module at all**. The acceptance criterion
that proves it: entering a duplicate batch number through a purchase is refused with Module 3's
own message, identical to entering it through Add Stock.

---

## 2. Access control

**An Employee has no access to anything here.** Not the nav, not the pages, not the API. That is
stricter than the rest of the system — a cashier can look at stock, at alerts, at the products
they sell — but what the pharmacy pays for its goods, and to whom it owes money, is not counter
information.

| Action | Admin | Pharmacist | Employee |
|---|:---:|:---:|:---:|
| View suppliers and balances | ✓ | ✓ | |
| Add / edit a supplier | ✓ | ✓ | |
| Deactivate / reactivate a supplier | ✓ | | |
| View purchases | ✓ | ✓ | |
| Record a purchase | ✓ | ✓ | |
| Record a purchase return | ✓ | ✓ | |
| **Record a payment** | ✓ | | |
| Supplier dues report | ✓ | | |

**Two asymmetries are deliberate.**

A Pharmacist may record a purchase and a return but **not a payment**. Both of the first two are
stock events they are standing there performing — goods arrived, goods went back — and making them
find an Admin first is how stock records stop matching the shelf. Deciding that money has left the
till is not theirs.

A Pharmacist may **not** read the supplier dues report, even though they can see the same balances
on each supplier's page. The report is in Module 8, which is Admin-only throughout because every
report there joins to cost; carving an exception into it for one report would be the start of
unpicking that.

---

## 3. Schema

Five entities, all `ITenantEntity`. Migration `013_CreateSupplierAndPurchaseTables.sql`.

### Supplier
`Id, TenantId, Name*, ContactPerson, Phone*, Email, Address, Company, IsActive, CreatedOnUtc, ModifiedOnUtc`

Name and phone are the only required fields. Plenty of local distributors have neither an email nor
a postal address, and a form that demanded them would be filled in with nonsense.

**Soft delete only.** A supplier is referenced by every purchase ever recorded against them.
Deactivating hides them from the pickers and blocks new purchases; it hides nothing already bought
and — importantly — does not make a debt disappear. A deactivated supplier can still be paid.

### Purchase
`Id, TenantId, SupplierId*, PurchaseNumber*, PurchaseDate*, TotalAmount, AmountPaid, Notes, CreatedByUserId, …`

`PurchaseDate` is a `DATE`, not a timestamp: a delivery is booked in against a day, often the day
after it physically arrived, and a time of day would be invented precision.

`PurchaseNumber` is `PUR-000123`, unique **per tenant**. Two pharmacies both have a PUR-000001.

### PurchaseLine
`Id, TenantId, PurchaseId*, ProductId*, BatchId*, QuantityInBaseUnits, PurchasePricePerBaseUnit, LineTotal`

**These columns duplicate the batch's on purpose.** A batch records what is on the shelf *now* —
its quantity falls as stock sells. This line records what the supplier delivered and invoiced, and
that must never move. Reading the quantity off the batch instead would mean a purchase total
silently changing every time a customer bought something out of that delivery, which is the most
destructive thing this module could do to a supplier's account.

### SupplierPayment
`Id, TenantId, SupplierId*, PurchaseId (nullable), Amount*, Direction*, PaymentDate, PaymentMethod, Notes, RecordedByUserId`

`Direction` is `Payment` / `Refund` / `WriteOff` — see §4.2. Added by migration `014` with a
default of `Payment`, so every row written before it keeps its meaning. `Amount` is strictly
positive whichever way the money went.

`PaymentMethod` is free text rather than an enum: the column costs nothing today and adding bKash
or a bank transfer later should not need a migration. The UI offers Cash. It describes *how* money
moved; `Direction` describes *which way*, and the two are independent.

### PurchaseReturn
`Id, TenantId, PurchaseLineId*, BatchId*, QuantityInBaseUnits*, Reason*, ReturnAmount, ReturnedByUserId`

Targets a **line**, not a purchase: a delivery of six products is returned one product at a time,
and the credit depends on what that specific line cost.

### PurchaseSequences
A per-tenant counter, not mapped as an EF entity — same reasoning as `InvoiceSequences` in Module
5. See §7.

---

## 4. The outstanding balance

**Never stored. Computed on every read, in exactly one place.**

```
OutstandingBalance(supplier) =
      SUM(Purchase.TotalAmount)              purchases
    − SUM(PurchaseReturn.ReturnAmount)       goods sent back
    − SUM(payments      out to them)         Direction = Payment
    + SUM(refunds       back from them)      Direction = Refund
    + SUM(credits written off)               Direction = WriteOff
```

Per bill:

```
Due(purchase) = TotalAmount
              − AmountPaid                      (payments naming THIS bill; stored)
              − (returns against its lines)
              − GeneralPaymentApplied           (see §4.4.1; display only, never stored)
```

That is `ISupplierBalanceQueries` / `SupplierBalance.Outstanding`, and nothing else subtracts
payments from purchases anywhere in the codebase.

### 4.1 Why computed rather than stored

The balance derives from three tables that move independently — a purchase is recorded, a payment
goes out, goods go back. A stored total is wrong from the moment any one of them changes without
it, and nothing would make that visible. Recomputing costs three indexed aggregates and cannot
drift.

### 4.2 Money moves both ways

`SupplierPayment.Direction` says which — `Payment`, `Refund` or `WriteOff` — and `Amount` stays
strictly positive for all three. **A direction rather than a negative amount**: allowing the
amount below zero would leave a table called *payments* holding receipts, and every sum over it
would depend on a sign convention invisible from the column name.

Migration `014` adds the column with a default of `Payment`, so every row recorded before refunds
existed keeps its meaning and no balance changes.

Refunds and write-offs both **add back**, because both undo a credit — the supplier returning the
cash, or the pharmacy giving up on collecting it, leave nothing owed either way. They are kept
apart because only one involves cash, and a pharmacy reconciling its till has to be able to tell
them apart.

**Neither may name a purchase.** A credit belongs to the account — it is the net of that supplier's
returns and overpayments — and settling it against one bill would mean reducing that bill's
`AmountPaid`, which records money that genuinely changed hands. Enforced by the validator *and* by
`CK_SupplierPayments_IncomingIsAccountLevel`, because the constraint is the one that still holds
when somebody writes to the database directly.

### 4.3 It can be negative, and nothing clamps it

A pharmacy that paid a bill in full and then returned half the delivery is **owed money by its
supplier**. Flooring the figure at zero would hide a real credit. Every screen is written to say
"in credit" rather than pretend the account is settled — see §8 for why not "overpaid".

The brief's messy-sequence test is exactly this, and `acceptance_purchases.py` runs it:

| Step | | Balance |
|---|---|---|
| Purchase | 610.00 | 610.00 |
| Pay (linked to the bill) | 400.00 | 210.00 |
| Return goods | 225.00 | −15.00 |
| Pay again | 50.00 | **−65.00** |

`610 − 225 − 450 = −65`. Overpaid by 65.

### 4.4 General payments versus purchase-linked ones

A payment **with** a `PurchaseId` advances that bill's `AmountPaid` in the same transaction.

A payment **without** one is money against the account — "here's fifty thousand, put it against
what we owe" — which is how a great many pharmacy settlements actually happen. It reduces the
supplier's balance and **modifies no individual bill**.

Writing a general payment into some bill's `AmountPaid` would turn a guess into a stored fact
nobody could later distinguish from a real allocation. So nothing does.

### 4.4.1 But it is shown against the bills it covers

**Storing nothing and displaying nothing are different decisions, and only the first one was
right.** Shipping the first without the second produced this:

> Supplier page: **Outstanding 0.00 — nothing owed**
> Directly beneath it: **PUR-000007 · Unpaid · 765.00 due**

Both figures were correct. The account was settled; that bill had had no money booked against it
*specifically*, because the payments were general. The pair was unreadable, and a pharmacy looking
at it would reasonably conclude the software was wrong.

`ISupplierBalanceQueries.GetGeneralPaymentAllocationsAsync` closes it. Per supplier, it walks the
bills **oldest first** and applies the pool of unallocated payments to each bill's positive due,
capped at what that bill owes:

- Bills already settled — or in credit from a return — absorb nothing. Crediting a bill that owes
  nothing would push it further into credit and leave one that *is* owed still reading Unpaid.
- No bill takes more than it owes.
- Anything left over stays unallocated, which is the honest outcome when more has been paid than
  the bills come to: the credit belongs to the account, not to any one bill.

The figure arrives as `GeneralPaymentApplied` and is folded into `Due` and the status badge by
`PurchaseMath`. **Nothing is written.** `Purchase.AmountPaid` still reads 0 on a bill covered
entirely this way, and the acceptance harness asserts exactly that.

The allocation runs in **both** directions, and the second half is the mirror of the first:

- **Unallocated payments** cover bills that owe something, oldest first, reducing their due.
- **Refunds and write-offs** clear bills that are in *credit*, oldest first, raising their due back
  toward zero.

Without the second half, refunding a credit would settle the account while leaving a bill showing
−15.00 — the same class of contradiction as the one the first half fixed, just inverted.

The property worth relying on: **for each supplier, the sum of their bills' dues equals their
outstanding balance**, whichever way money has moved. That is what makes the two figures on the
page reconcile, and `acceptance_purchases.py` §16 and §17 assert it against the database.

All four read paths use it — the supplier's purchase history, the purchases list, the purchase
detail page, and the payment form's dropdown — so no two screens can disagree about whether a bill
is settled. A bill covered by general payments drops out of the "link this payment to a bill"
dropdown for the same reason any settled bill does: it owes nothing.

Every screen that shows it says so in words, because the allocation is a view rather than a
record and a reader is entitled to know which they are looking at.

### 4.5 The SQL shape, and the trap it avoids

SQL Server rejects *"Cannot perform an aggregate function on an expression containing an aggregate
or a subquery"*. This codebase has hit it in Modules 6, 7 and 8 — always at runtime, never at
compile time. The supplier list (a balance per row) and the dues report are exactly that shape.

So `SupplierBalanceQueries` never computes a balance inside a projection over suppliers. It runs
the three sums as their own grouped aggregates over the base tables and joins them in memory. The
supplier list pages its suppliers first and then asks for balances **for that page's ids**;
the dues report asks the same method for every id.

One method, one query shape, two callers — which is also why the paginated and export paths cannot
diverge the way Module 8's did.

---

## 5. API endpoints

**Suppliers** (`TenantWriterPolicy` unless noted)

| Method | Route | Notes |
|---|---|---|
| GET | `/api/suppliers` | 25/page; `search` matches name or phone; `status` |
| GET | `/api/suppliers/options` | active only, for dropdowns |
| GET | `/api/suppliers/{id}` | with purchased / paid / returned / outstanding |
| GET | `/api/suppliers/{id}/purchases` | 20/page, each with due and status |
| GET | `/api/suppliers/{id}/payments` | 20/page |
| GET | `/api/suppliers/{id}/unsettled-purchases` | for the payment form's dropdown |
| POST | `/api/suppliers` | |
| PUT | `/api/suppliers/{id}` | |
| PATCH | `/api/suppliers/{id}/deactivate` | **Admin** |
| PATCH | `/api/suppliers/{id}/reactivate` | **Admin** |
| POST | `/api/suppliers/{id}/payments` | **Admin**; `direction` = Payment (default), Refund or WriteOff |

**Purchases**

| Method | Route | Notes |
|---|---|---|
| GET | `/api/purchases` | 25/page; supplier, date range, payment status |
| GET | `/api/purchases/{id}` | lines and payments |
| GET | `/api/purchases/{id}/returnable-lines` | capped by prior returns **and** batch stock |
| GET | `/api/purchases/origins` | which batches came from a purchase (Module 6's retrofit) |
| POST | `/api/purchases` | one transaction: number → batches → lines → total |
| POST | `/api/purchases/{id}/returns` | one transaction: return + adjustment + batch |

**Reports** (`TenantAdminPolicy`)

| Method | Route |
|---|---|
| GET | `/api/reports/supplier-dues` |
| GET | `/api/reports/supplier-dues/export` |

---

## 6. Transactions, and a trap worth recording

Purchase creation is one transaction: allocate the number → for each line send
`CreateBatchCommand` → build the `Purchase` with its lines → save. A failure on the third line
rolls back the first two batches, the purchase and its number. No orphan stock, no gap in the
sequence.

### 6.1 `ExecuteInTransactionAsync` commits on return

**`IUnitOfWork.ExecuteInTransactionAsync` commits as soon as the delegate returns — it inspects
nothing about the value.** Returning a failed `Result` from inside it therefore *commits* whatever
had already been written.

Module 5's handlers are unaffected because they validate everything before writing anything, so a
refusal has nothing to roll back. Purchase creation cannot work that way: the failure can only be
discovered after earlier lines have already created batches.

So `CreatePurchaseCommandHandler` throws a private `PurchaseRefusedException` carrying the `Error`,
catches it immediately outside the transaction, and converts it back to a `Result`. An exception is
the only signal that reaches the rollback.

The payment and return handlers keep the validate-then-write ordering instead, which is simpler
where it is available. Both say so in their class comments.

### 6.2 The purchase return

One transaction: `Batch.Adjust` (which is the only thing that can change a quantity, and which
returns the audit row that explains it) → the `PurchaseReturn` row → save. The stock adjustment is
not optional; there is no code path that moves stock without one.

The adjustment's reason is auto-prefixed `"Purchase return: "` so a batch's history reads as one
event rather than an unexplained write-off.

**`Purchase.AmountPaid` is untouched by a return.** A return does not un-pay money already handed
over; it reduces what is still owed. If the bill was settled in full, the balance goes into credit.

### 6.3 The returnable cap

```
Returnable = max(0, min(delivered − already returned, batch quantity now))
```

**The batch cap is the half that gets forgotten.** If the stock was sold or written off it is not
on the shelf to hand over, whatever the invoice said. Without it a return would drive a batch
negative, or be refused by the domain with an error the person could not act on.

The screen says *which* limit applied, because "you already sent most of it back" and "it has been
sold" are different problems to solve.

---

## 7. Purchase numbering

`PUR-000123`, per tenant, allocated by one atomic statement:

```sql
UPDATE dbo.PurchaseSequences SET NextNumber = NextNumber + 1
OUTPUT deleted.NextNumber AS [Value]
WHERE TenantId = @tenantId
```

**Deliberately not count-of-purchases + 1.** Two deliveries booked in at the same moment would both
read the same count and both claim PUR-000124; the unique index would reject one, losing a real
purchase to a race.

Allocated *inside* the purchase transaction, so a failed purchase rolls its number back and the
sequence has no gaps. Same design as `InvoiceNumberGenerator`, against its own counter table; the
duplication is intentional, since a shared "sequence service" would thread a name parameter through
every call site to buy nothing.

---

## 8. Key decisions

**Why a purchase cannot be edited or deleted.** It states what a supplier delivered and invoiced,
and both the stock and the balance have moved on that basis. Editing the total would silently
restate a debt; deleting it would orphan batches that have since been sold from. The corrective
paths are a **purchase return** (goods going back) and a **stock adjustment** (a counting error),
both of which leave a record of the correction rather than quietly replacing the original. The
purchase detail page says so in as many words.

**Why `PurchaseLine` duplicates `Batch` fields.** §3.

**Why no duplicate-name check on suppliers.** Two distributors can genuinely share a name, and a
pharmacy that has recorded the same one twice by accident has a tidying problem rather than a
constraint violation — refusing the second would leave them unable to record a delivery arriving
now. The list's search is what surfaces the duplicate.

**Why a general payment is stored unallocated but displayed allocated.** §4.4.1. The short
version: which bill an unallocated payment settled is genuinely unknown, so storing an answer would
be inventing evidence — but refusing to show one produced a settled account above a bill badged
Unpaid, which is worse. Store nothing; display oldest-first; label it as a view.

**Why an overpayment warns rather than blocks.** A rounded cash settlement, an advance on the next
delivery, a payment entered against the wrong bill — all real. Refusing would leave somebody unable
to record money that has genuinely left the till. The API returns a flag and a sentence; the
balance goes negative and says so. The same applies in reverse: a refund larger than the credit
means the supplier handed back too much, which is a fact to record rather than an input to reject.

**Why a credit has three ways out, and only two of them are recorded.** The next delivery absorbs
it automatically — a −65.00 balance plus a 200.00 purchase is 135.00, and nothing needs entering.
That is how distributors normally clear one. The supplier handing the cash back is a `Refund`;
giving up on a credit nobody will collect is a `WriteOff`, which needs a reason because otherwise
the dues report loses a figure with no explanation.

**Why "in credit" rather than "overpaid".** The state arises just as often from returning goods
after a bill was paid, which is not an overpayment at all. The screens name the state, not one of
its causes.

**Why a deactivated supplier can still be paid.** The debt is exactly why their record is still
there. What deactivation blocks is recording a *new* delivery against them.

**Why the status filter on the purchases list costs a full read.** A purchase's status is not a
column: it depends on returns booked against its lines. Filtering in SQL would need a correlated
aggregate in the WHERE — the shape that has failed at runtime three times here — and storing the
status would mean a column that goes stale the moment a return is recorded. So when a status filter
is applied, the matching purchases are read, their returns fetched in one grouped query, and the
page taken in memory. The set is bounded by the supplier and date filters the same screen offers,
and the default path (no status filter) pages in the database as usual.

---

## 9. Part E: retrofits performed

### 9.1 Module 8 — the supplier dues report

The reports landing page carried a **visibly disabled card** saying the report needed this module.
That card is now a working link, and `GET /api/reports/supplier-dues` is implemented.

It calls `ISupplierBalanceQueries.GetBalancesAsync` for the outstanding figure. It does **not**
reimplement the formula, and `acceptance_purchases.py` asserts that every supplier's figure on the
report matches their detail page exactly — a discrepancy would mean the service had been
duplicated.

**One judgment call worth stating.** The endpoint takes `dateFrom` / `dateTo` / `allTime` and
defaults to all time, unlike every other report in Module 8. A date range narrows the *purchased,
paid and returned* columns; the **outstanding column always covers all time**. What a supplier is
owed is a fact about now, and a "balance" computed from one month's purchases against one month's
payments is not money anybody owes anybody. The page labels the two differently and says so in a
banner when a range is applied.

`docs/08-reports-and-profit.md` has been updated: the "supplier dues unavailable" note is gone.

### 9.2 Module 6 — "Return to supplier" on expired stock

`/alerts/expired` now offers **Return to supplier** for a batch that arrived on a recorded
purchase, linking straight to that purchase's return screen with the line pre-selected. Batches
entered through Add Stock keep **Adjust stock**, because there is no bill to send them back on.
Where a return is possible, both are offered — a supplier who refuses the return still leaves
somebody needing to clear the shelf.

**The test is a `PurchaseLine` reference, not a supplier id.** Those were the same question until
this module; now Add Stock can name a supplier on a batch entered by hand, and such a batch has a
supplier and no purchase. `AlertPresentation.ActionFor` takes the origin, and
`GET /api/purchases/origins` answers for a whole page of batches at once rather than one query per
row.

### 9.3 Module 3 — the supplier field on Add Stock

`Batches.SupplierId` had existed as a nullable column with no foreign key since migration 008,
with the entity noting *"the Supplier entity arrives in Module 4 and the column is here so that
migration adds a constraint rather than a column."* Migration 013 adds
`FK_Batches_Suppliers`, and the Add Stock form's supplier field is now a searchable dropdown of
active suppliers instead of free text. It remains **optional** — this screen exists for stock with
no bill behind it.

The constraint is added `WITH NOCHECK`. It validates nothing already there and is enforced from
here on, which is the intent.

### 9.4 Known historical-data gap

**Free-text supplier names on existing batches are left exactly as they are.** No attempt is made
to match `SupplierNameText` to a `Supplier` record, and none should be: "Popular" on a delivery
note might be Popular Pharmaceuticals, or a rep, or a different distributor entirely, and a wrong
automatic match would attribute stock to a supplier it never came from — invisibly, and in a place
a balance is computed from.

So: batches created before this module display their free-text name and have no supplier id.
Batches created after it have both — the id is the record, the text is what was written on the
delivery note. Both columns are kept on `Batch` permanently for that reason.

---

## 10. Out of scope

- Payment methods beyond Cash. The schema takes any string; the UI offers one. (The
  *direction* of money is not a method and is fully supported — see §4.2.)
- Purchase orders and approval workflow. This records purchases *after the fact*.
- Supplier performance analytics.
- Recurring or scheduled purchases.
- **Editing or deleting a completed purchase** — see §8.
- Retroactively matching historical free-text supplier names — see §9.4.
- **A written-off credit is not reflected as a cost in any report.** Module 8's gross profit is
  sales revenue less batch cost, and net profit subtracts operating expenses, which come from the
  unbuilt Salary module. A credit given up is real money lost — the pharmacy paid for goods it
  returned and never recovered the cash — but it leaves the supplier balance without appearing as
  a loss anywhere. Surfacing it would mean deciding where purchase-side losses belong in the
  profit model, which is a Module 8 question rather than a Module 4 one.

---

## 11. Files

**Domain** — `Entities/{Supplier, Purchase, PurchaseLine, SupplierPayment, PurchaseReturn}.cs`

**Application** — `Common/DTOs/{SupplierDtos, PurchaseDtos}.cs`,
`Common/Purchasing/{PurchaseMath, SupplierPaging}.cs`,
`Interfaces/{ISupplierBalanceQueries, ISupplierQueries, IPurchaseQueries, IPurchaseNumberGenerator}.cs`,
`Features/Suppliers/**`, `Features/Purchases/**`,
`Features/Reports/Queries/GetSupplierDues/**`

**Persistence** — `Services/{SupplierBalanceQueries, SupplierQueries, PurchaseQueries, PurchaseNumberGenerator}.cs`,
`Configurations/SupplierAndPurchaseConfigurations.cs`

**API** — `Controllers/{SuppliersController, PurchasesController}.cs`, dues endpoints on
`ReportsController.cs`

**Database** — `013_CreateSupplierAndPurchaseTables.sql`,
`014_AddSupplierPaymentDirection.sql`

**Tests** — `tests/PMS.UnitTests/Application/Purchasing/PurchaseMathTests.cs` (30)

**Harnesses** — `acceptance_purchases.py` (175), `web_smoke_purchases.py` (153)

Frontend: see `docs/frontend/04-suppliers-and-purchase.md`.
