# 07 — Antibiotic Register

A reporting view over antibiotic sales, plus one per-pharmacy setting that changes how Module 5
behaves. It introduces no new sale logic, and it is the first module that modifies an earlier
one — see §3.

---

## 1. Overview: the gap this module is built around

Antibiotics legally require a prescription in Bangladesh, and pharmacies are expected to maintain
a sales register. That is the law, enforcement has been tightening, and Model Pharmacies apply it
strictly.

In practice, most retail pharmacies sell antibiotics over the counter without recording a
prescription.

That gap is the design problem, and it has two obvious solutions that are both worse than the one
built here:

**Hard-block the sale.** A system that refuses to complete an antibiotic sale without a
prescription does not produce compliance. It produces one of two things: invented patient names —
which is worse than capturing nothing, because it looks like a record and is not — or staff
working around the till entirely. The second is the one that ruins everything else: stock stops
being deducted, FEFO stops being true, the low-stock alerts start lying, and the pharmacy loses
inventory accuracy across all six previous modules to enforce one rule in this one.

**Drop the feature.** Then a pharmacy that does want to comply, or has to, cannot — and the
compliant path is the one with a legal requirement behind it.

### The resolution

Prescription capture becomes a **per-pharmacy setting with three modes, defaulting to Off**. Each
pharmacy picks the level that matches how it actually operates.

**But antibiotic sales are always recorded in the register, on every mode.** The tracking is never
optional; only the prescription details are. A pharmacy running on the loosest setting still has a
complete record of what antibiotics went out of the door and when — which is most of what an
inspector asks to see, and far more than the same pharmacy would have if the system had tried to
force the rest and been bypassed.

### One structural point

A single sale can contain more than one antibiotic. **The register shows one row per antibiotic
line item, not one row per sale**, so each product's quantity is individually attributable. Two
antibiotic lines on one invoice produce two register rows sharing an invoice number.

---

## 2. The mode setting

`Tenant.AntibioticPrescriptionMode`, an enum of `Off` / `Optional` / `Required`, defaulting to
`Off`.

| | `Off` (default) | `Optional` | `Required` |
|---|---|---|---|
| Prescription panel shown at billing | No | Yes, all fields optional | Yes, all fields required |
| Employee can sell antibiotics | **Yes** | **Yes** | No |
| Sale blocked without a prescription | No | No | Yes |
| Antibiotic sale recorded in the register | **Yes** | **Yes** | **Yes** |
| Missing prescription flagged as a problem | No | No | Yes |

### Where it is stored, and where it should live

On the `Tenant` row, added by [migration 012](../database/scripts/012_AddAntibioticPrescriptionMode.sql),
**with a TODO on both the entity and the migration** saying it belongs in tenant settings once a
Settings module exists. It is the first per-pharmacy *preference* the system has had — every other
column on `Tenants` identifies or governs the tenant rather than configuring it.

What it could not be is a constant. Two pharmacies on one deployment genuinely operate
differently: a Model Pharmacy applies the rule and the shop down the road does not. A global
switch would force one of them either to loosen or to fabricate patient data.

### How Module 5 consumes it

Through `ITenantSettings`, which is **scoped — and its lifetime is its cache**.

A sale asks for the mode once per cart item, several times per request. Without a cache a
ten-item cart would be ten identical queries about a value that cannot change between them. With
a longer-lived cache an Admin tightening the rule before an inspection would be left wondering
whether it had taken effect. Reading once per request gives both: one query per sale, and the new
mode in force on the very next request. No redeploy, no staleness beyond the request that was
already in flight.

`ITenantSettings` also returns the pharmacy's name, because the CSV export's header block needs
it and the service already reads that row. It is deliberately not on `ICurrentTenantService`,
which carries only the id: that interface decides which rows exist, and adding display data to it
would invite reading tenant data from the isolation mechanism.

---

## 3. Cross-module change: Module 5 is now conditional

