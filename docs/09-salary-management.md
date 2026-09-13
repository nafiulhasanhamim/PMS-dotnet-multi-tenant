# Module 9 — Salary Management

Who is on the payroll, what they are paid, what they have already been advanced, and — the part
that reaches outside this module — what all of that costs the business in a given month.

This is the module that completes `IOperatingExpenses` and makes net profit on the monthly report
mean what it says. See [08-reports-and-profit.md](08-reports-and-profit.md) §7.

---

## 1. Overview

Small Bangladeshi pharmacies pay staff monthly, and cash advances mid-month are routine: an
employee needs money for an emergency, the owner hands them ৳2,000 out of the register, and it
comes out of their next salary.

That single habit is what the module is shaped around, and it creates two failure modes that are
easy to build and hard to notice afterwards:

1. **An advance that is only a number typed in at month-end depends on somebody remembering it.**
   When they do not, the employee is paid in full on top of money they have already had, and
   nothing in the system ever says so.

2. **An expense figure that naively sums `NetPayable` loses every advance ever given.** That
   figure already has the advance subtracted out of it, so the cash that left the register in July
   appears in no month at all, and profit is overstated by exactly that amount.

Both are solved the same way: **record an advance when it is given, and count it as an expense on
the date it was given.** §4 is the part most worth reading.

### What the module does

| Screen | What it is for |
| --- | --- |
| Salary profiles | Who draws a salary, and how much |
| Record an advance | Cash handed over mid-month, captured when it happens |
| Advances | What has been handed over, and what is still outstanding |
| Generate salary | A month's payroll in one pass, advances already deducted |
| Salary history | Every month generated, what was paid and when |
| Salary slip | A printable breakdown per employee per month |

---

## 2. Access control: Admin only, with no read-only variant

**Every endpoint under `/api/salary/*` and every page under `/salary` requires
`TenantAdminPolicy`.** A Pharmacist and an Employee get 403 from the API and no nav entry at all.

This is stricter than most of the system, and stricter in a different way from Module 8's Reports.
Reports withholds *the owner's* margins. This withholds *colleagues'* pay: a counter assistant who
can open the payroll can see what the person standing beside them earns. There is no version of
"read-only access to salary" that is safe in a five-person pharmacy.

The policy is declared **once on the controller**, not per action, so a new endpoint cannot be
added without it. That is exactly how a module-wide rule leaks.

The access matrix at `/platform/access` derives this from the running build, so nothing here needs
hand-maintaining — see [01--users-and-authentication.md](01--users-and-authentication.md).

---

## 3. Schema

Three tables, created by `database/scripts/015_CreateSalaryTables.sql` in this order because the
dependency runs in a small circle: an advance points at the salary entry that settled it, and an
entry points at the profile it was generated for.

### 3.1 `EmployeeSalaryProfiles`

| Column | Notes |
| --- | --- |
| `Id`, `TenantId` | |
| `UserId` | FK → `Users`. One **active** profile per user per pharmacy |
| `Designation` | Free text — "Counter staff", "Pharmacist" |
| `MonthlyBaseSalary` | `DECIMAL(18,2)` |
| `JoiningDate` | `DATE` |
| `IsActive` | Soft delete |

**Why this is not extra columns on `User`.** Not every user draws a salary — the owner running the
system as Admin frequently does not — so these would be nullable on most rows. And a user's
identity is read on *every single request*; their pay is read by one Admin a few times a month.
Keeping HR and financial data out of the authentication table means a bug in one cannot expose the
other.

**Deactivation is a soft delete and must stay one.** Every salary entry and every advance ever
recorded points at this row. Deleting it would either orphan a year of payslips or cascade them
away, and those are the record of money that left the till. Someone who leaves is deactivated:
they disappear from the generation screen and the advance dropdown, and every month they were paid
for stays exactly where it was.

The unique index is **filtered** — `WHERE IsActive = 1` — so a rejoiner gets a second profile while
the first keeps its history. An unfiltered index would make rejoining impossible without editing
the old row, which is the one thing this table must never do.

### 3.2 `SalaryEntries`

