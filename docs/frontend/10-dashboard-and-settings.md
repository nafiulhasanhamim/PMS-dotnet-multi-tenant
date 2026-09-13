# Module 10 — Dashboard & Settings (frontend)

Two pages. The backend reasoning is in [../10-dashboard-and-settings.md](../10-dashboard-and-settings.md).

---

## 1. Page inventory

| Route | Page | Access |
| --- | --- | --- |
| `/` | `Index` | every role, tiered content |
| `/settings` | `Settings/Index` | **Admin** |
| `/settings/antibiotics` | `Settings/Antibiotics` | **Admin** — Module 7's focused screen, kept |

One new sidebar entry: **Settings → `/settings`**, `sliders` icon, Admin only, near the bottom
because it is configured once and then rarely opened. The dashboard entry already existed from
Module 1.

Module 7's single-setting screen survives because the antibiotic register's mode notice links
straight to it ("Change"), and because it is reachable at a moment when saving the whole settings
page would be the wrong shape. Both screens write through the same service into the same row.

---

## 2. The dashboard

### 2.1 One call

Until this module the page made three API calls — the profile, the alert summary and the
antibiotic figure — and every new module would have added another. It now makes exactly one, and
the web smoke asserts that by reading the page model's source: `_api.` appears once.

Three round trips to draw one screen means three chances at a partial render and three times the
latency on a shop's connection. It also means role visibility gets decided once, on the server,
instead of being re-derived from cookie claims in Razor.

### 2.2 The header

The **pharmacy name is the `<h1>`**, not the greeting. Somebody with access to more than one
pharmacy is looking at two otherwise identical screens, and this is the only thing telling them
apart. It comes from settings.

The greeting — "Welcome, Karim · Admin" — sits beneath it as the subtitle, rendered from the
sign-in cookie. The API has no name claim to give.

### 2.3 Quick actions

| Role | Buttons |
| --- | --- |
| Employee | **New sale** |
| Pharmacist, Admin | **New sale**, Add stock, New purchase |

An Employee gets one button because a cashier opening this page is almost always about to ring
something up, and a row of choices would slow that down. They do not get Add stock or New purchase
because they cannot do either, and offering a button that 403s is a worse introduction to a system
than not offering it.

### 2.4 Card visibility

| Card | Employee | Pharmacist | Admin |
| --- | --- | --- | --- |
| Expiring soon / Expired / Low stock / Out of stock | ✓ | ✓ | ✓ |
| Today's sales | | ✓ | ✓ |
| Antibiotics this month | | ✓ | ✓ |
| Today's profit | | | ✓ |
| Supplier dues | | | ✓ |
| Unpaid salary | | | ✓ |

**The page renders what is present.** A card a role may not see arrives `null` from the API, so
there is no `@if (User.IsTenantAdmin())` anywhere on this page — the server already decided, and
two implementations of that rule is one too many. The profit figure in particular never reaches a
Pharmacist's browser.

The four alert cards reuse Module 6's `_AlertCard` partial and `AlertCardModel`, as the brief
asks, rather than introducing a second card component.

### 2.5 Calm when zero

This is a styling rule with a reason behind it.

`AlertCardModel.Css` already renders a card in `pms-alert-card--quiet` when its count is zero —
no warning colour, no danger colour. Beneath the grid, when *nothing* needs attention, the page
says so **once**, quietly:

> Nothing needs attention: every product is above its reorder level and nothing is within 90 days
> of expiry.

Not four green ticks, and not four warning-coloured boxes reading zero. **A dashboard that shouts
on a good day teaches people to stop reading it, and then it is no use on a bad one.** The smoke
harness creates a brand-new pharmacy specifically to check that its dashboard renders with four
quiet cards and no danger or warning classes at all.

The number in that sentence is the pharmacy's own expiry window, taken from the same response that
produced the counts — so the figure it prints is the figure it counted with.

### 2.6 Supplier dues, and suppliers in credit

The card shows the net outstanding, red when the pharmacy owes anything, with a link to Module 8's
dues report. Where any supplier is in credit, a second note line appears:

> 2 suppliers in credit, holding ৳155.00

**The word is "in credit", never "overpaid"** — Module 4's terminology, because returning goods
after paying produces the same state without anybody overpaying. The smoke harness asserts the
absence of "overpaid" on the rendered page.

### 2.7 When the call fails

The page still renders: header, quick actions and a warning banner. The quick actions are the most
valuable thing on this screen for an Employee, and a cashier should not lose their way to the till
because a count could not be fetched. A 401 or a suspended tenant still redirects, because those
mean the session itself is unusable.

---

## 3. The settings page

### 3.1 Grouping and a single save

Four sections in a `pms-card` each, then **one** "Save settings" button for the page:

1. **Pharmacy details** — name\*, address, phone\*, drug licence number
2. **Alerts** — expiry window, dead-stock threshold, default reorder level
3. **Billing rules** — Employee and Pharmacist maximum discount
4. **Antibiotic prescription capture** — the three-mode radio group