**This module changed Module 5's behaviour.** Two rules that were unconditional are now read from
the pharmacy's mode on every sale, and the server never assumes:

| Rule | Before | Now |
|---|---|---|
| Employee antibiotic block | Always | `BillingPolicy.MaySellAntibiotics` — only under `Required` |
| Prescription field validation | Always required | Validated only under `Required`; stored as-is under `Optional`; not sent under `Off` |
| Panel rendering | Always when an antibiotic is in the cart | Absent under `Off`; optional fields under `Optional`; required under `Required` |

The rule itself lives in one place, `BillingPolicy.MaySellAntibiotics(role, mode)`, read by the
completion handler, the sellable-product search and the billing-limits endpoint. Three inline
copies of a rule with legal consequences would be three chances to get it wrong differently.

`Sale.SetPrescription` was loosened to accept nulls as part of this. Completeness depends on a
per-tenant setting, and a `Sale` cannot know which mode its pharmacy is on — so the entity records
and the handler judges. Nothing is lost: the guard it carried only ever fired on a path the
handler had already closed.

[`docs/05-billing-and-invoice.md`](05-billing-and-invoice.md) §8 has been rewritten to match, and
its access table now points here.

---

## 4. Access control

| Action | Admin | Pharmacist | Employee |
|---|---|---|---|
| Sell an antibiotic | ✓ | ✓ | **mode-dependent** — yes under Off and Optional |
| View the register | ✓ | ✓ | ✗ (403) |
| Export the register | ✓ | ✓ | ✗ (403) |
| Read the mode | ✓ | ✓ | ✓ |
| Change the mode | ✓ | ✗ (403) | ✗ (403) |

### The deliberate asymmetry

**An Employee may be able to sell an antibiotic but can never view the register.** That is not an
oversight and it is worth stating plainly, because it reads like one.

Selling is counter work. The register is the regulatory record of it — what colleagues dispensed,
to which named patients, against whose prescription — and reading that back is oversight rather
than counter work. The two questions have different answers, and the policies say so:
`TenantUserPolicy` on the till, `TenantWriterPolicy` on the register, `TenantAdminPolicy` on the
setting. Both halves are declared on the platform access page so the asymmetry appears in an
access review rather than only in this document.

**Reading the mode is open to everyone** because the till needs it on every load to decide whether
to draw the prescription panel. An Employee reads it several times a day without knowing they do.

---

## 5. Query service

`IAntibioticQueries`, implemented by `AntibioticQueries`. No entity of its own: every row is
assembled from `Sale`, `SaleLine`, `Product`, `SalesReturn` and `Users` — the lightweight
query-only pattern Module 6 established.

Tenant scoping is automatic. Every entity read except `Users` is an `ITenantEntity`; `Users` is
the global identity table, so reaching into it is an explicit join written out rather than buried
in a navigation.

### `GetRegisterAsync(from, to, productId, doctorName, cashierUserId, prescriptionStatus, page, pageSize)`

A row is included when **all** of:

- `Product.IsAntibiotic` — the pharmacist's confirmed flag, not the catalogue's machine-derived
  guess;
- `Sale.Status == Completed`;
- `Sale.SaleDate` within the range, **inclusive of the whole end day** (a range ending 31 August
  that stopped at midnight would omit every sale made on the 31st).

Sorted by sale date descending, then invoice number, then brand name. The tiebreakers are not
decoration: two antibiotics on one invoice share a timestamp exactly, and without them the order
between those rows is whatever the query plan produced — stable enough to pass a test once and to
shuffle rows between pages in production.

Dates default to the current month (`RegisterRange.Resolve`), which is what a register is read
for. A reversed range is swapped rather than refused: somebody who types the dates the wrong way
round wants to see the rows.

