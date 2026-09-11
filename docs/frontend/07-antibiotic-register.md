# Frontend 07 — Antibiotic Register

Two pages, one dashboard card, and a change to the billing screen.

The regulatory context, the three-mode decision and the query rules are in
[`docs/07-antibiotic-register.md`](../07-antibiotic-register.md). This document is about the
screens.

---

## 1. Page inventory

| Route | Page | Access | Purpose |
|---|---|---|---|
| `/antibiotics/register` | `Pages/Antibiotics/Register` | Admin, Pharmacist | The register. Printable. |
| `/antibiotics/register?handler=Export` | same page, handler | Admin, Pharmacist | Streamed CSV. |
| `/settings/antibiotics` | `Pages/Settings/Antibiotics` | **Admin only** | The three-mode radio group. |
| `/` | `Pages/Index` | Admin, Pharmacist | "Antibiotics dispensed this month" card. |
| `/billing` | `Pages/Billing/Index` | — | Prescription panel now follows the mode. |

### Navigation

One entry, `Antibiotic register`, visible to **Admin and Pharmacist only**:

```csharp
new NavItem("Antibiotic register", "/Antibiotics/Register", "clipboard",
    Roles: new[] { UserRole.Admin, UserRole.Pharmacist },
    MatchPrefix: "/antibiotics"),
```

An Employee sees no entry, no dashboard card, and gets an access-denied redirect on the route —
even though, depending on the pharmacy's mode, they may be able to sell antibiotics all day. The
asymmetry is explained in the backend doc; it is the thing a reviewer is most likely to read as a
mistake, so it is stated in both.

A new `clipboard` icon, which is what the register physically is in a pharmacy that keeps one on
paper. It had to be distinguishable at 18px from the receipt and the alert bell already in the
sidebar.

---

## 2. Why the register is styled as an official record

This page may be printed and handed to an inspector. That drives most of its design, and it is the
one screen in the product that is deliberately *less* decorated than the rest:

- **No zebra striping, no status colours, no badges except one.** Every bit of colour that is not
  carrying information competes with the bit that is.
- **Dense but legible.** Tighter cell padding than the other grids, because there are nine columns
  and a reader is scanning down one of them — but the font-size floor is untouched.
- **The only colour on the page is the red flag**, and only under Required mode.

Compare the alerts pages, which are the opposite case: there, colour *is* the information.

Columns: Date, Invoice, Product (brand with generic beneath in muted text), Quantity, Patient
name, Patient phone, Doctor, Prescription number, Sold by. The invoice number links to the full
invoice from Module 5.

**Prescription columns show `—` when nothing was captured.** Written as `&mdash;` in markup rather
than as a C# string: Razor's `@()` runs output through `HtmlEncoder`, which escapes non-ASCII to
numeric entities, and the rest of the app already uses the entity form.

**Rows with returns carry a muted sub-line** — "Returned: 6 pieces" — because the row stays in the
register and the net dispensed has to be visible.

---

## 3. The mode indicator and its note

Beneath the header, before the filters, on screen **and in print**:

> **Prescription capture: Off**  ·  *Change*
>
> This pharmacy does not currently record prescription details for antibiotic sales. All
> antibiotic sales are still tracked below.

**The note under Off is the most important text on the page.** A register whose patient and doctor
columns are entirely dashes reads one of two ways to someone who does not know the setting: as a
system that lost the data, or as a pharmacy in breach. Neither is true, and the reader may be an
inspector. Leaving them to guess is the worst option available.

Each mode gets its own wording (`AntibioticModeNoticeModel.For`):

| Mode | Note |
|---|---|
| Off | "This pharmacy does not currently record prescription details… All antibiotic sales are still tracked below." |
| Optional | "Staff record prescription details when they have them. Rows without them are expected, not errors." |
| Required | "Every antibiotic sale must carry a verified prescription. Rows flagged below predate this setting or indicate a problem…" |