Per-field saves would mean nine forms, nine success banners and nine chances to leave the page
half-changed. The API applies the whole set in one transaction or none of it, and the screen is
shaped to match: fill it in, press Save once, and either everything took or nothing did with the
reasons shown against the fields.

Every helper string is the one the brief specifies, because each answers a question the label
alone raises — "Existing products are not affected" under the reorder level is the difference
between an Admin changing it and an Admin being afraid to.

### 3.2 Validation

Client-side `[Range]` and `[Required]` rules mirror the API's exactly. They exist to catch a typo
before a round trip, **not** to be the control: the server refuses the same values with the same
bounds, and a browser with scripting off is refused there instead. The API's per-field messages
land under the same inputs the client-side hints would have flagged, through `PmsPageModel`.

Form-level problems render as a banner at the top; field problems render inline. On a refused
save the page reloads the **stored** settings first, so the "Current" badges on the mode radios
still describe what is really in force rather than what the person typed.

### 3.3 The antibiotic mode, and why the descriptions are long

The three descriptions are longer than the labels on any other setting in the application, and
that is deliberate.

> **Off** — Antibiotics are sold like any other product. No prescription details are recorded. All
> antibiotic sales are still tracked in the register.
>
> **Optional** — Staff can record prescription details when available, but sales are not blocked
> if they don't.
>
> **Required** — Prescription details must be recorded for every antibiotic sale, and only
> pharmacists or admins can sell antibiotics. Use this if your pharmacy is under Model Pharmacy
> rules or preparing for inspection.

**This setting has legal consequences in both directions.** Required stops Employees dispensing
mid-shift; Off stops prescriptions being recorded at all. Nobody should have to infer that from
three one-word names — and an owner choosing under time pressure before an inspection least of
all. The current mode carries a "Current" badge so the starting point is never ambiguous.

### 3.4 The confirmation, and when it fires

Switching **to Required** asks first:

> This will prevent employees from selling antibiotics and require prescription details on every
> antibiotic sale. Staff will need to be informed. Continue?

`settings-page.js` attaches the confirmation only when Required is a *change*. Confirming on every
save would train an owner to click through the dialogue, and by the time the one that mattered
appeared they would not be reading it. That matters more here than on Module 7's focused screen,
because this page is saved for ordinary reasons — correcting a phone number — far more often than
for that one.

Progressive: with no script the button is a plain submit and the form still saves. The
confirmation is a courtesy; the API applies whatever mode it is sent and refuses whatever it
should refuse.

---

## 4. What the migration changed on other screens

| Screen | Before | After |
| --- | --- | --- |
| `Sales/Detail` (invoice) | `Address line one, Dhaka` / `Phone 01700-000000` in the markup | the pharmacy's own details, plus the drug licence where set |
| `Salary/Entries/Slip` | the same two literals | the same, from the same settings call |
| `Medicines/Create`, `OtherItems/Create` | reorder level started at `100` | starts at `default_reorder_level` |
| `Medicines/BulkSetup` | every row started at `100` | every row starts at the setting |
| `Alerts/Expiring` | window dropdown `[30,60,90,180]`, default `90` | the same four **plus the pharmacy's own**, defaulting to it |
| `Reports/DeadStock` | threshold dropdown `[30,60,90,180]` | the same, plus the pharmacy's own |
| `Billing/Index` | helper text from a constant | helper text from the configured cap |

The invoice and slip fall back to the cookie's tenant name if the settings call fails — a document
that cannot name the pharmacy is not one anybody can hand over. Both headers render address, phone
and licence only when set, so a pharmacy that has filled in none of them gets a clean header
rather than blank lines.

The two dropdowns now include the pharmacy's configured value even when it is not one of the four
standard periods. A pharmacy that set 45 days and then found the dropdown could not show 45 would
have a settings screen its own alert page disagreed with.

---

## 5. Files

`Pages/Index.cshtml(.cs)` — rewritten
`Pages/Settings/Index.cshtml(.cs)` — new
`Api/SettingsContracts.cs`, `Api/PmsApiClient.cs` (Module 10 section)
`Navigation/NavRegistry.cs`, `Pages/Shared/_NavIcon.cshtml` (the `sliders` glyph)
`wwwroot/js/settings-page.js`
`Pages/Sales/Detail.cshtml(.cs)`, `Pages/Salary/Entries/Slip.cshtml(.cs)` — headers from settings
`Pages/Products/ProductFormModel.cs`, `ProductWritePageModel.cs`,
`Pages/Medicines/Create.cshtml.cs`, `Pages/Medicines/BulkSetup.cshtml.cs`,
`Pages/OtherItems/Create.cshtml.cs` — reorder level from settings
`Pages/Alerts/Expiring.cshtml(.cs)`, `Pages/Reports/DeadStock.cshtml(.cs)` — configured window
`Api/AlertContracts.cs`, `Api/ReportContracts.cs` — fallbacks point at `TenantSettings.Fallback`