| Column | Notes |
| --- | --- |
| `Id`, `TenantId` | |
| `EmployeeSalaryProfileId` | FK |
| `Month`, `Year` | Unique per profile — `UX_SalaryEntries_Tenant_Profile_Period` |
| `BaseSalary` | **Copied** from the profile at generation |
| `Bonus`, `AdvanceDeduction`, `OtherDeduction` | `DECIMAL(18,2)`, default 0 |
| `AdjustmentNotes` | Required whenever bonus or other-deduction is non-zero |
| `NetPayable` | Computed and **stored**, floored at 0 |
| `PaymentStatus` | `Unpaid = 0`, `Paid = 1` |
| `PaymentDate` | `DATE`, null until paid. **This is what the expense report keys on** |
| `GeneratedByUserId` | FK → `Users` |

**Why `BaseSalary` is copied rather than read live.** A raise in October must not restate what
somebody was paid in July. Deriving the figure on read would silently rewrite every historical
payslip the moment a base salary changed — and a payslip that changes after it was handed over is
not a record of anything. `PurchaseLines` duplicates a batch's cost for the identical reason; see
[04-suppliers-and-purchase.md](04-suppliers-and-purchase.md).

`NetPayable` is stored for the same reason, and its CHECK is `>= 0` rather than an equality against
the four components: **the arithmetic is floored**, and a floor is not an equation. When advances
exceed what a month can pay, net payable is 0 while the components sum below it.

The unique index on `(TenantId, EmployeeSalaryProfileId, Year, Month)` is what makes pressing
"generate for August" twice safe — the second press is refused by the database, not merely by a
handler that happened to check first.

### 3.3 `SalaryAdvances`

| Column | Notes |
| --- | --- |
| `Id`, `TenantId` | |
| `EmployeeSalaryProfileId` | FK |
| `Amount` | `DECIMAL(18,2)`, must be > 0 |
| `AdvanceDate` | `DATE`. **The day the expense falls on** |
| `Reason` | Optional, and worth asking for |
| `GivenByUserId` | FK → `Users` |
| `IsSettled` | |
| `SettledInSalaryEntryId` | FK → `SalaryEntries`, null while outstanding |

**A row is a tranche, and a tranche is settled whole or not at all.** Usually a row is exactly one
handover. When a month can only recover part of one — ৳15,000 of a ৳16,000 advance against a
৳15,000 salary — the row splits: it shrinks to the ৳1,000 remainder, and a settled ৳15,000 tranche
is created beside it carrying the same date, reason and giver.

The alternative — a part-settled row — would need a column recording how much each of several
salary entries had recovered from it, which is a join table wearing a disguise. This way
`IsSettled` stays a fact rather than a comparison, the outstanding total is a plain sum, and **the
expense figure is untouched**: both tranches carry the same `AdvanceDate` and still total what was
handed over.

### 3.4 Dates, money and deletes

- Both money-moving dates are `DATE`, not `DATETIME2`. A salary is paid on a day and an advance is
  handed over on a day; a time component would invite a report to slice on it and quietly drop
  everything stamped 00:00 from a range beginning at 09:00.
- Every money column is `DECIMAL(18,2)`. Unlike purchasing there is no per-base-unit price here.
- **Nothing cascades.** Every foreign key is `NO ACTION`, including
  `SalaryAdvances.SettledInSalaryEntryId` — `SET NULL` would leave `IsSettled = 1` pointing at
  nothing, and the money would never be recovered.

---

## 4. Operating expenses — the part to get exactly right

`IOperatingExpenses.GetOperatingExpensesAsync(from, to)` is implemented in
`PMS.Persistence/Services/OperatingExpenses.cs`, replacing Module 8's placeholder **in place**.

```
GetOperatingExpenses(from, to) =
      SUM(SalaryEntry.NetPayable  WHERE PaymentStatus = Paid
                                    AND PaymentDate  BETWEEN from AND to)
    + SUM(SalaryAdvance.Amount     WHERE AdvanceDate  BETWEEN from AND to)
```

**Both components are required.** `NetPayable` already has the advance subtracted out of it, so
summing it alone silently loses every advance ever given. The advance was real cash leaving the
register on the day it was handed over, and must be counted then — independent of when the salary
entry that nets it out is eventually generated and paid.

The two cannot double-count, and that is a property of the arithmetic rather than a rule applied on
top of it.

### 4.1 The worked example

Karim's base salary is ৳15,000.