The *Change* link appears only for an Admin — the only role that can act on it.

### The red flag, and when it does not appear

`AntibioticRegisterRow.IsFlagged(mode)` returns true only when the mode is `Required` **and** the
prescription is missing or unverified. Under Off and Optional, absence is expected and nothing is
flagged.

When any row on the page is flagged, a line beneath the table says why — once, quietly, rather
than as a banner:

> 3 rows on this page are flagged because prescription capture is set to Required and no verified
> prescription was recorded. Sales made while this pharmacy was set to Off or Optional will always
> appear this way: they were recorded correctly under the rules in force at the time.

A pharmacy that has just switched to Required sees every older row light up. Without that
sentence, the obvious conclusion is that the system is broken.

---

## 4. Filters and the summary line

Six filters: date range (defaulting to the current month), product, doctor name, cashier,
prescription status. The product dropdown lists **only antibiotic products this pharmacy holds**,
and the cashier dropdown is drawn from the antibiotic sales themselves — so it never offers
somebody who has never dispensed one, and never omits a cashier who has since left.

The prescription-status filter earns its place under Optional, where a pharmacy wants to see how
much of its own record-keeping is actually happening.

The summary line sits above the table:

> Showing 47 antibiotic sales from 1 Aug 2026 to 31 Aug 2026. Total dispensed: **1,240 pieces**.

It describes the **whole filtered range**, not the current page — the API returns the page and the
range total in one response for exactly this reason. The unit word is "pieces" only when every row
in the set shares that base unit; otherwise it is "units", because summing tablets and bottles
into one number and naming it after one of them would overstate what the figure means.

The date range on the page is read back from the API response rather than from the query string,
so the heading and the printed header cannot claim a range the table was not built from.

---

## 5. Print stylesheet

`@media print` hides the nav, the top bar, the filter bar, the summary line, the action buttons
and the pagination — everything carrying `pms-no-print`.

**A printed header appears in its place**, hidden on screen (`pms-print-only`):

```
Antibiotic sales register
Popular Pharmacy
1 Aug 2026 – 31 Aug 2026
Prescription capture: Off
47 antibiotic sales. Total dispensed: 1,240 pieces.
```

A sheet on a desk has no address bar and no navigation to infer context from, so the printed page
has to say whose register it is, what it covers, what the pharmacy's policy is and what the totals
are. The mode indicator stays in print too — without its tint — because a page of dashes in the
prescription columns needs the line that explains them more on paper than on screen.

Two details for monochrome printers: the table keeps hairline rules instead of relying on a tint
that may not print, and a flagged row gets a **left border rule** as well as its background, so it
still reads as flagged in black and white.

---

## 6. The settings page, and why the wording is long

`/settings/antibiotics`. Admin only. A radio group, three options, and a description under each
that is longer than the label on any other setting in the application.

That is deliberate. **This setting has legal consequences in both directions.** Required stops
Employees dispensing mid-shift; Off stops prescriptions being recorded at all. Nobody should have
to infer either from a one-word name — least of all an owner choosing under time pressure before
an inspection, which is precisely when this page will be opened.

The exact wording, from the brief:

- **Off** — "Antibiotics are sold like any other product. No prescription details are recorded.
  All antibiotic sales are still tracked in the register."
- **Optional** — "Staff can record prescription details when available, but sales are not blocked
  if they don't."
- **Required** — "Prescription details must be recorded for every antibiotic sale, and only
  pharmacists or admins can sell antibiotics. Use this if your pharmacy is under Model Pharmacy
  rules or preparing for inspection."

Each option shows a "Current" badge when it is the one in force, so the page answers "what are we
on now" before it asks "what should we be on".

### The confirmation, and when it fires

Switching **to Required** confirms, through the shared modal:

> **Require prescriptions for antibiotics?**
> This will prevent employees from selling antibiotics and require prescription details on every
> antibiotic sale. Staff will need to be informed. Continue?

