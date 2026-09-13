# Frontend 06 — Expiry & Low-Stock Alerts

Four read-only screens plus the dashboard panels. No forms, no writes — every row links out to a
screen that already exists.

Query rules and the two decisions that carry the module are in
[`docs/06-expiry-and-low-stock-alerts.md`](../06-expiry-and-low-stock-alerts.md).

---

## 1. Page inventory

| Route | Page | Purpose |
|---|---|---|
| `/` | `Pages/Index` | Dashboard. Four alert cards replace the Module 1 placeholder. |
| `/alerts` | `Pages/Alerts/Index` | The hub. The same four cards, larger, with a line each. |
| `/alerts/expiring` | `Pages/Alerts/Expiring` | Batches expiring inside the selected window. |
| `/alerts/expired` | `Pages/Alerts/Expired` | Batches past their date, still on the shelf. |
| `/alerts/low-stock` | `Pages/Alerts/LowStock` | Products at or below their reorder level. |

All four carry `WebPolicies.TenantUser`. Every role sees everything — see the backend doc for why.

### Navigation

One entry, between *Stock* and *Users*:

```csharp
new NavItem("Alerts", "/Alerts/Index", "alert", MatchPrefix: "/alerts",
    BadgeKey: NavBadges.Alerts),
```

`NavItem` gained an optional `BadgeKey` for this. `NavRegistry` is static and a count is
per-request, so the item names a key and the layout looks it up in a dictionary built once per
page. An entry whose key is absent renders no badge, which lets a later module add a nav item
before it has anything to count.

---

## 2. The nav count badge

**What it counts: expired batches + out-of-stock products.** Not all four categories.

Expiring stock and merely-low stock are things to plan around this week. Expired stock is money
already lost sitting on a shelf, and an out-of-stock product is a sale being turned away right
now. Those two earn a number next to the word; folding in the other two would inflate the badge
on a pharmacy that is actually fine, and a badge that is always lit is a badge nobody reads.

**Zero renders nothing.** A nav item wearing a permanent "0" is noise, and reassurance nobody
asked for is still something to read every time.

**It is cached for 60 seconds per pharmacy.** The layout renders on *every* page in the app, so
without a cache this would add an API round trip to every stock list, every login redirect, every
print view. A badge is a glance: a number up to a minute old is worth far more than a whole
application that loads more slowly to keep it exact. The alert pages themselves always fetch live.

The key includes the tenant. A cache keyed on nothing would show one pharmacy another's urgent
count for up to a minute — both a leak and a lie.

**Failure is silent.** If the API is unreachable the badge does not appear and the page renders
normally. A layout that threw because it could not draw a decoration would take down every screen
in the application. The failure is logged and the *absence* is cached for the same short window,
so a pharmacy whose API is briefly down does not have every page retry the same failing call.

The badge is announced, not merely coloured: `aria-label="3 need attention"`, so a screen reader
reads it rather than skipping a red circle.

---

## 3. Dashboard cards

Four cards in a responsive grid — four across, two on a tablet, one on a phone.

| Card | Tone when non-zero | Links to |
|---|---|---|
| Expiring soon | Amber | `/alerts/expiring` |
| Expired | Red | `/alerts/expired` |
| Low stock | Amber | `/alerts/low-stock` |
| Out of stock | Red | `/alerts/low-stock` |

The Expiring card carries a preview line when there is something to preview:

> Nearest: Napa 500 (B-101) — 12 days

Both the dashboard and the hub build their cards from `AlertCardModel.From(summary)`, so the two
screens cannot drift in wording or order. The hub renders the same model with an extra class and
a description beneath.

### Zero looks calm, and that is the rule the colours depend on

**A card showing zero is plain** — no tint, a muted number, no border colour. A pharmacy with
nothing expiring and nothing running out should not open its dashboard to four warning-coloured
boxes.

Colour that is always on carries no information. The first thing a person learns from a permanently
red dashboard is to stop looking at it, and by the time something is genuinely wrong the signal has
already been spent. When every count is zero the page says so once, quietly, in a line of helper
text, instead of four reassuring ticks.

### A failed alert lookup does not take the dashboard down

`Pages/Index` renders the profile panel regardless. If the summary call fails, the cards are
replaced by a single warning line — "Stock alerts could not be loaded just now. Everything else on
this page is current." A pharmacy should not lose its home screen because a count could not be
fetched.

---

## 4. Row colour-coding

Thresholds come from the server. `StockPolicy.SeverityFor` decides, the API returns a `Severity`
on every row, and `AlertPresentation.RowCss` maps it to a class. The page never does the
arithmetic, which is what stops a row being coloured differently from the count that led somebody
to it.