**Filters.** `doctorName` is a partial match, because a register is searched by half-remembered
names — "Karim" finds "Dr A. Karim Chowdhury". `prescriptionStatus` keys off whether **any**
detail was captured, not all of them; under Optional a doctor's name alone is a real record, and a
filter demanding the full set would report a pharmacy as capturing nothing when it is capturing
something.

### `GetRegisterSummaryAsync(...)` — the same filters, no paging

Row count and net quantity across the **whole filtered set**. Separate from the page on purpose:
a total that changed as somebody paged would be worse than no total, and on a document that may be
handed to an inspector a header contradicting the table beneath it is the worst available outcome.

### `StreamRegisterAsync(...)` — the export

`IAsyncEnumerable`, over EF's `AsAsyncEnumerable`. A busy pharmacy accumulates thousands of
antibiotic lines a year and an export is precisely the request that asks for all of them;
buffering a year into a list to write it straight out again would hold the lot in memory for no
reason. The controller writes rows to the response body as the reader produces them, and the web
app passes them through with `HttpCompletionOption.ResponseHeadersRead` so neither tier undoes it.

### `GetMonthlySummaryAsync(month, year)` — the dashboard card

Total antibiotic quantity dispensed in the month, **net of returns, excluding cancelled sales**.

### A note on summing base units

The register and the dashboard both sum `QuantityInBaseUnits` across products, and that is only
meaningful when those base units are the same thing. Twelve tablets plus three bottles is not
fifteen of anything.

So the unit label is computed: when every row in the filtered set shares one base unit the total
is labelled with it ("1,240 pieces"), and when the set mixes them the label is "units". Borrowing
whichever name came first would make the figure look more precise than it is.

### Query shapes worth knowing about

Two aggregates had to be split into separate statements because SQL Server refuses to aggregate
over an aggregate — *"Cannot perform an aggregate function on an expression containing an aggregate
or a subquery"*. `SUM` over a per-line `SUM` of returns is exactly that shape; `SelectMany` over
the `Returns` navigation flattens to a join first, so the outer `SUM` is over plain columns.
Module 6 hit the same rule from the other direction.

---

## 6. API endpoints

| Endpoint | Access | Notes |
|---|---|---|
| `GET /api/antibiotics/register` | Admin, Pharmacist | 25/page. Params: `dateFrom`, `dateTo`, `productId`, `doctorName`, `cashierUserId`, `prescriptionStatus`, `page`, `pageSize`. Returns the page, the range summary **and** the filter dropdowns in one response, because all three change together. |
| `GET /api/antibiotics/register/export` | Admin, Pharmacist | Streamed CSV of the **whole filtered set**. Header block: pharmacy, range, mode, export timestamp. |
| `GET /api/antibiotics/summary?month=&year=` | Admin, Pharmacist | Monthly total, net of returns, excluding cancelled. Month and year default to the current one. |
| `GET /api/settings/antibiotic-mode` | Any tenant user | Billing needs it. |
| `PUT /api/settings/antibiotic-mode` | **Admin only** | Takes effect on the next request. |

The billing screen gets the mode from `GET /api/sales/limits` alongside the discount caps, rather
than making a second call on the busiest screen in the product. Both read the same
`ITenantSettings`, so they cannot disagree.

### CSV escaping

RFC 4180: any field containing a comma, a quote or a newline is quoted, and embedded quotes are
doubled. It matters more here than on most exports — a patient's name is free text typed at a
counter, and `Rahman, Md. Abdul` unquoted would shift every later column on that row alone.

---

## 7. Key decisions

### Why antibiotic sales are always tracked, whatever the mode

Because the register is the part of the regulation a pharmacy can actually keep, and it is the
part an inspector is most likely to ask for. Making it conditional on the mode would mean a
pharmacy on Off had no antibiotic record at all — which is the outcome both this module and the
law are trying to avoid, and the one the pharmacy would be in genuine trouble for.

The setting governs how much *prescription detail* is captured. It has never governed whether the
sale is recorded.

### Why missing prescriptions are flagged red only under Required

