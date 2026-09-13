# Module 10 — Dashboard & Settings

The last module, and both halves of it are consolidation rather than new capability.

---

## 1. Overview

Two jobs.

**The dashboard** had accumulated. Module 1 left a placeholder panel reading "more panels arrive
with their modules", Module 6 added alert cards, Module 7 added an antibiotic figure, and the page
made three separate API calls to assemble them. This module replaces that with one deliberate,
role-tiered home screen fed by a single endpoint.

**Settings is the half that matters.** Five values were hardcoded across Modules 2, 5, 6 and 8,
each with a comment promising that a settings module would move it. `BillingPolicy`'s own class
doc said, in as many words, *"A Settings module will eventually make these per-pharmacy, and the
work of that module is then to replace this class"*. Migration 012 said the same about the
antibiotic mode column.

> **The point of this module is not a settings page that looks right.** It is that nothing reads a
> hardcoded value any more. A settings screen that saves values nothing consumes is worse than no
> settings screen, because it lies to the person using it.

§5 is the checklist of every constant that moved, and it doubles as the verification list for
future review.

---

## 2. Access control

| | Dashboard | Settings read | Settings write |
| --- | --- | --- | --- |
| Admin | everything | yes | **yes** |
| Pharmacist | trading, no money | yes | no — 403 |
| Employee | alerts only | yes | no — 403 |

**Settings are readable by every role, and that is not an oversight.** The billing screen needs
the discount caps and the antibiotic mode on every load; an invoice and a salary slip need the
pharmacy's name, address and phone. A cashier therefore reads these several times a day without
ever being able to change one. Writing changes what staff may sell and what they may discount,
which is an owner's decision.

**Dashboard tiering happens on the server.** A figure a role may not see is `null` in the
response, not hidden by CSS. A Pharmacist's dashboard carries today's sales and no profit figure
at all — the number never reaches their browser. Module 8 draws the same line for the same reason.

---

## 3. Schema

One table, created by `database/scripts/016_CreateAppSettingsTable.sql`.

### 3.1 `AppSettings`

| Column | Notes |
| --- | --- |
| `Id`, `TenantId` | Tenant-scoped by convention; `ITenantEntity` |
| `Key` | `NVARCHAR(100)`, snake case. Unique per pharmacy |
| `Value` | `NVARCHAR(1000)` **NOT NULL** — empty rather than null for a blank setting |
| `UpdatedByUserId` | Nullable, **no foreign key** |
| audit + `RowVersion` | as everywhere else |

**Key-value rather than typed columns.** Adding a setting later becomes a seed row rather than a
migration plus an entity change plus a configuration change plus a DTO change. The cost is real
and worth stating plainly: **SQL Server type-checks none of this.** `"90"` and `"ninety"` are
equally valid in that column, and the `CHECK` constraint that used to guard the antibiotic mode is
gone.

That cost is paid in exactly one place. `ISettingsService` owns every cast and every fallback, and
`UpdateSettingsCommand`'s validator refuses anything out of range before a write. No call site
parses a string.

**No foreign key on `UpdatedByUserId`, deliberately.** A setting outlives the person who set it:
`Restrict` would block removing a user who once saved this page, and `Cascade` would take the
setting with them. The id answers "who changed the discount cap", and an id that no longer
resolves to a name still answers it better than a blank does.

**Empty rather than null.** A drug licence number the pharmacy does not have is *no value*, not
*unknown*, and making the column non-nullable removes the question of which one an empty string
meant.

### 3.2 The seeded keys

Every default below is the literal that module used to hardcode. The numbers did not change; only
where they live did.

