# 08 — Reports & Profit

Eight aggregations over data five earlier modules already store. No new entity, no new table, no
write path. Which makes this the easiest module in the system to get *subtly* wrong, and the
hardest to notice when it happens.

---

## 1. The problem this module actually has

Every other module fails loudly. A sale that cannot deduct stock returns a 400. A batch with a
bad expiry is refused. A page that cannot load its data shows an error.

A report does none of that. It returns a number. The number looks like every other number on the
screen, it is internally consistent, it totals correctly, and it is wrong — and a pharmacy owner
prices their shelves against it, decides what to reorder against it, and works out what they can
afford to pay their staff against it.

So the whole of this module's design is about three rules that each fail silently, and about
making them impossible to apply inconsistently across eight reports.

### 1.1 Revenue is `NetLineTotal`, never `LineTotal`

Module 5 spreads a bill-level discount proportionally across the lines of a sale and stores each
line's share. `LineTotal` is what the line came to before that; `NetLineTotal` is after.

Reading `LineTotal` reports inflated margin on every discounted sale, and the more generous the
discount the more inflated it is — so the sales a pharmacy is least sure about are the ones it
would be most misled on.

### 1.2 Cost comes from the batch, never from the product

Each batch was bought at its own price. That is the entire reason batches exist: the same
medicine delivered twice, three months apart, cost two different amounts, and a sale out of the
first delivery earned a different margin than a sale out of the second.

A product's current purchase price is what the *next* delivery cost. It has nothing to do with
what the units on last month's invoice cost. Every sale line carries `BatchId`, and every cost
figure in this module reads through it.

The acceptance harness has a fixture built specifically to catch a violation of this: a sale of
80 units that empties a batch bought at 4.00 and crosses into one bought at 6.00. The correct cost
is 340.00 — 70 at 4.00 plus 10 at 6.00 — and no formula reading a single per-product price can
produce that number.

### 1.3 A cancelled sale is excluded, not zeroed

A cancelled sale restored its stock and reversed its payment. It never happened.

Zeroing its totals would leave it counted in every denominator: transactions per hour, average
basket size, sales per cashier, margin per sale. Each of those would be quietly wrong, and the
error grows with the cancellation rate — so a pharmacy having a bad day would also get worse
numbers about it.

This one is a filter rather than arithmetic, so it lives in the query rather than in `ProfitMath`.
`ReportQueries.CompletedSales` and `ReportQueries.SoldLines` both apply it, and nothing in the
file reads a sale any other way.

---

## 2. Where the arithmetic lives

`PMS.Domain.Billing.ProfitMath` — one static class, seven methods, no dependencies.

It exists so that "profit" means exactly one thing across eight reports. The SQL in
`ReportQueries` mirrors these expressions rather than calling them (EF cannot translate a method
call into a `SUM`), and the two are kept in step by `ProfitMathTests` plus the end-to-end
assertions in `acceptance_reports.py`.

| Method | What it is for |
|---|---|
| `LineCost` | quantity × the **batch's** per-base-unit price |
| `LineProfit` | `NetLineTotal` − `LineCost` |
| `ReturnImpact` | refund − the cost of the stock that came back |
| `NetProfit` | gross profit − operating expenses |
| `MarginPercent` | profit ÷ revenue × 100, zero revenue giving zero |
| `WeightedAverageCost` | total value ÷ total quantity |

### 2.1 Nothing rounds

Not in `ProfitMath`, not in the queries. SQL aggregates over unrounded decimals, and rounding
happens exactly twice: `Money.Format` on the way to a screen, and `CsvField.Money` on the way to
a file.

Rounding per line and then summing drifts by up to a paisa a line. Over a month that is a
discrepancy somebody will try to reconcile against a till and cannot — and the reconciliation
will cost more than the paisa.

`ProfitMathTests.Summing_unrounded_lines_beats_summing_rounded_ones` pins the difference with
three lines that each land on a half-paisa: rounded first they sum to 1.53, summed first to 1.52.
The second is right.

### 2.2 Weighted average cost

Weighted by *quantity*, not an average of the batch prices.

Ten units at 1 and one unit at 100 average 1.09 a unit, not 50.50. The stock valuation's headline
total depends on getting that right, because it is the figure an owner reads as "money tied up in
inventory".