Under `Off`, nothing was captured by design. Under `Optional`, it was captured when somebody had
it — which is exactly what that mode is for. A red badge on those rows would be crying wolf on
every row in the table, and the first thing anybody learns from an indicator that is always lit is
to stop reading it. By the time it meant something, it would have been ignored for months.

Under `Required` a missing or unverified prescription means Module 5's enforcement let something
through, which is worth shouting about.

**One consequence to be clear about, because it looks like a bug and is not.** A pharmacy that
switches to `Required` will see its older `Off` and `Optional` rows light up red. Those sales were
recorded correctly under the rules in force when they happened. The register says so beneath the
table, in as many words, and nothing recomputes history to match a setting changed afterwards — a
register that rewrote itself when a policy changed would be the opposite of a record.

### Why cancelled sales are excluded and returns are shown as indicators

A **cancelled** sale dispensed nothing: its stock went back and its payment was reversed. It does
not belong in a dispensing register, and Module 8 will exclude them the same way. **If the two
ever disagree the totals stop being worth reading**, which is the reason to state the rule in both
documents.

A **partially returned** sale did happen. Removing the row would be rewriting the record rather
than correcting it, so the row stays with a "Returned: 6 pieces" sub-line, and the totals are net.
The distinction is between an event that was undone in full and one that occurred and was
partially reversed.

### The quota question, and why nothing is built for it

**Out of scope, deliberately and on an unresolved factual question.**

It is unconfirmed whether Bangladeshi regulation sets a specific numeric monthly antibiotic limit
per pharmacy, as opposed to requiring prescription tracking and register maintenance — which are
the requirements that are definitely real. The difference matters: a quota system would warn
pharmacies about a threshold that may not exist, and staff who learn that a warning is meaningless
learn to dismiss every warning the system gives them.

**Do not build one until the number is verified** against the actual regulation or with a pharmacy
owner who is subject to it. The monthly summary this module does provide is the input anybody
would need to check a quota by hand if one turns out to exist.

---

## 8. Out of scope

- **A configurable government antibiotic quota with automated warnings** — see above.
- **Prescription image or photo upload.** The verification checkbox is what actually happens at a
  counter.
- **Automatic flagging of suspicious patterns** — the same patient buying repeatedly, unusual
  volumes. That is inference about people from thin data, and a false accusation is worse than no
  analysis.
- **PDF export.** CSV satisfies the requirement, and a PDF would mean a new dependency.
- **Cross-tenant or platform-level antibiotic analytics.** A platform operator has no business in
  one pharmacy's dispensing record.
- **A general settings screen.** One setting, one small page. The broader Settings module will
  absorb it — see §2.

---

## 9. What the next modules depend on

**Module 8 — Reports.** Must exclude cancelled sales exactly as this module does. The two will be
read side by side and a discrepancy would make both untrustworthy. The monthly antibiotic figure
here is deliberately not a report: it is current-state only, with no history stored.

**A Settings module.** `Tenant.AntibioticPrescriptionMode` is the column to move, and
`ITenantSettings` is the seam to move it behind — every consumer already goes through that
interface, so the change is to its implementation and nothing else.

---

## 10. Verified

- 26 unit tests, including the full 3 × 3 role × mode grid for who may dispense, the date-range
  resolver, and what a sale records when the mode does not demand everything.
- 86 acceptance checks against a live API: all three modes end to end, mode switching taking
  effect on the next sale without a redeploy, two pharmacies holding different modes
  independently, multi-antibiotic sales producing one row each, cancellation removing a row and
  its quantity, a partial return keeping the row and reducing the total, every filter, the
  streamed CSV with its header block and its RFC 4180 escaping, the Employee 403s, and tenant
  isolation.
- 54 page checks: the register in all three modes, the flag appearing only under Required with its
  explanatory note, the settings page wording, the mode-dependent billing panel, the export
  handler's filename and content type, and the print-only header.
