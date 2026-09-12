# Module 9 — Salary Management (frontend)

Eight Razor pages under `/salary`, all Admin-only. The backend reasoning is in
[../09-salary-management.md](../09-salary-management.md).

---

## 1. Navigation

One sidebar entry: **Salary → `/salary`**, with the `wallet` icon, `Roles: [Admin]`.

Absent entirely for a Pharmacist and an Employee. As always, nav visibility is a convenience and
never the boundary — every page carries `[Authorize(Policy = WebPolicies.TenantAdmin)]`, and that
is what actually stops them. The smoke harness checks both halves.

---

## 2. Page inventory

| Route | Page | What it is for |
| --- | --- | --- |
| `/salary` | `Salary/Index` | Hub: three figures, five cards, and the timing explanation |
| `/salary/profiles` | `Salary/Profiles/Index` | The payroll, with outstanding advances per person |
| `/salary/profiles/edit/{id?}` | `Salary/Profiles/Edit` | Add or edit a profile |
| `/salary/advances` | `Salary/Advances/Index` | Every advance, settled and outstanding |
| `/salary/advances/create` | `Salary/Advances/Create` | Record cash handed over |
| `/salary/generate` | `Salary/Generate` | A month's payroll, one row per employee |
| `/salary/entries` | `Salary/Entries/Index` | Salary history, with Mark paid |
| `/salary/entries/edit/{id}` | `Salary/Entries/Edit` | Correct an **unpaid** entry |
| `/salary/entries/{id}/slip` | `Salary/Entries/Slip` | Printable slip |

All four paginated lists use 25 per page and the shared `_ApiPagination` partial, carrying their
filters through in route values.

---

## 3. The hub

Three stat cards — unpaid salaries (count and total), outstanding advances (count and total), and
the active headcount. The first two point in **opposite directions**: one is money the pharmacy
owes its staff, the other is money its staff owe back. Showing them side by side is the point; an
owner reading "12,000 outstanding" needs to know which way it goes.

Beneath the cards, a `report-note` explains the cash-basis timing in plain language. It is here
rather than buried in the docs because an owner *will* ask why August looked unusually profitable,
and the answer belongs on the screen they will be looking at.

If the summary call fails the cards are empty but the five links still render. A summary that
failed to load is a reason to say so, not a reason to withhold the links somebody came here to
follow.

---

## 4. Salary profiles

Search matches employee name or designation; the status filter defaults to Active.

The outstanding-advance column is styled as `figure-negative` but it is **not** a debt of the
pharmacy's — it is money the employee owes back. Nothing outstanding renders as a muted dash rather
than a row of zeroes, which reads faster.

**Deactivate** opens the shared `_ConfirmModal` with a body that spells out what is preserved:

> They will disappear from the salary generation screen and from the advance form. Every salary
> already generated for them, and every advance recorded against them, stays exactly as it is —
> nothing is deleted. You can put them back on the payroll later.

Reactivate is a plain submit — there is nothing to warn about — but it can still come back 409 if
the person has since been given a second profile. That renders against the reloaded list rather
than a redirect, so the message sits beside the rows it is about.

### The form

One page for add and edit. The difference is the employee:

- **Adding** offers a dropdown of users *without* an active profile, with a note explaining that
  not every user needs one. If everybody already has a profile, an info alert replaces the
  dropdown rather than showing an empty select.
- **Editing** renders the employee as read-only text plus a hidden field, and says why: moving a
  profile to a different person would take their salary history with it.

The base-salary field carries the note the brief asks for, and it changes wording between add and
edit:

> **Changing this affects future salary generation only.** Every month already generated copied the
> figure that applied at the time, so a raise now will not restate what this person was paid in
> July.

---

## 5. Recording an advance — why it has its own screen

This is the screen the module is built around. An advance typed in during generation depends on
somebody remembering it three weeks later; recorded here, it is captured with its date, its reason
and who handed it over.

The employee picker shows **what is already outstanding**:

- Every `<option>` carries `data-outstanding` and `data-salary`, rendered server-side.
- `salary-advance.js` reads them on `change` and rewrites the help text beneath the select.
- With JavaScript off, the option text itself names the outstanding total — so the figure is never
  missing, only less prominent.

That shape is deliberate: fetching the total on change would make it appear sometimes, and the one
moment it matters is the moment before somebody hands over more cash.

The date field says plainly what it controls:

> **The day the cash actually changed hands.** This is the date the expense falls on in the profit
> report, not the month it is eventually deducted in.

On save the success message names the outstanding total *after*, not just the amount recorded, so
somebody handing over a third advance sees the running figure without opening another screen.

---

## 6. The generation screen

`/salary/generate`, with a month/year picker defaulting to the current month and a five-year
window around it — payroll is generated for the month just ended or, occasionally, one that was
missed.

### The grid

One row per active profile:

| Column | Behaviour |
| --- | --- |
| Checkbox | Ticked by default. Unticking greys the row and disables its inputs |
| Employee, designation | Read-only |
| Base salary | Read-only — copied at generation, not editable here |
| Bonus | Editable, default 0 |
| Advance deduction | Editable, **pre-filled with the unsettled total** |
| Other deduction | Editable, default 0 |
| Notes | Editable; required if bonus or other-deduction is non-zero |
| Net payable | Read-only, recomputed live |

### The unsettled-advance breakdown

Beneath the advance-deduction input, a `<details>` element lists **every** outstanding advance for
that employee — date, amount and reason:

> 15 Aug 2026 · 2,000.00 · medical emergency