| Days remaining | Severity | Row |
|---|---|---|
| ≤ 7 (and anything negative) | `Critical` | Red |
| 8 – 30 | `Warning` | Amber |
| 31 and beyond | `Normal` | Plain |

Whole rows are tinted rather than a dot in one cell: severity is a property of the row, and
somebody scanning the list reads down it. **Colour is never the only signal** — the days column
says the same thing in words, "12 days" or "40 days overdue".

The expired page has no gradation. Every row is red, because the question there is not how bad a
row is but how long it has been ignored.

---

## 5. The three lists

### Expiring (`/alerts/expiring`)

Window selector offering 30 / 60 / 90 / 180 days, defaulting to the configured window. The
heading reads the window back from the API response rather than from the query string, so it
cannot claim a window the list was not built with.

Columns: Product (brand with generic beneath), Batch, Expiry date, Days remaining, Quantity.
The product cell is one link covering both lines, to the product's stock detail page — which is
where the two things a person can do about an expiring batch live.

### Expired (`/alerts/expired`)

Opens with the explanatory line the brief specifies, because the list otherwise reads as a
warning about something that might happen:

> These batches have passed their expiry date and are blocked from sale. Consider a purchase
> return or a stock adjustment to remove them from inventory.

"Days overdue" replaces "Days remaining". Actions appear only for an Admin or a Pharmacist — an
Employee still sees the list, which is the point, but is not offered a button that leads to an
access-denied page.

### Low stock (`/alerts/low-stock`)

Filters on product type and status. Rows are amber for Low and red for Out of stock, with a badge
that says which in words.

**The row that would otherwise look like a bug.** A product can be out of stock while its shelf is
visibly full, because expired batches do not count. The row says so:

> 0 pieces — plus 200 pieces expired, which cannot be sold

Without that line the alert looks wrong to the person standing in front of the shelf, and an alert
somebody argues with is an alert they stop trusting.

---

## 6. Empty states are written as good news

An empty expiry list is not a failed search. It is the pharmacy being in order.

| Page | Empty state |
|---|---|
| Expiring | "Nothing expiring in the next 90 days" / "Every batch with stock left is further out than that." |
| Expired | "No expired stock" / "Nothing on the shelves has passed its expiry date." |
| Low stock | "All products are adequately stocked" / "Every active product is above its reorder level." |

The same grey "no results found" used for a mistyped search would teach people that the page is
broken rather than that the shelves are fine — and this is a screen somebody opens *hoping* to see
nothing. The low-stock page keeps a neutral "nothing matches those filters" for the case where a
filter is actually narrowing, which is a different statement.

---

## 7. The contextual action on the expired page

The brief asks for "Return to supplier" where the batch came from a recorded purchase and "Adjust
stock" where it did not.

**How the choice is determined:** `AlertPresentation.ActionFor(batch)` reads
`ExpiringBatch.CameFromARecordedPurchase`, which is `SupplierId is not null`. Deliberately the
foreign key and not the free-text `SupplierNameText`: a batch entered by hand with "local
supplier" scribbled on it has nobody to return anything to, and the only honest thing to offer it
is a write-off.

**Both branches currently render "Adjust stock", and that is a gap rather than a decision.** The
purchase-return screen belongs to Module 4, which is not built — there is no `Supplier` entity, so
no batch can carry a real purchase behind its supplier id either. Rendering a link to a page that
does not exist would throw at render time.

What is there instead: the branch is written out and named, and where a supplier *is* recorded the
row shows who it was, so the person can at least see who to ring. Wiring the real destination is
one line in `Expired.cshtml` once Module 4 lands.

---

## 8. New shared components

| Component | Change |
|---|---|
| `_AlertCard.cshtml` | New. One card, rendered small on the dashboard and large on the hub. |
| `NavItem` | Optional `BadgeKey`; `NavBadges` holds the keys as constants so a typo is a build error. |
| `SidebarModel` | New. Nav items plus the per-request badge counts. `_SidebarNav` takes it instead of a bare list; the platform layout uses `SidebarModel.Of(...)` with none. |
| `AlertBadgeProvider` | New. Cached, tenant-keyed, silent on failure. |
| `AlertCardModel`, `AlertPresentation` | New view models. Card wording and row CSS. |
| `site.css` | `pms-sidebar__badge`, `pms-alert-grid`, `pms-alert-hub`, `pms-alert-card*`, `pms-row-warning`, `pms-row-danger`, `pms-cell-link`. |

`pms-row-warning` and `pms-row-danger` are general row tints rather than alert-specific ones —
Module 7's register will want the same thing.

---

## 9. Out of scope

- Any write action. Every row links to a screen that already owns the change.
- A settings screen for the window or the colour thresholds.
- Dismissing or snoozing an alert — there is nothing to store it in, and an alert somebody can
  silence is one that stops being true without anybody noticing.
- Notifications of any kind outside the app.
