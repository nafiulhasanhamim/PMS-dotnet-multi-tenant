# 08 — Reports & Profit (frontend)

Eight Razor pages, all Admin-only, all read-only. No new JavaScript file.

Server logic and the arithmetic: `docs/08-reports-and-profit.md`.

---

## 1. Shape

```
/reports                          landing — one card per report
/reports/daily-sales              a day, with an hourly bar chart
/reports/monthly-sales            a month, with bar + line charts
/reports/top-selling              paginated, sortable, filterable
/reports/sales-by-product-type    two rows and a chart
/reports/dead-stock               paginated, threshold picker
/reports/sales-per-user           one row per cashier
/reports/stock-valuation          paginated, snapshot
```

Every page: filters at the top, headline figures, chart where one helps, then the table the
figures came from. Export CSV and Print in the header.

---

## 2. Charts are server-rendered inline SVG

**No charting library, and that is a decision rather than an omission.**

Nothing in this stack draws charts. Adding Chart.js for these pages would mean a new dependency,
a new bundle, and a blank rectangle for anyone whose script fails or is blocked.

Server-rendered SVG needs none of that:

- it is in the HTML the page already sent — no second request, no flash of empty box;
- **it prints**, which matters, because these are reports people print and file;
- it scales without blurring;
- it picks up the theme through CSS variables, including the print overrides that flatten
  everything to grey.

A future need for zooming, panning or live updating would justify revisiting it. Reading last
month's sales does not.

### 2.1 Accessibility

The SVG is `aria-hidden` and always accompanied by the table it summarises. It is an aid to
seeing a *shape*, never the only route to a number, so a reader using a screen reader loses
nothing by skipping it. Hover detail comes from a native SVG `<title>` — no script, works
everywhere.

### 2.2 Two partials

`_BarChart` stretches non-uniformly (`preserveAspectRatio="none"`) because a bar's height is its
meaning and its width is not. `_LineChart` keeps its aspect ratio, because a line's *slope* is its
meaning and stretching it unevenly would misrepresent it.

Labels sit in a flex row outside the SVG, so the non-uniform stretch cannot distort the text. The
axis rule uses `vector-effect: non-scaling-stroke` for the same reason.

`ChartModel.Scale` is the largest **magnitude**, not the largest value — a profit series can be
negative, and a bar scaled against a negative maximum would point the wrong way.

---

## 3. Money is formatted in exactly one place

`ViewModels/Money.cs`. The API sends everything unrounded because it aggregates unrounded; the
last step before a person reads a number is the right place to round it, and that step is here.

- `Format` — `N2`, invariant culture. The server's culture is not the reader's, and `1.234,56` on
  one machine and `1,234.56` on another is a figure two people will argue about.
- `Signed` — uses a real minus sign (`−`), which is wider than a hyphen and harder to miss on a
  profit column where missing it inverts the row's meaning.
- `PerUnit` — four decimals. A per-base-unit cost from a bulk pack is usually fractional, and two
  places would not reproduce the total beside it, which reads as an arithmetic error.
- `Tone` — returns a CSS class. **Colour is never the only signal**: `Signed` has already put a
  minus sign there, so the page survives monochrome printing and colour blindness.

Money columns are `text-end` with `font-variant-numeric: tabular-nums`, so decimal points line up
into a column the eye can run down.

---

## 4. The landing page makes no API call

Eight summary requests would make it the slowest page in the application, and any one of them
failing would take the whole page with it. Each card describes what its report answers — "dead
stock" and "stock valuation" are not self-evident to somebody opening the section for the first
time — and links to it.

Below the cards, a short note states the four rules the figures follow: cost from the batch,
revenue after discount, cancelled sales excluded rather than zeroed, returns counted in the period
they happened. Somebody comparing two reports needs to know those before they start.

### 4.1 The supplier dues card is visibly disabled

Module 4 does not exist, so the report cannot be built — and it is **not** built as a page
returning zeros. A money report that says "0.00 owed" is worse than an absent one.

The card renders dashed and greyed, says "Not available yet", and names what it needs. A gap
somebody notices and asks about beats one they never learn exists.

---

## 5. Page-by-page decisions

### 5.1 Daily sales

Hours arrive from the API as **UTC buckets** and are shifted into the display zone here, using the
offset for the report's own date rather than for now. That is the same division of labour every
timestamp in the app uses: the server stores and filters in UTC, and the one layer that knows a
person is looking at a screen converts. The chart caption names the zone.

All 24 hours are drawn, including empty ones — a gap where the shop was quiet must not read as
missing data.

The returns line sits under the table rather than in the profit column, because the returns
processed today may reverse sales from other days. See the module doc §3.

### 5.2 Monthly sales

**The warning beside net profit is the most important thing on the page.** While operating
expenses are zero, net profit equals gross profit, and an owner reading "net profit" is entitled
to assume salaries are in it.

