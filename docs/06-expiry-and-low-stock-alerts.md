# 06 — Expiry & Low-Stock Alerts

The smallest module so far, and the one that makes the previous three visible. Every figure here
was already sitting in `Batches` and `Products`; nothing surfaced it.

**No new entities. No migration. No writes.** Four queries and the screens that show them.

---

## 1. Overview

A pharmacist's daily questions are "what is about to go out of date", "what has already gone out
of date and is still on my shelf", and "what am I about to run out of". The data to answer all
three has existed since Module 3. This module is the reading.

Because there is no schema, §3 documents the queries instead — the exact filter rules are the
module.

Two of those rules carry the whole thing, and both are easy to get subtly wrong:

- **A null expiry date is excluded from both lists.** Never sorted to one end, never treated as
  expired, never treated as far-future.
- **Expired stock does not count towards what a product has available.** A shelf full of expired
  tablets is not stock.

---

## 2. Access control

| Action | Admin | Pharmacist | Employee |
|---|---|---|---|
| Dashboard cards | ✓ | ✓ | ✓ |
| `/alerts` and all three lists | ✓ | ✓ | ✓ |
| Anything that changes data | — | — | — |

One policy, `TenantUserPolicy`, on the whole controller. There is nothing to restrict because
there is nothing to change: every action a row invites — adjust a batch, reorder a product — lives
on a screen that already has its own permissions.

**Why an Employee sees all of it.** Operational visibility is worth more shared than withheld.
An Employee cannot clear expired stock off a shelf, but knowing not to reach for it is exactly
the sort of thing counter staff should be told — and the alternative, a colleague being asked why
the product they can see is "out of stock", is worse for everybody.

The one place the role shows is on the expired list, which offers its action buttons only to an
Admin or a Pharmacist. That is not a restriction on the data; it is not offering somebody a
button that leads to an access-denied page.

---

## 3. Query services

All four live on `IAlertQueries`, implemented by `AlertQueries`. None takes a tenant id and none
may: `Product` and `Batch` are both `ITenantEntity`, so the global query filter supplies the
pharmacy on every query — inside the aggregates too.

Every quantity is formatted through Module 2's `UnitConversion.FromBaseUnits`, so a row reads
"1 box + 2 strips" or "30 bags" according to the product's own configuration. No unit word is
written anywhere in this module.

### `GetExpiringBatchesAsync(daysAhead, page, pageSize)`

| | |
|---|---|
| **Include** | `IsActive`, `QuantityInBaseUnits > 0`, `ExpiryDate IS NOT NULL`, `ExpiryDate BETWEEN today AND today + daysAhead` |
| **Order** | `ExpiryDate` ascending, then batch number |
| **Returns** | product id, brand, generic, type, batch number, expiry date, days remaining, quantity raw and formatted, severity |

Already-expired batches are excluded. They have their own list, and a count called "expiring
soon" that included things it is too late to act on would be a worse number than no number.

`daysAhead` is coerced to one of 30 / 60 / 90 / 180 by `StockPolicy.CoerceExpiryWindow`; anything
else falls back to the configured default rather than being refused. An unbounded window is a
request for every batch in the pharmacy wearing an alert query's clothes.

### `GetExpiredBatchesAsync(page, pageSize)`

| | |
|---|---|
| **Include** | `IsActive`, `QuantityInBaseUnits > 0`, `ExpiryDate IS NOT NULL`, `ExpiryDate < today` |
| **Order** | `ExpiryDate` ascending — the oldest date is the most overdue |
| **Returns** | the same shape, with `DaysUntilExpiry` negative |

One DTO serves both lists. The expiring page reads the field as days remaining; the expired page
negates it and calls it days overdue.

### `GetLowStockProductsAsync(productType, status, page, pageSize)`

| | |
|---|---|
| **Include** | active products where sellable total ≤ `ReorderLevel` |
| **Sellable total** | sum of `QuantityInBaseUnits` over batches that are active, non-empty, and **not past their expiry date** — a null expiry counts as sellable |
| **Order** | `total / ReorderLevel` ascending, then brand name |
| **Returns** | product id, brand, generic, type, total raw and formatted, reorder level, base unit, status, and any expired quantity |

`Status` is `OutOfStock` when the sellable total is zero, `Low` otherwise.

**Ordered by ratio, not by absolute quantity.** Ten of a product that should hold twenty is a
different situation from ten of one that should hold a thousand, and the list is read top-down by
somebody deciding what to order first. A reorder level of zero is guarded rather than divided by;
such a product reaches the list only when it has nothing left, so sorting it first is also right.

**One statement, verified.** The sellable rule is composed by the shared `Fefo.Sellable` helper
and referenced from the product row, which SQL Server reads as a correlated aggregate:

```sql
SELECT COUNT(*) FROM [Products] AS [p]
WHERE [p].[TenantId] = @tenant AND [p].[IsActive] = 1
  AND COALESCE((
        SELECT TOP(1) COALESCE(SUM([b].[QuantityInBaseUnits]), 0)
        FROM [Batches] AS [b]
        WHERE [b].[TenantId] = @tenant AND [b].[IsActive] = 1
          AND [b].[QuantityInBaseUnits] > 0
          AND ([b].[ExpiryDate] IS NULL OR [b].[ExpiryDate] >= @today)
        GROUP BY [b].[ProductId] HAVING [b].[ProductId] = [p].[Id]), 0)
      <= [p].[ReorderLevel]
```

The acceptance harness seeds 100 products and 300 batches, serves the endpoint, and asserts that
exactly **two** statements touch `Products` or `Batches` — one for the count, one for the page.
An N+1 would be a hundred and something.

**A product with no batches at all needs no special case.** A sum over no rows is zero, which is
exactly the out-of-stock row the list exists to show. This was written first as a
`GroupJoin` + `DefaultIfEmpty` outer join — see §5.

### `GetAlertSummaryAsync(expiryWindowDays)`

Four counts and the nearest-expiry preview, in **four round trips**:

1. Both batch counts, in one pass with two conditional aggregates.
2. Low-stock count.
3. Out-of-stock count.
4. The nearest expiring batch, `TOP 1`.

(2) and (3) cannot share a pass — see §5.

The preview is scoped to the same window as the count on the card above it. A preview naming
something outside the counted set would read as a contradiction of the number beside it.

---

## 4. API endpoints

| Endpoint | Notes |
|---|---|
| `GET /api/alerts/summary` | The four counts and the preview. Takes no window: the cards, the badge and the pages' defaults all have to agree, and a caller who could choose one could make them disagree. |
| `GET /api/alerts/expiring?days=90&page=1&pageSize=25` | `days` accepts 30 / 60 / 90 / 180, anything else coerced to the default. |
| `GET /api/alerts/expired?page=1&pageSize=25` | |
| `GET /api/alerts/low-stock?productType=&status=&page=1&pageSize=25` | Filters: `Medicine`/`Other`, and `All`/`Low`/`OutOfStock`. |

All four are `TenantUserPolicy`, all carry `ITenantScopedRequest`, all page at 25 by default with
`AlertPaging` clamping rather than validating — a stale bookmark should land on a sensible page,
not a 400.

---

## 5. Key decisions

### Nullable expiry, and why nulls are excluded from both lists

Diapers, syringes and dressings have no expiry date. There are three things one could do with
them and only one is right:

- **Treat them as expired.** Absurd — it would fill the expired list with perfectly good stock.
- **Treat them as far-future.** Defensible, and wrong: they would sit at the bottom of the
  expiring list forever, as permanent noise that trains people to stop reading the last page.
- **Exclude them.** A product that cannot expire has nothing to say on an expiry screen.

So every expiry query carries `ExpiryDate != null` **explicitly**, even where a range comparison
would already exclude nulls by SQL's three-valued logic. That redundancy is deliberate: relying
on `NULL >= @today` evaluating to `UNKNOWN` makes the rule an accident of the predicate's current
shape, and the next person to rewrite it has nothing to tell them it mattered.

Module 3's stock list already got this right for its nearest-expiry column — `MIN` over an empty
set is `NULL`, which is exactly the em dash the column wants — and the acceptance harness checks
it still does.

### Why expired batches are excluded from the low-stock available total

This is the one that would otherwise go unnoticed for months. A product with 200 expired tablets
and nothing else has **nothing to sell**. FEFO refuses to dispense them, the billing screen
refuses to cart them — so a low-stock list that counted them would report the product as
adequately stocked, and the pharmacy would discover otherwise from a customer.

The list goes further and says so on the row: *"0 pieces, plus 200 pieces expired, which cannot
be sold."* An alert that looks wrong to somebody staring at a full shelf is an alert they learn
to argue with.

The total uses the same `Fefo.Sellable` helper that the till deducts through, which is what makes
"available" here mean the same thing as "available" at the counter.

### Where the alert window lives

**`StockPolicy.ExpiringSoonWindowDays`, and no second name for it.**

The brief asked for a constant in a clearly-named place, suggesting `AlertSettings.ExpiryWindowDays`.
Module 3 had already established `StockPolicy` for exactly this — the stock list's "expiring soon"
column reads it — and the two are the same question: how many days ahead counts as soon. Adding a
second name would have created precisely the drift the requirement exists to prevent, with the
extra hazard that the list and the alerts could disagree about the same word.