| Date | Event |
| --- | --- |
| 28 July | Given a ৳2,000 advance |
| 31 August | August salary generated (net ৳13,000) and paid |

| Month | Operating expenses | Why |
| --- | --- | --- |
| July | **৳2,000** | The advance, dated July — the day the cash left the register |
| August | **৳13,000** | The paid salary, dated August |
| **Both together** | **৳15,000** | Karim's true monthly cost, split across the months cash actually moved |

If only paid entries were summed, July would show nothing and August ৳13,000 — and ৳2,000 of real
cash would appear in no month at all.

### 4.2 Why paid, not generated — read this bit to the owner

The profit report reflects **cash out of the door**, not what is owed on paper. This is cash-basis
accounting, and it has a visible consequence an owner will notice:

> A month where you are behind on paying salaries will look unusually profitable. The month you
> catch up will dip. Neither is a mistake — the money genuinely moved when it moved.

Concretely:

- **A salary generated but not yet paid contributes nothing** to any month's expenses. Generating
  October's payroll early does not change October's profit figure by one paisa.
- **An August salary paid on 2 September counts toward September**, not August.
- An advance counts on its own date regardless of which month eventually deducts it.

Accrual-basis reporting — matching a salary to the month it was *earned* — is explicitly out of
scope. It would require every report to carry two sets of figures, and a pharmacy reconciling
against a cash box would find the accrual set unusable.

### 4.3 Query shape

Two queries, added in memory, not one projection. Each aggregates a plain column over one base
table. Composing them as subqueries inside a single projection reads better and fails at runtime —
SQL Server rejects *"Cannot perform an aggregate function on an expression containing an aggregate
or a subquery"*, which this codebase has hit in Modules 6, 7 and 8. The same structure appears
throughout `SalaryQueries`; see §7.

---

## 5. Generation

`SalaryEntry.Generate(...)` is the only thing that writes `SalaryEntries`. Given a profile, a
period and the figures an Admin typed, it:

1. **Copies** the profile's current `MonthlyBaseSalary` onto the entry.
2. Works out what the month can bear: `BaseSalary + Bonus − OtherDeduction`, clamped at 0.
3. Recovers advances, oldest first, up to the lesser of what was asked for and what the month can
   bear.
4. Sets `AdvanceDeduction` to **what was actually recovered**, and `NetPayable` to the remainder,
   floored at 0.

### 5.1 The advance overshoot rule

When unsettled advances exceed what the month can pay, the deduction is **capped** so `NetPayable`
floors at zero. An employee cannot be asked to hand money back, and a negative payable would be a
number no screen could show honestly.

Whatever is left stays unsettled and reappears on next month's generation screen. The generation
screen warns inline on any row where this will happen:

> Advances exceed this month's payable. ৳X will carry over to next month.

### 5.2 Settlement order is oldest first, and it is deterministic

Advances are settled by `AdvanceDate`, then by `CreatedOnUtc` — so two advances given on the same
day are still ordered by when they were recorded, and the result never depends on the order rows
came back from the database.

Oldest-first is the only order under which an advance cannot be stranded indefinitely by newer ones
jumping the queue, and it is what a person reconciling by hand would do.

**The last advance the deduction reaches may be recovered only in part**, and at most one per entry
ever is. See §3.3 for what that does to the row.

### 5.3 One transaction

Generating a month's payroll for four employees writes four entries, marks their advances settled,
and inserts any split tranches — all in **one** `SaveChanges`. Either every part happens or none
does. A half-generated payroll, with an advance marked settled against an entry that does not
exist, is the outcome the handler's structure exists to prevent.

Everything is checked *before* anything is written, which is what makes that single save the whole
transaction. This is not a stylistic preference: `IUnitOfWork.ExecuteInTransactionAsync` commits as
soon as its delegate returns and inspects nothing about the value, so a handler that wrote first
and returned a failure afterwards would commit the write. Module 4 learned this the hard way.

### 5.4 Locking: a paid entry is immutable

**Once `PaymentStatus = Paid`, nothing on the entry can be changed** — not the bonus, not the
deductions, not the notes. The money has gone and the employee has a slip; an entry that changes
afterwards is not a record of anything.

- The API returns **409** on `PUT /api/salary/entries/{id}` for a paid entry.
- The history screen renders no Edit action on a paid row, and says "Paid entries cannot be edited."
- Following an old edit link for a paid entry redirects to its slip.