The acceptance harness leaves Alpha holding 5 units at 4.00 and 90 at 6.00 — 560.00 over 95 units,
so 5.8947 a unit. A plain average of the two prices would say 5.00 and value the shelf at 475.00.

---

## 3. Returns

A return reverses **both sides**: the money refunded, and the cost of the goods that came back
onto the shelf. Subtracting only the refund would treat returned stock as though it had
evaporated, and the pharmacy would appear to lose its full sale price on every return rather than
its margin.

**Returns are attributed to the period they happened in**, keyed on `SalesReturn.CreatedOnUtc` —
not to the period of the original sale. A sale in August returned in September reduces September.

That is when the cash actually left, and it is what an owner reconciling a month against their
till expects. The alternative — restating a closed month because of something that happened in
the next one — means last month's report changes after it has been read, printed and filed.

On the daily report the return impact is shown as its own line under the table rather than folded
into the per-sale profit column. The returns processed today may reverse sales from other days, so
they belong to the day's profit but to no row in the table — and a total that did not reconcile
with the column above it would read as an arithmetic error.

---

## 4. The reports

| Report | Shape | Notes |
|---|---|---|
| Daily sales | One day, every sale, hourly buckets | Defaults to today |
| Monthly sales & profit | One month, day by day | The only report with net profit |
| Top selling products | Paginated, sortable, streamed export | Net of returns on all three measures |
| Sales by product type | One row per type | Unpaginated — bounded by the enum |
| Dead stock | Paginated, streamed export | See §5 |
| Sales per user | One row per cashier | Unpaginated — bounded by headcount |
| Stock valuation | Paginated, streamed export | Snapshot, no date range |
| Supplier dues | One row per supplier | Added by Module 4 — see §4.1 |

### 4.1 Supplier dues

**Shipped disabled with this module, completed by Module 4.**

When Module 8 was built there was no `Supplier`, no `Purchase` and no `SupplierPayment`, so the
report could not exist. It was deliberately left as a **visibly disabled card** on the landing
page rather than a page returning zeros: a money report confidently saying "0.00 owed" is worse
than an absent one, because somebody would pay a supplier on the strength of it. A gap a person
notices and asks about beats one they never learn exists.

Module 4 filled it in. `GET /api/reports/supplier-dues` and `/reports/supplier-dues` now work, and
the landing card is an ordinary link.

**The outstanding figure is read from `ISupplierBalanceQueries`** — the same service a supplier's
own page calls — and is not reimplemented here. `acceptance_purchases.py` asserts that every
supplier's balance on the report matches their detail page exactly; a discrepancy would mean the
formula had been duplicated.

One difference from every other report in this module: **it defaults to all time.** "Who do we owe"
is a question about now, not about a window. A date range narrows the purchased, paid and returned
columns while the outstanding column continues to cover everything, and the page says so in a
banner when a range is applied. See `docs/04-suppliers-and-purchase.md` §9.1.

### 4.2 Dates are bounded in UTC

Consistently with every other date filter in the system — the sales list, the antibiotic register,
the alert windows. A second convention in one report would mean two screens disagreeing about
which day a 2 a.m. sale belongs to, which is a worse problem than the one it would solve.

The daily report's hourly buckets are therefore keyed by **UTC hour**, and the *page* shifts the
labels into the display zone when it draws them — the same division of labour every timestamp in
the application already uses. A properly local day boundary needs the per-tenant time zone that
Module 1 already notes as deferred, and it should land everywhere at once rather than here alone.

### 4.3 Empty buckets are rendered

The daily report returns all 24 hours and the monthly report returns every day of the month,
including the ones with nothing in them.

A chart with gaps where a shop was simply quiet reads as missing data rather than as a quiet
period, and a trend line that skipped quiet days would compress its own x-axis and misrepresent
the shape of the month.

---

## 5. Dead stock: the one query that had to be an outer join

**This is the single most likely bug in the module, and it produces a report that looks entirely
plausible while omitting its own headline cases.**

Dead stock means "has stock, has not sold recently". The most important rows are the products
that have **never** sold at all — bought once, shelved, and untouched since. A product with no
sale lines has nothing to join to, so an inner join between products and sales silently drops
exactly those rows.

The result is a report that runs, returns rows, totals correctly, and is missing the deadest
stock in the pharmacy.