`StockPolicy` now holds all of it: the window, the two row-colour thresholds, the selectable
windows, and the two small functions over them.

**Moving to Settings later** is a change to that one class. Every consumer already takes the value
as a parameter — `GetAlertSummaryAsync(expiryWindowDays)`, `GetExpiringBatchesAsync(daysAhead)` —
so a per-tenant lookup replaces the constant at the handlers and nothing below changes. The web
app never holds a copy: the page reads the window back off the API response, and the discount-cap
precedent from Module 5 is the same pattern.

### Confirmation of the expired-sale block (Part C)

Both halves were **already correct**. No correction was needed.

`Fefo.IsSellable` reads:

```csharp
batch.IsActive && batch.QuantityInBaseUnits > 0
    && (batch.ExpiryDate == null || batch.ExpiryDate >= today)
```

— which excludes `ExpiryDate < today` and includes `ExpiryDate IS NULL`, exactly as specified.
`FefoTests` already covered all three cases before this module started
(`ExcludesExpiredBatches`, `IncludesBatchesWithNoExpiry`, `IncludesABatchExpiringToday`).

Module 5's billing block was verified live rather than by reading: the acceptance harness creates
a product whose only batch is expired with quantity 200, then

- attempts to sell it → **400**, "Only 0 pieces of 'X' available across all batches";
- looks it up on `/api/products/sellable` → status `AllStockExpired` with the Module 5 message;
- confirms it appears on `/alerts/expired`;
- confirms it is reported **out of stock**, not adequately stocked, on `/alerts/low-stock`.

The same harness confirms a batch with no expiry date is still sellable, which is the other half
of the rule and the one a careless fix would break.

### Two EF Core translation findings

Both were found by calling the endpoint, not by reading the code — worth recording, because the
code compiled cleanly in both cases and failed at runtime.

**The outer join did not translate.** `GroupJoin` + `DefaultIfEmpty` + `let` is the textbook LINQ
for "products with their totals, including products with none". EF8 returned *"The LINQ expression
could not be translated"*. The replacement composes each aggregate as its own queryable and
references it from the projection — the pattern `StockQueries.ListAsync` already uses — which EF
turns into a correlated subquery, still one statement, and which handles the missing group without
a join at all.

**SQL Server will not aggregate over a subquery.** The two product counts in the summary were
written as one pass with conditional aggregates, mirroring the batch counts. That fails with
*"Cannot perform an aggregate function on an expression containing an aggregate or a subquery"*,
because each product's total **is** a subquery. A `COUNT` with that subquery in the `WHERE`
instead of the `SELECT` is fine, which is what two separate counts produce — hence four round
trips rather than three.

### The aggregates are composed outside the lambdas

`Fefo.Sellable` is an extension that composes an expression. Called *inside* a query lambda it
would sit in the tree as a method call EF cannot translate; composed outside it is ordinary C#
building an `IQueryable`, and referencing that from a lambda is translated as a subquery. Module 3
carries the same note for the same reason, and it is the second-easiest way to break these
queries at runtime after the two above.

---

## 6. Out of scope

- **Email or SMS notifications.** In-app only.
- **A settings UI for the window.** Constants, as above.
- **Automated actions** — no auto-created purchase returns, no reorder suggestions, no purchase
  orders. The pages link to the screens; a person drives the action.
- **Historical trends** — how much stock expired last month. This module is current-state only;
  that belongs in Reports if it is ever wanted.
- **A supplier-return link on the expired page.** The branch is written and the data is there, but
  the destination screen is Module 4's and Module 4 is not built. See the frontend doc.

---

## 7. What the next modules depend on

**Module 7 — Antibiotic register** follows the same pattern established here, and that pattern is
the deliverable as much as the screens are: a query-only module needs no entity, no migration and
no feature folder full of logic. One `I…Queries` interface with the filter rules in its XML docs,
one implementation, thin CQRS wrappers, a read-only controller on `TenantUserPolicy`, and pages
that link out to where the actions already live. The register will read `Sale`'s prescription
columns the way this reads `Batch`'s expiry date.

**Module 8 — Reports** may reuse `GetAlertSummaryAsync` for a current-state panel, but not for
history: nothing here stores a snapshot, so "how much expired last month" cannot be answered from
these queries and should not be bolted onto them.

**Module 9 — Dashboard** consumes `GetAlertSummaryAsync` as it stands. The four cards and the
sidebar badge are already built on it.

---

## 8. Verified

- 22 unit tests over the severity thresholds and the window coercion.
- 62 acceptance checks against a live API: every criterion in the brief, including the 100-product
  SQL-statement count, the expired-stock sale block, and null-expiry exclusion from both lists.
- 46 page checks over the dashboard, the hub and the three lists, including the nav badge, the
  role-dependent actions and the empty states.