| Key | Type | Default | Where it came from | What reads it now |
| --- | --- | --- | --- | --- |
| `pharmacy_name` | text, **required** | the tenant's own name | new | invoice + salary slip headers, dashboard |
| `pharmacy_address` | text | `Address line one, Dhaka` | new (was Razor placeholder) | invoice + salary slip headers |
| `pharmacy_phone` | text, **required** | `01700-000000` | new (was Razor placeholder) | invoice + salary slip headers |
| `pharmacy_license_number` | text | *(blank)* | new | invoice header |
| `expiry_alert_window_days` | int 1–3650 | `90` | Module 6 — `StockPolicy.ExpiringSoonWindowDays` | alert summary, expiring list, stock screens, dashboard |
| `dead_stock_threshold_days` | int 1–3650 | `90` | Module 8 — `StockPolicy.DeadStockThresholdDays` | dead-stock report + its CSV export |
| `default_reorder_level` | int > 0 | `100` | Module 2 — three field initialisers | new-product form, bulk setup grid |
| `discount_cap_employee_percent` | int 0–100 | `5` | Module 5 — `BillingPolicy` | sale completion, billing limits, helper text |
| `discount_cap_pharmacist_percent` | int 0–100 | `10` | Module 5 — `BillingPolicy` | same |
| `antibiotic_prescription_mode` | `Off` / `Optional` / `Required` | copied per pharmacy | Module 7 — `Tenants` column | sale completion, sellable search, register, billing limits |

`pharmacy_name` seeds from the tenant's own name rather than "My Pharmacy": a placeholder on a shop
that already has a name would be a worse default than no default.

### 3.3 Seeding

**Existing pharmacies** are seeded by migration 016, which inserts only keys that are missing. It
is safe to re-run forever — re-running can never overwrite a value an Admin has since changed, and
it doubles as a repair for a pharmacy whose original seed was incomplete.

**New pharmacies** are seeded by `ISettingsSeeder`, called from `CreateTenantCommandHandler`
immediately after the tenant is saved. A pharmacy whose settings only appeared when somebody first
opened the settings page would spend its first day on fallbacks — working, but with a warning
logged for every read and a settings screen showing values that were not actually stored.

`SettingsSeeder` writes **raw parameterised SQL**, which is the one place in the system that
bypasses the tenant machinery. It has to: inserting through EF would put the rows in front of
`TenantEntityInterceptor`, which stamps *the current* tenant on every inserted `ITenantEntity` and
throws when none is resolved — and a platform operator creating a pharmacy has no resolved tenant.
Weakening the interceptor to accommodate this would weaken the guarantee that an Admin can only
ever write their own pharmacy's rows. Keeping the exception behind its own narrow interface means
it is one obvious method rather than a flag on the service everything else uses.

---

## 4. `ISettingsService`

```csharp
Task<string> GetStringAsync(string key, ...);
Task<int>    GetIntAsync(string key, ...);
Task<TEnum>  GetEnumAsync<TEnum>(string key, ...);
Task<IReadOnlyDictionary<string, string>> GetAllAsync(...);
Task<IReadOnlyCollection<string>> SaveAsync(values, updatedByUserId, ...);
void Invalidate();
```

### 4.1 Casting lives here

Values are stored as text. A second place that parsed one would be the beginning of two pharmacies
behaving differently for reasons nobody could find. Call sites ask for an `int` and get an `int`.

### 4.2 Caching, and its trade-off

**Registered scoped: one read per request, cached for the life of that request.**

A sale asks for the discount cap once per line and the antibiotic mode once per cart item; without
the cache a ten-item cart would be twenty identical queries about values that cannot change
mid-request.

**Nothing is cached across requests, deliberately.** A five-minute process cache would be
marginally faster and wrong in the way that matters: an Admin who raises a discount cap and then
watches a cashier get refused has no way to tell a stale cache from a bug. Reading once per
request means a saved change is in force on **the very next request** — a stronger guarantee than
the "within seconds" that was asked for, and for ten rows read from a covering index it costs
nothing worth optimising.

The staleness that remains is bounded by one request, which is the smallest window that still lets
a single sale see consistent values throughout its own processing.

> The sidebar's alert badge is separately cached for 60 seconds by `AlertBadgeProvider`. That is a
> Module 6 decision about an HTTP call, not about settings, and it was left alone.