### 5.1 How it is written, and why not the obvious way

Two earlier findings shaped this:

- **Module 6 proved `GroupJoin` + `DefaultIfEmpty` does not translate** in this EF/SQL Server
  combination. The idiomatic LINQ left join is not available.
- **SQL Server refuses "an aggregate function on an expression containing an aggregate or a
  subquery"**, which Modules 6 and 7 each hit once.

So the query is split in two:

`DeadStockProductIds` decides membership and contains **no aggregate at all** — it uses `Any`,
never `Sum` or `Max`. The threshold test is expressed as *"has not sold since the cutoff"* rather
than *"the last sale was before the cutoff"*. The two describe the same set, but only the first
covers never-sold products without a `Max` over an outer join.

`DeadStockRows` then projects quantity, value and last-sold date as correlated subqueries.
`FirstOrDefault()` over a `DateTime?` projection yields null when there is no sale — **that is the
outer join**, written in a form EF reliably translates.

Counts and totals aggregate the lean id query or the batch table directly, never the row
projection.

### 5.2 It was found by running it, not by reading it

The first version aggregated over the row projection and compiled cleanly. The streaming CSV
export worked. The paginated endpoint returned a 500 with *"Cannot perform an aggregate function
on an expression containing an aggregate or a subquery"* — five times over, once per aggregate.

That is now three modules in a row where an EF translation limit was found by calling the
endpoint and would not have been found by inspection. Worth recording as a pattern: **probe every
new query shape against a running database before building any UI on top of it.**

### 5.3 Expired stock is included here

It has not sold, and it no longer can. That makes it the deadest stock on the shelf rather than an
exception to the report. The page says so and links to the expired-stock list, which is where
somebody can actually act on it.

---

## 6. Stock valuation: expired stock is not an asset

Expired stock is **counted separately and excluded from the value**.

It cannot be sold — FEFO refuses it and the till refuses it — so counting it at cost would
overstate the single figure an owner reads as "money tied up in inventory". It is a write-off
waiting to happen, and the report names it as one.

The sellable rule is `Fefo.Sellable`, composed outside the projection lambdas rather than restated
inline, so the valuation and the till agree by construction about what can be sold.

There is **no date range**, because there is no history of stock levels to look back through. "As
at last month" is a question this system honestly cannot answer, and a date picker that silently
ignored the dates would be worse than not offering one.

---

## 7. Operating expenses — the seam, now filled

`IOperatingExpenses.GetOperatingExpensesAsync(from, to)` is what turns gross profit into net.

**Module 8 shipped it returning zero**, with the monthly report saying so on screen, because the
Salary module did not exist yet. It was a real interface with a real registered implementation,
and the monthly report *called it* rather than assuming zero.

**Module 9 replaced that one class.** No report changed. No report forgot to include the new
figure. Nothing had to be hunted for — which is the whole argument for having declared the
interface a module early, and worth remembering the next time a module needs something the one
after it will provide.

### 7.1 What it returns

Two sums, added together, and **both are required**:

```
GetOperatingExpenses(from, to) =
      SUM(SalaryEntry.NetPayable  WHERE PaymentStatus = Paid
                                    AND PaymentDate  BETWEEN from AND to)
    + SUM(SalaryAdvance.Amount     WHERE AdvanceDate  BETWEEN from AND to)
```

`NetPayable` already has the advance subtracted out of it, so summing paid entries alone would
lose every advance ever given. Both are dated by **when the money moved** — the day a salary was
paid, and the day an advance was handed over — matching how §3 attributes a return to the day the
goods came back rather than the day of the original sale.

The full reasoning, the worked example and the cash-basis consequence an owner will ask about are
in [09-salary-management.md](09-salary-management.md) §4.

### 7.2 The note beside net profit

An owner reading "net profit" is entitled to assume salaries are in it. A silent zero would
overstate profit by the largest recurring expense a pharmacy has.

Module 8 rendered a warning saying salaries were not yet recorded anywhere. **That note is gone.**
What replaced it says what is actually true of a month with no expenses in it: nothing was paid
out, and the likeliest reason is a payroll generated but not yet marked paid.