This is what replaces relying on memory, and it is why a total alone is not enough: a number still
asks somebody to trust it, whereas the dates and reasons let them recognise the cash they handed
over. An employee with nothing outstanding gets "Nothing outstanding" in its place.

### Already-generated rows

Profiles with an entry for the period render **greyed and unselectable**, badged "Already
generated", with a link to the existing slip. They are never omitted: an employee missing from this
table is indistinguishable from one nobody put on the payroll, and the difference matters
enormously on the one screen where somebody decides who gets paid.

### Live recalculation, and what it is not

`salary-generate.js` recomputes net payable on every `input` and `change`, keeps a running total
and a selected count in the footer, and disables the submit button when nothing is ticked.

It mirrors `SalaryEntry.Generate` exactly — `payable = base + bonus − other`, `recovered =
min(requested, payable)`, `net = max(0, payable − recovered)` — and keeping the two in step
matters: a preview promising ৳0 against a server producing ৳2,000 would be worse than no preview.

**Nothing computed in that file is submitted.** The inputs are, and that is all. The server
recomputes net payable from the same rules, caps the deduction itself, and the response reports
what actually happened. If the script fails to load, the form still works and still produces
correct entries — it just stops showing the answer in advance.

### The overshoot warning

Rendered inline on any row where the requested deduction exceeds what the month can bear:

> Advances exceed this month's payable. ৳1,000.00 will carry over to next month.

with net payable showing ৳0.00. The server-rendered first paint already carries the row's
`Overshoot`, so the warning is correct before a single keystroke.

### Confirmation

Submitting asks:

> Generate salary for 4 employees for August 2026? Total payable: ৳52,500.00.

Not a nicety. This writes a locked-once-paid record for several people at once, and the totals are
the thing worth reading back before it happens.

After a successful post the flash message reports what was **actually** settled and what carried
over, which differ from what was requested whenever a month could not cover somebody's advances.

---

## 7. Salary history

Filters: month, year, employee, payment status. The deductions column shows the combined figure
with the advance portion called out beneath it.

**Mark paid** puts a date input directly in the row — defaulting to today — followed by a
confirmation:

> {Employee} will be recorded as paid ৳13,500.00. The entry LOCKS once paid — the bonus, the
> deductions and the notes can no longer be changed. It also becomes an operating expense on the
> payment date you entered, not in the month it covers.

The date is inline rather than in a modal because getting it right is the whole act; asking for it
in a second step would put a dialog in front of the most routine thing on the page.

**A paid row renders no Edit action**, and says so rather than leaving somebody hunting for a
button that used to be there:

> Paid entries cannot be edited.

Following an old `/salary/entries/edit/{id}` link for a paid entry **redirects to its slip** rather
than rendering a form with a disabled button. A paid entry has no editable state at all, and the
slip is what somebody following that link actually wants.

### Editing an unpaid entry

Base salary is shown read-only with its reasoning. The advance-deduction field explains both
directions:

> Lowering this releases advances back into next month's generation; raising it recovers more, up
> to whatever this month can bear. Net payable never goes below zero.

A closing note repeats that the entry becomes immutable once paid, and that a correction after
payment is a database intervention rather than a screen.

---

## 8. The slip

Follows the invoice's print approach from Module 5: `pms-invoice` markup, a `pms-no-print` header,
and `window.print()` on a button that does not appear on paper.

Pharmacy name comes from the session; address and phone are placeholders until Module 10's
settings, exactly as the invoice carries them.

The body itemises base salary, bonus (with notes), **the advances recovered — each with its date
and reason** — and other deduction, then net payable in bold.

That advance list is the reason a slip is worth printing at all: an employee handed ৳13,000 against
a ৳15,000 salary can see exactly which advances account for the difference.

**An unpaid entry has a slip too**, and it says so on its face, in a banner that prints:

> NOT YET PAID. This slip shows what is due, not what has been handed over.

Handing somebody the breakdown before the money moves is how a disagreement gets settled before it
becomes one — but a slip that printed identically whether or not the money had moved is a document
somebody could present as proof of payment. Same reasoning as a cancelled invoice in Module 5.

---

## 9. The Module 8 seam, from the browser's side

Two things changed outside this module:

- `/reports/monthly-sales` no longer carries the note saying salary recording "arrives with the
  Salary module". What replaced it fires only on a month with genuinely zero expenses and points at
  the likely cause — a payroll generated but not marked paid.
- The CSV export carries the same note, and now **only when it applies**. Module 8 printed it
  unconditionally because expenses did not exist; a caveat on a month with real payroll in it would
  be worse than none.

The contract property driving both is now `NoExpensesRecorded` rather than `ExpensesMissing`.

---

## 10. Files

`Pages/Salary/Index.cshtml(.cs)`
`Pages/Salary/Generate.cshtml(.cs)`
`Pages/Salary/Profiles/Index.cshtml(.cs)`, `Profiles/Edit.cshtml(.cs)`
`Pages/Salary/Advances/Index.cshtml(.cs)`, `Advances/Create.cshtml(.cs)`
`Pages/Salary/Entries/Index.cshtml(.cs)`, `Entries/Edit.cshtml(.cs)`, `Entries/Slip.cshtml(.cs)`
`Api/SalaryContracts.cs`, `Api/PmsApiClient.cs` (Module 9 section)
`Navigation/NavRegistry.cs`, `Pages/Shared/_NavIcon.cshtml` (the `wallet` glyph)
`wwwroot/js/salary-advance.js`, `wwwroot/js/salary-generate.js`