**If a correction is genuinely needed after payment, that is a deliberate database intervention,
not a UI path.** Someone with database access must: update the `SalaryEntries` row, and — if the
advance deduction changes — reconcile `SalaryAdvances.IsSettled` and `SettledInSalaryEntryId` by
hand so the two still agree. There is no screen for this on purpose: the situations that call for
it are rare, individually different, and each deserves someone thinking about it.

### 5.5 Editing an unpaid entry

`SalaryEntry.Revise(...)` re-runs settlement from scratch: it releases everything this entry had
settled, then recovers again against the new figures. That is what makes lowering a deduction
genuinely free the advances back up, rather than leaving rows marked settled against money the
pharmacy is no longer recovering.

A tranche released this way is **not** merged back into the row it was split from. Two unsettled
rows of ৳15,000 and ৳1,000 where one ৳16,000 handover used to be is untidy but never wrong: the
outstanding total, the dates and the expense figure are all unchanged.

---

## 6. API

All commands implement `ITenantScopedRequest`; no endpoint takes a tenant id. Every list is 25 per
page by default, clamped at 200 by `SalaryPaging`.

### Profiles

| Method | Route | Notes |
| --- | --- | --- |
| GET | `/api/salary/profiles` | `search`, `status`, `page`, `pageSize`. Each row carries the employee's outstanding advance total |
| GET | `/api/salary/profiles/{id}` | |
| GET | `/api/salary/profiles/eligible-users` | Users without an active profile — the add dropdown |
| GET | `/api/salary/profiles/options` | Active profiles with outstanding totals, for the advance form |
| POST | `/api/salary/profiles` | `userId`, `designation`, `monthlyBaseSalary`, `joiningDate` |
| PUT | `/api/salary/profiles/{id}` | Does **not** retroactively change generated entries |
| PATCH | `/api/salary/profiles/{id}/deactivate` | Soft delete |
| PATCH | `/api/salary/profiles/{id}/reactivate` | 409 if the user already has another active profile |

`POST /profiles` validates the chosen user against the **same list** the dropdown is built from.
That answers two questions at once: `Users` is a global table with no tenant filter, so "is this
person one of ours" cannot be taken on trust from a posted id — and "are they already on the
payroll" becomes a sentence rather than a unique-index violation.

### Advances

| Method | Route | Notes |
| --- | --- | --- |
| GET | `/api/salary/advances` | `profileId`, `settlement` (All/Unsettled/Settled) |
| POST | `/api/salary/advances` | `profileId`, `amount`, `advanceDate` (defaults today), `reason` |

An advance against a **deactivated** profile is refused: it could never be deducted, because that
employee will never appear on a generation screen again.

There is deliberately **no cap** on the amount relative to the employee's salary. An owner who
hands over more than a month's pay has done so; refusing to record it would leave the cash
untracked, which is the one outcome this module exists to prevent. Generation caps the *deduction*
instead.

### Salary entries

| Method | Route | Notes |
| --- | --- | --- |
| GET | `/api/salary/generate/preview?month=&year=` | Every active profile, with its unsettled advances itemised. Already-generated profiles come back **flagged, not omitted** |
| POST | `/api/salary/generate` | `month`, `year`, `lines[]`. One transaction |
| GET | `/api/salary/entries` | `month`, `year`, `profileId`, `status` |
| GET | `/api/salary/entries/{id}` | |
| PUT | `/api/salary/entries/{id}` | **409 if paid** |
| PATCH | `/api/salary/entries/{id}/pay` | `paymentDate`, defaults today. 409 if already paid |
| GET | `/api/salary/entries/{id}/slip` | Includes the advances this entry recovered |
| GET | `/api/salary/summary` | The hub's figures |

Each generation line's `advanceDeduction` is a **request, not a result**: the domain caps it and
decides which advances it covers. The response reports what actually happened —
`advancesSettled` and `advancesCarriedOver`.

Paying twice is refused rather than silently accepted. On a screen somebody refreshes, a second
press would otherwise move the payment date — and with it, which month the expense falls in.

---

## 7. Query shapes

`ISalaryQueries` / `SalaryQueries` is the read side. Two structural rules run through it:

**No advance total is ever a correlated subquery inside a row projection.** "Each profile with the
sum of its unsettled advances" is exactly the shape SQL Server rejects. Every method that needs one
pages or filters its profiles first, then aggregates over `SalaryAdvances` filtered by those ids,
and joins the two in memory — the same structure `SupplierBalanceQueries` uses, for the same
reason.

**Filters apply to the entity, and the join-plus-projection is terminal.** Projecting a three-way
join into a named record so that callers could filter over it does not translate: EF cannot see
through a record constructor and the whole query fails at runtime. So `GetEntriesAsync` narrows
`SalaryEntries` with plain predicates, then hands the narrowed query to `ProjectEntries`, which the
single-entry read also calls. Sharing one projection is still worth the awkwardness — Module 8
shipped a report whose streaming path worked while its paginated path 500'd, because the two built
their own.

---

## 8. Key decisions

**Why advances are tracked when given rather than remembered at month-end.** An advance typed in
during generation depends on somebody recalling a note handed over three weeks earlier. When they
do not, the employee is paid in full on top of money they already had, and nothing ever says so.
Recording it on its own screen also captures the date, the reason and who handed it over — which is
what makes the generation screen's breakdown mean something months later.

**Why an advance is an expense on the advance date, not the settlement date.** The cash left the
register then. Counting it when the salary that nets it out is eventually paid would put it in the
wrong month, and an advance never deducted would never be counted at all.

**Why paid entries lock.** The money has gone and the employee has a slip. An entry that can still
change is not a record; and because paying is what makes an entry an expense, an editable paid
entry would let a month's reported profit move after the fact.

**Why the profile is separate from `User`.** See §3.1.

**Why `BaseSalary` is copied per entry.** See §3.2.

**Why a split tranche rather than a part-settled row.** See §3.3.

**Why there is no delete-entry endpoint.** Deleting an entry would have to release its advances
first; forgetting would leave `IsSettled = 1` pointing at nothing. The `NO ACTION` foreign key
refuses the delete rather than letting it skip that step. An unpaid entry that was generated in
error is corrected by editing it.

---

## 9. Out of scope

Explicitly, and each for a reason:

- **Attendance and leave management.** A different module with a different shape.
- **Tax and statutory deductions** (provident fund, etc.). `OtherDeduction` is a single free amount
  with notes, not a compliance calculation — and a half-built compliance calculation is worse than
  none.
- **Automatic scheduled generation.** Manual only. A payroll that appears on its own on the 1st is
  a payroll nobody checked.
- **Payslip email delivery.** Print only.
- **Partial payment of a single entry.** One paid/unpaid status, no instalments. A half-paid salary
  is a conversation between an owner and an employee, not a record the pharmacy reconciles; the
  honest way to record one is a smaller salary now and a bonus next month.
- **Accrual-basis expense reporting.** Cash basis only — see §4.2.
- **A future-date guard on payment or advance dates.** Consistent with Module 4, which records the
  date it is told: a typo is a data-entry problem, and refusing a date somebody genuinely means
  leaves cash untracked.

---

## 10. Files

**Domain** — `Entities/EmployeeSalaryProfile.cs`, `Entities/SalaryEntry.cs`,
`Entities/SalaryAdvance.cs`, `Enums/SalaryPaymentStatus.cs`

**Application** — `Common/DTOs/SalaryDtos.cs`, `Common/Salary/SalaryPaging.cs`,
`Common/Salary/SalaryPeriod.cs`, `Interfaces/ISalaryQueries.cs`, `Interfaces/IOperatingExpenses.cs`,
`Features/Salary/**`

**Persistence** — `Configurations/SalaryConfigurations.cs`, `Services/SalaryQueries.cs`,
`Services/OperatingExpenses.cs`

**API** — `Controllers/SalaryController.cs`

**Web** — `Api/SalaryContracts.cs`, `Pages/Salary/**`, `wwwroot/js/salary-advance.js`,
`wwwroot/js/salary-generate.js`

**Database** — `database/scripts/015_CreateSalaryTables.sql`

**Tests** — `tests/PMS.UnitTests/Domain/Salary/SalaryEntryTests.cs`,
`tests/PMS.SchemaTests/Salary/SalarySchemaTests.cs`

See [frontend/09-salary-management.md](frontend/09-salary-management.md) for the screens.