The notice is driven by the figure being zero rather than hard-coded, so it disappears on its own
the day Module 9 records a salary. The exported CSV carries the same caveat as a trailing line,
because a file that has been emailed on is read without the screen it came from.

Every day of the month is listed, quiet ones greyed with `.row-quiet` rather than dropped, and
each links through to that day's report.

The month picker submits on change; a `<noscript>` submit button keeps it usable without script,
and the previous/next links work regardless.

### 5.3 Top selling

The chart shows the top ten **only on page one**. A bar chart headed "top selling" drawn from rows
26 to 50 would be actively misleading, so on later pages it is simply absent.

The three sort orders answer different questions — what to reorder, what earns most, what to
promote — and the chart's series follows whichever is selected.

### 5.4 Dead stock

Never-sold rows sort first **and** carry a warning tint, because on page two the sort order no
longer says anything. "Never sold" is written out in the Last sold column rather than left blank:
an empty cell in a date column reads as missing data, where here it is the finding.

The threshold the page echoes back is the one the **server applied**, not the one asked for. A
hand-edited query string is coerced, and a filter control showing the rejected value would
describe the table wrongly.

The headline figures say "across the whole report, not this page" — the capital-tied-up number is
what motivates somebody to act, so it has to describe the report rather than whatever happens to
be on screen.

### 5.5 Sales per user

The page carries a "Reading this fairly" note, and it is there deliberately: a table that ranks
colleagues invites a conclusion the numbers do not support on their own. A cashier whose account
was removed shows as "(unknown user)" with a placeholder role — dropping the row would drop their
sales out of the total.

### 5.6 Stock valuation

Expired stock is shown in its own column and excluded from the value, with the reason spelled out
in the notes beneath. Average cost renders to four decimals for the reason in §3.

No date picker, because there is no stock history to look back through. The notes say so rather
than leaving somebody hunting for a control that is not there.

---

## 6. Exports

Each page's Export button posts to its own `OnGetExportAsync`, passing **the filters currently on
screen** — so a downloaded file covers exactly the table above it, not a default range. The query
builders (`PmsApiClient.RangeQuery`, `TopSellingQuery`, `DeadStockQuery`) are shared between the
page request and the export request, which is what keeps the two in step.

`ExportReportAsync` uses `HttpCompletionOption.ResponseHeadersRead` and returns the live stream.
The response is deliberately **not** disposed there — ASP.NET Core disposes it when the
`FileStreamResult` finishes writing. Buffering here would undo the streaming the API went to the
trouble of doing.

A failed download returns to the report with a warning banner rather than to an error page: the
person still wants the report, they just did not get the file.

---

## 7. Shared page model

`ReportRangePageModel` — four pages bind the same two dates, default them the same way, and pass
the same filters to their export. Writing that four times is how one of them ends up defaulting to
a different window, and two reports over "the last month" that disagree are worse than one report.

Dates are resolved on **both** sides. The server is the authority on what it queried, but the page
has to put something in its two date inputs, and leaving them blank while showing thirty days of
data would misdescribe the table. Both sides use `DateTime.UtcNow`, the same 30-day window, and
the same inclusive-both-ends rule — using the display zone here would put a different date in the
input than the one the server filtered on.

---

## 8. Print

Every report page prints. Filters and the landing cards are hidden, headline cards flatten to
bordered boxes, charts drop to grey fills, and negative figures lose their red — which costs
nothing, because `Money.Signed` already put a minus sign there.

Each page carries a `pms-print-only` header naming the pharmacy and the period, so a printed sheet
is self-describing once it has left the screen it came from.

---

## 9. Navigation

One entry, `Reports`, Admin-only, between the antibiotic register and Users. A new `chart` icon —
bars rising left to right, the shape universally read as "reports", and distinguishable from
Stock's stacked layers at 18px because these bars do not touch.

Visibility is a convenience. `[Authorize(Policy = WebPolicies.TenantAdmin)]` on every page model
is what actually stops anybody, and `web_smoke_reports.py` asserts a redirect to `/denied` for
both other roles on all eight pages.

---

## 10. Files

**Pages** — `Pages/Reports/{Index, DailySales, MonthlySales, TopSelling, ByProductType,
DeadStock, SalesPerUser, StockValuation}.cshtml(.cs)`, `Pages/Reports/ReportRangePageModel.cs`

**Shared** — `Pages/Shared/_BarChart.cshtml`, `Pages/Shared/_LineChart.cshtml`

**View models** — `ViewModels/Money.cs`, `ViewModels/ChartModels.cs`

**Api** — `Api/ReportContracts.cs`, report methods on `Api/PmsApiClient.cs`

**Navigation** — `Navigation/NavRegistry.cs`, `Pages/Shared/_NavIcon.cshtml`

**Styles** — `wwwroot/css/site.css`, Module 8 block plus its print overrides