`antibiotic-settings.js` attaches the confirmation only when the selected mode is Required **and
that is a change**. Confirming on every save — including Off to Off — would train an owner to
click through the dialogue, and by the time the one that mattered appeared they would not be
reading it.

It degrades cleanly: with no script the button is a plain submit and the form still saves. The
confirmation is a safeguard for the person, not the enforcement — the API applies whatever mode it
is sent.

The success message tells the Admin what they have just done to their staff:

> Prescription capture is required. Employees can no longer sell antibiotics, and every antibiotic
> sale now needs a verified prescription. Let your staff know.

Saving takes effect immediately: `ITenantSettings` reads once per request, so the next page load
and the next sale are both under the new rule.

---

## 7. The dashboard card

One card, Admin and Pharmacist only:

> Antibiotics dispensed this month
> **1,240 pieces**
> 47 sales · view the register

**Neutral styling, not a warning.** A pharmacy dispensing antibiotics is a pharmacy doing its job.
Colouring the figure amber would file it alongside expired stock and low inventory — things that
are wrong and need fixing — and this is a figure an owner wants to know and a regulator may ask
about. It uses `pms-info-card`, which is new and deliberately unlike `pms-alert-card`.

It links to the register, and a failed lookup is silent for the same reason the alert panels
tolerate one: a dashboard should not fall over because one card could not be filled in.

---

## 8. The billing screen's prescription panel

Module 5's panel is now driven by the mode, which arrives with the rest of the caller's limits from
`GET /api/sales/limits`.

| Mode | Panel |
|---|---|
| `Off` | **Not rendered at all.** Not hidden — absent from the HTML. |
| `Optional` | Rendered when the cart holds an antibiotic. All fields optional, heading reads "(optional — record what you have)", and *Complete sale* is not gated on it. |
| `Required` | As Module 5 shipped: every field required, verification box required, button disabled until both. |

**Why Off renders nothing rather than hiding it.** A pharmacy that does not record prescriptions
should not have a prescription form on its till screen. A form that appears with every field
optional and no consequence is a form people learn to scroll past — and the fields would post
empty values on every sale for no purpose.

**Why Optional does not gate the button.** Gating it would make "optional" a lie. The point of
that mode is that a cashier records what they have and the sale goes through either way.

`billing.js` reads `data-prescription-required` off the panel itself rather than inferring the mode,
so the screen and the sale agree about which rules are in force. Under Off the panel is not in the
DOM and the check short-circuits.

---

## 9. New shared components

| Component | Change |
|---|---|
| `_AntibioticModeNotice.cshtml` | New. The mode indicator, on screen and in print. |
| `AntibioticModeNoticeModel` | New. Per-mode wording, including the Off explanation. |
| `antibiotic-settings.js` | New. Conditional confirmation on the settings page. |
| `_NavIcon.cshtml` | `clipboard`. |
| `BillingLimits` | Carries `AntibioticMode`, plus `CapturesPrescriptions` and `RequiresPrescription`. Its `None` fallback is `Off` — see below. |
| `site.css` | `pms-mode-notice`, `pms-register`, `pms-mode-option`, `pms-info-card`, `pms-print-only`, and the register's print block. |

### The fallback asymmetry in `BillingLimits.None`

When the limits call fails, the fallback is no permissions **and** the loosest antibiotic mode.
That looks inconsistent and is not.

Falling back to no permissions is safe: it only hides controls the server would refuse anyway.
Falling back to `Required` would be the opposite — it would render a mandatory prescription panel
at a pharmacy that does not collect one, and the cashier would have to invent data to get past a
screen that was wrong about the rules.

---

## 10. Out of scope

- Prescription image upload.
- A general settings screen. One setting, one page; the Settings module will absorb it.
- PDF export.
- Any way for an Employee to reach the register, including read-only.