It is still driven by `OperatingExpenses == 0` rather than hard-coded — the contract property is
now `NoExpensesRecorded` — so it appears only on a month that genuinely had no staff cost. The CSV
export carries the same note, and now only when it applies: a file that has been emailed on is
read without the screen it came from, and a caveat printed unconditionally on a month with real
payroll in it would be worse than none.

---

## 8. Access: the strictest area in the application

**`TenantAdminPolicy` on every endpoint and every page. No exceptions, no read-only variant.**

Other modules separate reading from writing. Here the reading *is* the sensitive act:

- Every report joins to batch cost, so every report exposes what stock was bought for.
- The margin figures are the business's profitability.
- Sales-per-user is, in effect, a staff performance review.

A Pharmacist who may dispense a controlled drug and adjust stock still has no business knowing the
owner's margin on it. A cashier has no business reading a table ranking their colleagues'
discounting.

The nav entry is Admin-only too, but that is a convenience — the policy is what stops anybody.
`acceptance_reports.py` asserts a 403 for both other roles on four representative endpoints, and
`web_smoke_reports.py` asserts a redirect to `/denied` for all eight pages.

### 8.1 Sales per user is written to be read fairly

The page says in as many words that a high discount figure is a question rather than a finding.
Counters differ: one shift serves the regulars, another the walk-ins, and a manager may have
authorised goodwill that only one person was on hand to apply.

The average-discount column is a share of what each person rang up **before** discount, which is
what makes it comparable between people at all. Dividing by net sales instead would understate
every cashier's percentage, and by more the more they discounted.

A cashier whose account has since been removed still appears, as "(unknown user)". Hiding the row
would hide their sales from the total.

---

## 9. Exports

Every report exports to CSV. The paginated ones export **the whole filtered set, not one page** —
an export that quietly stopped at row 25 would be worse than no export.

Those three are **streamed**: `IAsyncEnumerable` out of EF, straight onto `Response.Body`, with
`HttpCompletionOption.ResponseHeadersRead` in the web client so neither tier buffers the body. A
year of a pharmacy's catalogue is precisely the request that asks for everything at once.

The unpaginated reports are bounded by their own shape — a day has one day's sales, a month has 31
rows, product types are an enum — so they export from the same method the page uses.

Every file opens with a header block naming the pharmacy, the period and when it was taken, so a
CSV that has been emailed on is still self-describing.

`CsvField` handles escaping (RFC 4180) and formats money with two decimals, invariant culture, no
group separators and no currency symbol. No separators because a thousands comma inside an
unquoted field shifts every column after it by one; no symbol because a spreadsheet will not sum a
column it cannot read as a number, and summing it is why somebody exported it.

It was lifted out of `AntibioticsController` when the reports became its second caller.

---

## 10. What was found by running it

Recorded because both would have shipped otherwise.

**The aggregate-over-aggregate failure (§5.2).** Compiled cleanly, streamed cleanly, 500'd on the
paginated path. The fix was structural, not a tweak.

**The valuation fixture.** The acceptance harness asserted Alpha would be worth 540.00 — 90 units
in the dear batch, the cheap one sold out. It came back 560.00. The harness was wrong, not the
code: the return in the same run had put 5 units back into the *cheap* batch after the sale
emptied it. The assertion is now 560.00 over 95 units at a weighted 5.8947, which is a better test
than the one it replaced, because a mixture is exactly what weighted average cost is for.

---

## 11. Files

**Domain** — `Billing/ProfitMath.cs`

**Application** — `Common/DTOs/ReportDtos.cs`, `Common/Reports/ReportRange.cs`,
`Common/Stock/StockPolicy.cs` (dead-stock thresholds added), `Interfaces/IReportQueries.cs`,
`Interfaces/IOperatingExpenses.cs`, `Features/Reports/Queries/**`

**Persistence** — `Services/ReportQueries.cs`, `Services/OperatingExpenses.cs`
(the latter implemented in Module 9; see §7)

**API** — `Controllers/ReportsController.cs`, `Csv/CsvField.cs`,
`Access/AccessMatrixBuilder.cs` (area name and order)

**Tests** — `tests/PMS.UnitTests/Domain/Billing/ProfitMathTests.cs`

**Harnesses** — `acceptance_reports.py` (123 assertions), `web_smoke_reports.py` (137)

Frontend: see `docs/frontend/08-reports-and-profit.md`.