### 4.3 Fallbacks

A missing key returns the value it had as a hardcoded constant, and **logs a warning**.

Missing keys mean a seeding gap — a pharmacy created before this migration, or a seed that failed
halfway — not a configuration choice. The warning is the point: the fallback keeps billing working
while the log says the seed needs looking at. Throwing instead would take down a till because a
row was absent.

`GetIntAsync` and `GetEnumAsync` fall back on an **unparseable** value as well as a missing one. A
row reading "ninety" is a hand-editing accident, and the ninety-day default is a better answer than
a `FormatException` surfacing from inside an alerts query on somebody's dashboard.

Asking for a key that `SettingKeys` does not declare logs an **error** and returns empty — that is
a programming mistake rather than a seeding gap, and there is no sensible value to invent.

### 4.4 Writing

`SaveAsync` writes the whole set in one `SaveChanges`, creates any row that does not exist yet,
skips keys whose value has not moved, and invalidates the cache.

Skipping unchanged keys is what keeps "who last changed the discount cap" meaningful after
somebody saves the page having edited only the phone number. Creating missing rows is what lets a
pharmacy whose seed never ran repair itself the first time an Admin presses Save.

---

## 5. The migration checklist

This is the part of the module worth reviewing. Every row is a constant that existed before
Module 10 and does not now.

| # | Constant | Was | Now reads from settings at |
| --- | --- | --- | --- |
| 1 | Expiry alert window | `StockPolicy.ExpiringSoonWindowDays = 90` | `GetAlertSummaryQueryHandler`, `GetExpiringBatchesQueryHandler`, `GetStockQueryHandler`, `GetBatchQueryHandler`, `GetProductStockQueryHandler`, `CreateBatchCommandHandler`, `UpdateBatchCommandHandler`, `AdjustBatchCommandHandler` |
| 2 | Dead-stock threshold | `StockPolicy.DeadStockThresholdDays = 90` | `GetDeadStockQueryHandler`, `ReportsController.ExportDeadStock` |
| 3 | Employee discount cap | `BillingPolicy` switch arm `5m` | `CompleteSaleCommandHandler`, `GetBillingLimitsQueryHandler` |
| 4 | Pharmacist discount cap | `BillingPolicy` switch arm `10m` | same |
| 5 | Default reorder level | `= 100` on `Product`, `ProductFormInput`, `BulkSetupRowInput` | `Medicines/Create`, `OtherItems/Create`, `Medicines/BulkSetup` |
| 6 | Antibiotic mode | `Tenants.AntibioticPrescriptionMode` column | `CompleteSaleCommandHandler`, `GetSellableProductsQueryHandler`, `GetBillingLimitsQueryHandler`, `AntibioticQueries`, `AntibioticsController` |
| 7 | Pharmacy name / address / phone | Razor literals in `Sales/Detail.cshtml` and `Salary/Entries/Slip.cshtml` | both page models, via `GET /api/settings` |

### 5.1 What the policy classes kept

`StockPolicy` and `BillingPolicy` still exist and are still pure functions. What changed is that
the numbers arrive as arguments:

```csharp
StockPolicy.CoerceExpiryWindow(int? days, int configuredWindowDays)
BillingPolicy.MaxDiscountPercentFor(UserRole role, DiscountCaps caps)
```

Keeping them pure is what makes the server-side check at sale completion, the refusal message the
cashier reads and the helper text on the billing screen provably the same rule — and what lets a
unit test exercise it without a database.

**`StockPolicy` also kept three constants on purpose**: the amber and red thresholds on an expiry
row, and the point at which adding stock to a nearly-expired batch asks for confirmation. Those
are judgements about how to *present* a risk rather than how much risk a pharmacy will carry, and
the brief is explicit that settings outside its table should not be invented.

### 5.2 Selectable values versus the configured value

The expiring-soon page and the dead-stock report each offer a dropdown of standard periods —
30, 60, 90, 180 — and those lists survive. What changed is that **the pharmacy's own value is
always added to the list** and is what an unrecognised request falls back to:

```csharp
SelectableExpiryWindows(configured)  // {30,60,90,180} ∪ {configured}, ordered
```

A pharmacy that set 45 days and then found the dropdown could not show 45 would have a settings
screen its own alert page disagreed with.

### 5.3 Verifying it

`acceptance_settings.py` changes each setting and asserts the **behaviour** moved — not that the
value came back. The dead giveaway for a regression here would be a test that only round-trips a
value.

---

## 6. API

| Method | Route | Access |
| --- | --- | --- |
| GET | `/api/settings` | any tenant user |
| PUT | `/api/settings` | **Admin** |
| GET | `/api/settings/antibiotic-mode` | any tenant user |
| PUT | `/api/settings/antibiotic-mode` | **Admin** |
| GET | `/api/dashboard` | any tenant user, tiered |

`GET /api/settings` returns a **typed object**, not a key-value list. The storage is key-value; a
client that had to know `expiry_alert_window_days` holds an integer would be a second place the
types live, and the first place to drift.

`PUT /api/settings` takes a **partial or full set** — every field is nullable and null means
"leave it alone". An empty string is a value being *set*, which is what makes "clear the pharmacy
name" expressible and refusable. **Nothing is applied unless everything validates**: a body
carrying a good pharmacy name and a discount cap of 150 changes neither.

The single-setting antibiotic endpoints survive the move because Module 7's own screen calls them,
and because "change one setting" is a smaller thing to ask for than "save the whole page". Both
paths write through `ISettingsService` into the same table.

### 6.1 Validation

- percentages 0–100 (zero allowed — see below)
- day thresholds 1–3650
- `default_reorder_level` > 0
- `pharmacy_name` and `pharmacy_phone` non-empty
- `antibiotic_prescription_mode` one of the three

**A discount cap of zero is allowed**, unlike a day threshold of zero. A pharmacy where only the
owner discounts is making a coherent choice; refusing it would force them to set 1% and hope. A
window of zero, by contrast, silently reports nothing, which looks exactly like the feature being
broken.

There is deliberately **no rule that a Pharmacist's cap must exceed an Employee's**. It is the
sensible arrangement and not the only defensible one — a pharmacy whose pharmacist is a locum and
whose employee is the owner's family would set them the other way round, and refusing that would
be this system inventing a policy.

---

## 7. The dashboard

`GET /api/dashboard` assembles the home screen from the query services the other modules own:

| Card | Source | Roles |
| --- | --- | --- |
| Expiring / Expired / Low / Out of stock | Module 6 `IAlertQueries` | all |
| Today's sales | Module 8 `IReportQueries.GetDailySalesReportAsync` | Admin, Pharmacist |
| Today's profit | same call, **null for a Pharmacist** | Admin |
| Antibiotics this month | Module 7 `IAntibioticQueries` | Admin, Pharmacist |
| Supplier dues | Module 8's dues report → Module 4 `ISupplierBalanceQueries` | Admin |
| Unpaid salary | Module 9 `ISalaryQueries.GetSummaryAsync` | Admin |

**Nothing here computes an aggregate of its own.** That is what makes each card provably equal to
the screen it links to — they are the same query, not two implementations of the same idea. A
dashboard that disagreed with the report it linked to would destroy trust in both.

It also sidesteps the aggregate-over-subquery failure that has bitten Modules 6, 7, 8 and 9: this
handler issues several independent queries and combines them in C#, so there is no projection
whose columns are correlated aggregates.

**Cost**: four to six round trips depending on role, none per-card and none in a loop. The one
deliberate over-fetch is today's trading — `GetDailySalesReportAsync` returns the day's invoice
rows and hourly breakdown when the card needs two numbers off the top. A narrower query would be
cheaper and would be a second definition of "today's takings", which is the drift this handler
exists to avoid.

### 7.1 Supplier dues and the in-credit case

The card shows the **net** outstanding, straight off the dues report, plus a secondary line where
any supplier holds money of the pharmacy's:

> 2 suppliers in credit, holding ৳155.00

Shown beside the net rather than folded into it, because "you owe ৳6,845" reads differently when
৳155 of that is already sitting with a distributor who owes it back.

**"In credit", never "overpaid"** — Module 4's word, because returning goods after paying produces
the same state without anybody overpaying. `SupplierDuesReportDto.TotalCreditHeld` was added for
this card and derives from the same balance figures as every other total on that report.

### 7.2 The user's name

The dashboard response carries the **pharmacy's** name but not the **user's**. The API's token
carries a subject, a tenant and a role and no name claim, so returning one would mean a database
round trip for a greeting. The web app renders it from its own sign-in cookie.

---

## 8. Key decisions

**Key-value over dedicated columns.** §3.1. Cheap new settings, at the cost of no database type
checking — paid for by one casting seam and one validator.

**The antibiotic mode moved off `Tenants`.** Migration 012 put it there only because no settings
table existed, and said in as many words that a settings module should move it. Two places to
store one value is exactly the drift worth avoiding: a settings screen writing one column while
billing reads another is a bug nobody finds until an inspection. The trade is real — the column
had a `CHECK` constraint and a key-value row cannot — and it is the same trade every other setting
in the table already makes.

Existing values are **copied, not defaulted**. A pharmacy running under Required keeps running
under Required across the upgrade; silently relaxing a regulatory setting during a migration would
be the worst thing that script could do. The column is dropped only after every pharmacy has the
row, guarded by a `NOT EXISTS`.

**The supplier dues card reuses Module 4's balance service** by going through Module 8's report
rather than around it. §7.1.

**Read by all, written by Admin.** §2.

**One save for the whole settings page.** Per-field saves would mean nine forms, nine success
banners and nine chances to leave the page half-changed. The API applies the whole set or none of
it, and the screen is shaped to match.

---

## 9. Out of scope

- **Per-user dashboard customisation** — which cards, what layout.
- **Settings change history or an audit log.** `UpdatedByUserId` answers "who last", which is the
  question actually asked; a full history is a second table for a screen nobody has requested.
- **Currency symbol configuration.** ৳ stays hardcoded in display formatting.
- **Any setting not in the table above.** Three `StockPolicy` constants stayed constants for this
  reason — see §5.1.
- **Platform-level settings** applying to every tenant at once.
- **Dark mode.**

---

## 10. Files

**Domain** — `Entities/AppSetting.cs`; `Entities/Tenant.cs` (mode column removed);
`Entities/Product.cs` (reorder-level initialiser removed)

**Application** — `Common/Settings/SettingKeys.cs`, `Common/DTOs/SettingsDtos.cs`,
`Common/DTOs/DashboardDtos.cs`, `Common/Billing/DiscountCaps.cs`,
`Interfaces/ISettingsService.cs`, `Interfaces/ISettingsSeeder.cs`,
`Features/Settings/**`, `Features/Dashboard/**`;
`Common/Billing/BillingPolicy.cs` and `Common/Stock/StockPolicy.cs` (numbers become arguments)

**Persistence** — `Configurations/AppSettingConfiguration.cs`, `Services/SettingsService.cs`,
`Services/SettingsSeeder.cs`; `Services/TenantSettings.cs` **removed**

**API** — `Controllers/SettingsController.cs`, `Controllers/DashboardController.cs`

**Web** — `Api/SettingsContracts.cs`, `Pages/Settings/Index.cshtml(.cs)`,
`Pages/Index.cshtml(.cs)`, `wwwroot/js/settings-page.js`

**Database** — `database/scripts/016_CreateAppSettingsTable.sql`

**Tests** — `tests/PMS.SchemaTests/Settings/SettingsSchemaTests.cs`;
`BillingPolicyTests`, `AlertPolicyTests` and `AntibioticModeTests` updated for the moved values

See [frontend/10-dashboard-and-settings.md](frontend/10-dashboard-and-settings.md) for the screens.
