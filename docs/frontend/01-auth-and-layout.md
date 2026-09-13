# Frontend 01 — Authentication and layout

The first frontend module. It builds the Razor Pages UI for tenant foundation and
authentication, and in doing so sets the layout, design system, API client pattern and auth
mechanism that **every later frontend module follows**. Read the design system section before
building any new page.

> **Status: working end to end.** Verified against SQL Express through the running UI: both
> logins, tenant creation including a duplicate-domain rejection, admin provisioning on both
> paths, staff creation, deactivation, role-based denial, per-login tenant context, and the
> 401 session-expiry path.

---

## 1. Overview

Two areas, two audiences, one app.

| Area | Who | Layout | Accent |
|---|---|---|---|
| Pharmacy | Admin, Pharmacist, Employee | sidebar + top bar naming the pharmacy | blue |
| Platform administration | platform operators | its own sidebar, labelled, no pharmacy | purple |

`PMS.Web` **references no other project in the solution.** It declares the API's wire contract
locally in `Api/ApiContracts.cs` and talks HTTP. That costs a few duplicated records and buys
the only real proof that the API is usable by a client that cannot cheat — which is exactly
the position a mobile or desktop client will be in. If this app could reach into
`PMS.Application`, the API would no longer be proven by anything.

---

## 2. Auth architecture

### The shape

The JWT never reaches the browser. Not in `localStorage`, not in `sessionStorage`, not
rendered into a page, not in a readable cookie.

```
┌─────────┐  1. POST credentials              ┌───────────┐  2. POST /api/auth/login  ┌─────┐
│ browser │ ────────────────────────────────▶ │ PMS.Web   │ ────────────────────────▶ │ API │
│         │                                   │ (server)  │ ◀──────────────────────── │     │
│         │ ◀──────────────────────────────── │           │  3. { token, expiresAt }  └─────┘
└─────────┘  4. Set-Cookie: pms.tenant=…      └───────────┘
             HttpOnly; Secure; SameSite=Strict
                  ▲  the token is INSIDE this cookie, encrypted by data protection

Every later request:
  browser sends pms.tenant  →  cookie handler decrypts the ticket  →  ITokenAccessor reads
  the pms:api_token claim   →  ApiTokenHandler sets Authorization: Bearer …  →  API call
```

The token is stored as a claim in the ASP.NET Core authentication ticket, which data
protection encrypts and signs. So the cookie's *bytes* travel to the browser, but its contents
are opaque there and unreadable by JavaScript. An XSS bug on any page cannot walk off with a
pharmacy's session.

The cookie's `ExpiresUtc` is pinned to the token's own `expiresAtUtc`, and `SlidingExpiration`
is off. The two cannot drift apart: a cookie outliving its token would leave someone
apparently signed in while every action failed.

### Two cookie schemes, not one

`Auth/WebAuth.cs` defines both:

| Scheme | Cookie | Claims |
|---|---|---|
| `PmsTenant` | `pms.tenant` | `NameIdentifier`, `Name`, `Email`, `Role`, `pms:user_id`, `pms:tenant_id`, `pms:tenant_name`, `pms:tenant_domain`, `pms:api_token` |
| `PmsPlatform` | `pms.platform` | `NameIdentifier`, `Name`, `Email`, `pms:user_id`, `pms:platform_admin=true`, `pms:api_token` |

Cookies are `HttpOnly; SameSite=Strict; Secure`. One deliberate exception: **Development**
uses `SameAsRequest`, and `UseHttpsRedirection` is skipped there too. The reason for both is
the same — the ASP.NET development certificate covers `localhost` only, not
`popular-pharmacy.localhost`, so forcing https locally sends every pharmacy address to a
certificate warning. Requiring a hand-made wildcard certificate just to run the app is a poor
trade. Every other environment keeps `Always` plus redirection and HSTS.

If you do want https locally, one command creates a certificate that covers the subdomains:

```powershell
New-SelfSignedCertificate -DnsName "localhost","*.localhost" `
    -CertStoreLocation "cert:\LocalMachine\My" -FriendlyName "PMS dev wildcard"
# then move it into Trusted Root Certification Authorities
```

A side effect of address-based tenancy worth knowing: cookies are scoped per host, so a
session at `popular-pharmacy.pms.example.com` is never sent to `green-life.pms.example.com`.
Two pharmacies open in two tabs stay independent, for free.

A platform session writes **no tenant claims at all** — there is no pharmacy to name, and
writing one would be a lie a page could act on.

Why two schemes rather than one scheme with two policies: with a single scheme, a policy is
the only thing standing between a platform session and a tenant page, and a policy is one
edit away from being wrong. With two, the tenant scheme does not *authenticate* a platform
cookie — the principal never materialises — so a platform session cannot satisfy a tenant
page even if its policy were written badly.

A practical consequence worth knowing: signing in to one area does not sign you out of the
other. Both cookies can exist simultaneously and each area sees only its own.

### Policies

Declared in `Program.cs`. Each names the scheme it accepts, which is what makes the
separation real rather than cosmetic:

| Policy | Scheme | Requires |
|---|---|---|
| `TenantUserPolicy` | `PmsTenant` | authenticated + `pms:tenant_id` |
| `TenantAdminPolicy` | `PmsTenant` | authenticated + `pms:tenant_id` + role `Admin` |
| `PlatformAdminPolicy` | `PmsPlatform` | authenticated + `pms:platform_admin=true` |

**Secure by default** comes from a fallback policy, not `AuthorizeFolder("/")`:

```csharp
.SetFallbackPolicy(new AuthorizationPolicyBuilder(WebSchemes.Tenant, WebSchemes.Platform)
    .RequireAuthenticatedUser()
    .Build())
```

A fallback applies only to endpoints with no authorization metadata of their own, so a page
added later and forgotten still requires a signed-in user. A blanket `AuthorizeFolder("/")`
was tried first and is wrong here: it stacks a second, *tenant-scheme* requirement onto the
platform pages and locks platform operators out of their own area.

Anonymous pages are listed explicitly: `/login`, `/platform/login`, `/denied`, `/error`.

---

## 3. API client pattern

Follow this in later modules rather than inventing a new one.

### The typed client

`Api/PmsApiClient.cs`, registered with `AddHttpClient<PmsApiClient>` and a base address from
`Api:BaseUrl`. One class holds every call, so no page constructs a URL or a header.

### The JWT-injecting handler

```csharp
builder.Services
    .AddHttpClient<PmsApiClient>(…)
    .AddHttpMessageHandler<ApiTokenHandler>();
```

`ApiTokenHandler` reads `ITokenAccessor.Token` and sets the bearer header on **every** request
on this client. A handler rather than per-call code, so a call added later cannot forget one.
The two login endpoints need no special case: no token is stored yet when they run, so none is
attached — which also means a stray token can never be sent to them.

### Nothing throws for an HTTP failure

Every method returns `ApiResult<T>`. A rejected login or a duplicate domain is an expected
answer that belongs on a page, not an exception to be caught. Transport failures
(`HttpRequestException`, timeouts) are converted to `ApiProblem` too, logged server-side, and
surfaced as "the service is temporarily unavailable".

### Centralized 401 / 403 / 5xx handling

`ApiProblem.Failure` classifies a failure once, and `PmsPageModel.HandleFailureAsync` acts on
it once:

| Classification | Trigger | What happens |
|---|---|---|
| `SessionExpired` | `401` | sign out of this area's scheme, redirect to its login with the session-ended notice |
| `TenantUnavailable` | `403` with code `Tenant.Inactive` / `Tenant.NotFound` | sign out — the cookie names a pharmacy that can no longer be used — and explain on `/denied` |
| `Forbidden` | any other `403` | redirect to `/denied`. **No sign-out** — the session is still valid, and logging someone out over a permissions slip is its own bug |
| `ServerError` | `5xx`, unreachable, timeout | redirect to `/error`; the detail was already logged, never rendered |
| `Rejected` | `400`, `404`, `409` | stays on the page — see below |

That 403 split is the part to keep. A 403 can mean "your role is not enough", which leaves the
session perfectly usable, or "this pharmacy is suspended", which makes the cookie misleading.
The API distinguishes them with a `code`; treating them alike would either sign people out
over a mis-click or keep a suspended pharmacy's session alive.

### Surfacing the API's validation

The API is the authoritative validator; client-side attributes are a convenience layer only.
`PmsPageModel.ApplyProblem` binds the API's answer onto the form:

- a validation `400` carries an `errors` dictionary → each message goes to
  `ModelState["Input.<Field>"]` and renders under that input;
- a `409` carries no `errors` (it is a conflict, not a validation failure) → a page names the
  input it belongs to via `ConflictField`, so a duplicate domain appears under **Domain name**
  rather than in a page banner;
- anything else becomes the form-level banner.

```csharp
// Pages/Platform/Tenants/Create.cshtml.cs
protected override string? ConflictField => nameof(InputModel.DomainName);
```

### Strongly-typed DTOs

`Api/ApiContracts.cs` only. No `JsonDocument`, no `dynamic`. The enums must match the server's
**numeric** values — `UserRole`, `TenantStatus` and `ProvisioningOutcome` are mirrored, and
that mirroring is the price of the zero-reference rule.

---

## 4. Design system

**This section is the canonical reference for every future frontend module.** The tokens live
in `wwwroot/css/site.css`; use them rather than literal values.

### Character

A business tool used all day by people who are not thinking about the software — often on a
modest laptop, sometimes in a hurry at a busy counter. Closer to well-made accounting software
than to a consumer app. Clarity, speed and unambiguous labels over visual flourish.

### Framework

**Bootstrap 5.1** (already vendored in `wwwroot/lib`). Do not add a second CSS framework.
**No jQuery** — Bootstrap 5's bundle needs none, and `wwwroot/js/site.js` is plain ES5-ish
vanilla JS. The template's jQuery and unobtrusive-validation scripts were removed.

### Layout

- **Persistent left sidebar**, 232px, collapsing to an overlay below 1024px.
- **Top bar** leading with the pharmacy name and domain, then the user's name and role, then
  sign out. The pharmacy name's prominence is deliberate: someone with access to two
  pharmacies has no other way to tell which one they are about to change.
- **Page header** on every page: title left, primary action right (`.pms-page-header`).
- Responsive down to tablet (1024px). Full phone optimisation is out of scope for v1.

Three layouts: `_Layout` (pharmacy), `_PlatformLayout` (platform, purple, labelled),
`_AuthLayout` (centred card, no chrome, accent set by `ViewData["Accent"]`).

### Typography

| Token | Size | Use |
|---|---|---|
| `--pms-font-h1` | 24px / weight 500 | page title |
| `--pms-font-h2` | 18px / weight 500 | section heading |
| `--pms-font-lg` | 16px | emphasis, card values |
| `--pms-font-base` | 15px | body — the default |
| `--pms-font-sm` | 13px | helper text, table headers, badges — **the floor** |

Medium (500) headings, never 700: the size difference already carries the hierarchy, and heavy
weights read as dated in a tool like this. **Sentence case everywhere** — labels, buttons,
headings, table headers. Never Title Case, never ALL CAPS.

### Spacing

`--pms-space-1` … `--pms-space-6` (4px → 32px). Cards use `space-5` inside, table cells
`space-3 space-4`. Generous but not wasteful — a data-dense page should not force scrolling.

### Colour

One primary (`--pms-primary`) for primary actions and the active nav state; nothing else
competes with it, so "the blue thing" always means "the action". Platform uses
`--pms-platform` so the two areas are never confused.

Semantic colours, used sparingly and consistently: `--pms-danger` (destructive, errors),
`--pms-warning` (warnings, trial), `--pms-success` (confirmations, active), muted grey
(secondary, disabled, deactivated rows).

**Never colour alone.** Every badge and alert carries text, and alerts add an icon plus a
screen-reader-only label ("Error:", "Success:"). This matters for accessibility and because
the status badges later modules lean on — expiry, low stock — have to survive a greyscale
printout.

### Standard patterns

| Pattern | Partial / class | Notes |
|---|---|---|
| Page header | `.pms-page-header` | title + subtitle left, actions right |
| Card | `.pms-card` + `__header` / `__body` / `__footer` | |
| Read-only key/values | `.pms-detail-list` (`dl`/`dt`/`dd`) | collapses to one column on narrow screens |
| Form field | `.pms-form-label` + control + `.pms-help` + `.pms-field-error` | label **above** the input |
| Required marker | `<span class="pms-required" aria-hidden="true">*</span>` | red asterisk; `aria-hidden` because `required` already says it |
| Table | `.pms-table-wrap` > `.pms-table` | subtle header, row hover, `.pms-row-inactive` for de-emphasis |
| Pagination | `_Pagination` + `PagedView<T>` | 25 rows, "Showing X–Y of Z" always, controls only when needed |
| Empty state | `_EmptyState` | names what is missing, offers the action |
| Badge | `.pms-badge` + `--success` / `--warning` / `--danger` / `--info` / `--neutral` / `--platform` | via `StatusPresentation` |
| Alert / banner | `_Alert` + `AlertModel` | success is dismissible; errors are not |
| Confirmation | `_ConfirmModal` + `data-pms-confirm*` | one modal per page, reused |
| Icons | `_NavIcon` | inline SVG, `currentColor`, `aria-hidden` |

### Forms

Labels above inputs. Required fields marked with a red asterisk. Helper text in muted 13px
below any input that is not self-explanatory. Field errors directly beneath the input in red;
form-level errors ("Invalid credentials") as a banner at the top.

Every form carries `data-pms-guard`, which disables the submit button and shows a spinner on
submit. That is not cosmetic: a second POST of "create user" is a second user, and staff on a
slow connection *will* click again. The button is disabled on a deferred callback so its value
still posts, and an invalid form is left alone.

Primary submit plus a secondary **Cancel** that returns to the sensible previous page.

### Confirmations

Destructive and irreversible actions use `_ConfirmModal`, never `window.confirm()` — a browser
dialog cannot explain consequences in more than one sentence, cannot style the destructive
action red, and matches nothing else on screen. The trigger carries the wording:

```html
<button type="button" class="btn btn-sm btn-outline-danger"
        data-pms-confirm-form="user-active-…"
        data-pms-confirm-title="Deactivate Peter Pharmacist?"
        data-pms-confirm-ok="Deactivate"
        data-pms-confirm="This will remove Peter Pharmacist's access to Popular Pharmacy.
                          Their access to any other pharmacy is unaffected.">
```

Bootstrap supplies the focus trap and Escape; `site.js` focuses **Cancel** on open so a
reflexive Enter takes the safe path.

### Accessibility baseline

Every input has a `<label for>`. Icon-only controls carry `aria-label`. Tab order follows DOM
order — no positive `tabindex` anywhere. `:focus-visible` is styled, never removed. Tables
carry a visually-hidden `<caption>` and `scope` on headers. Colour is always paired with text.

---

## 5. Page inventory

| Route | Authorization | Purpose |
|---|---|---|
| `/login` | anonymous | Two modes. On a pharmacy's address: **email and password only**. On the base address: a signpost that navigates to a pharmacy's address, with no credential fields at all. |
| `/platform/login` | anonymous | Platform sign in. Visually and textually distinct. |
| `/` | `TenantUserPolicy` | Dashboard. Greeting, pharmacy, role, placeholder for later modules. |
| `/account` | `TenantUserPolicy` | My profile, read-only. Password change noted as later. |
| `/users` | `TenantAdminPolicy` | Staff list, deactivate / reactivate with modal. |
| `/users/create` | `TenantAdminPolicy` | Add Pharmacist or Employee. |
| `/logout` | `TenantUserPolicy`, POST | Clears the tenant cookie → `/login`. |
| `/platform/tenants` | `PlatformAdminPolicy` | Tenants list, status change with modal. |
| `/platform/tenants/create` | `PlatformAdminPolicy` | Onboard a pharmacy. |
| `/platform/tenants/{id}` | `PlatformAdminPolicy` | Detail: summary, status management, users, create admin. |
| `/platform/logout` | `PlatformAdminPolicy`, POST | Clears the platform cookie only. |
| `/denied` | anonymous | Permission denied. Anonymous because a signed-out session lands here. |
| `/error` | anonymous | Generic failure and 404s. Says nothing specific; detail is in the log. |

Both logout pages are **POST only**. A GET logout can be fired by any site with an `<img>`
tag, which would let a hostile page sign people out at will.

`RouteOptions.LowercaseUrls` is on, so generated links are the canonical lowercase routes.

---

## 6. Navigation

`Navigation/NavRegistry.cs` holds one list per area; `_SidebarNav.cshtml` renders whichever it
is given. **A new module edits the registry, not the layout.**

```csharp
public static readonly IReadOnlyList<NavItem> Tenant = new[]
{
    new NavItem("Dashboard", "/Index", "grid", MatchPrefix: "/"),
    new NavItem("Users", "/Users/Index", "people", Roles: new[] { UserRole.Admin }),
    new NavItem("My profile", "/Account", "person"),
    // Module 2 onwards: medicines, batches and stock, suppliers, billing, alerts, reports.
};
```

`Roles` empty or null means every signed-in user of that area. `MatchPrefix` decides the
active highlight, so `/users/create` still highlights "Users"; it defaults to the page's own
folder. Add an icon key to `_NavIcon.cshtml` if you need a new glyph.

Module 1 visibility:

| Nav item | Admin | Pharmacist | Employee |
|---|:---:|:---:|:---:|
| Dashboard | ✓ | ✓ | ✓ |
| Users | ✓ | | |
| My profile | ✓ | ✓ | ✓ |

Rendering is **server-side conditional** — a link a role should not have is absent from the
HTML, not merely hidden by CSS. Verified: a Pharmacist's dashboard contains no `href="/users"`
anywhere.

---

## 7. Key decisions

**Cookie over `localStorage`.** Razor Pages renders on the server, so the browser never needs
the token — putting it in web storage would expose it to every script on the page for no
benefit at all. Inside an encrypted, HttpOnly, Secure, SameSite=Strict cookie it is
unreachable by JavaScript, and the session's lifetime is managed by the framework instead of
by hand.

**Separate platform and tenant schemes.** Two audiences with structurally different sessions
and very different powers. Separate schemes mean the isolation is enforced by authentication,
not only by a policy — and someone who can suspend every pharmacy on the platform should never
have to wonder which area they are looking at, which is also why the platform gets its own
layout and accent.

**"Existing account linked" needs its own message.** One email is one account platform-wide,
so provisioning has two outcomes, and on the linked path *the password the administrator just
typed was never applied*. An administrator who passes that password to a new colleague has
created a support call and a person who cannot sign in. Both messages therefore say outright
which happened and, in the linked case, that the entered password was not used. This is not
a nicety; it is the single most confusing behaviour in the identity model.

**Nav hiding is never a security boundary.** `NavRegistry` decides what people are *offered*.
Every page carries its own `[Authorize]`, and folder conventions back it up, so a Pharmacist
who types `/users` is stopped by authorization — not by the absence of a link. Verified: it
returns one redirect to `/denied`, not a loop and not a raw API 403.

**API validation is authoritative; client-side is convenience.** `required`, `minlength` and
`pattern` exist to save a round trip. The API's `errors` dictionary and its 409s are what
actually decide, and both are bound back onto the exact inputs the person has to fix.

**There is no pharmacy field on the sign-in form.** Typing an identifier you never think
about is friction on every single sign-in, so the address supplies it and the form asks only
for email and password — the two things a person actually knows.

The page has two modes, and only one of them collects credentials:

| Address | Mode | What is on screen |
|---|---|---|
| `popular-pharmacy.pms.example.com` | credentials | "Signing in to popular-pharmacy", email, password |
| `pms.example.com`, `localhost`, a reserved prefix | signpost | an explanation and a pharmacy-name box that **navigates**; no email or password fields exist |

The signpost is not the old field in disguise: it does not sign anyone in, it redirects to the
pharmacy's own address, and the destination is always built as `{name}.{baseDomain}`. That
construction is load-bearing — following a host somebody typed on an anonymous page would be
an open redirect — so a name containing a dot, slash or colon is refused with an explanation.
A pharmacy that uses its own domain therefore cannot be reached this way; it has its own
address, and the page says so.

It also does **not** check whether the pharmacy exists before redirecting. Confirming that to
an anonymous visitor would hand over exactly what the login endpoint refuses to reveal; an
unknown name simply lands on a page whose sign-in then fails generically.

Two things keep the address-based mode from being a trap: the page names the pharmacy it is
about to sign you in to, and a "Sign in to a different pharmacy" link switches to the signpost
for anyone who works at more than one.

A pharmacy can be named **either way**, in the one `DomainName` column:

| Stored value | Address | Works when |
|---|---|---|
| `popular-pharmacy` | `popular-pharmacy.pms.example.com` | immediately, the moment the row exists |
| `citycare.com` | `citycare.com` | once that domain points here and has a certificate |

What stops one column holding two kinds of value from being ambiguous is a stated order:
`Tenant.ResolutionCandidates` tries the **whole host first**, so a pharmacy that owns its
address is found by it, and only then the leading label under the base domain. Creating a
domain that sits *under* the base domain is refused, because exact matching wins and such a
row would shadow the pharmacy that legitimately owns that prefix.

The resolution rule lives in the **API**, not here. `PMS.Web` only answers the narrow
question "does this address name a pharmacy at all, or is it the platform's front door?" and
hands the host over. Deciding which pharmacy a host belongs to needs the tenant rows, which
this app deliberately cannot see.

**Pagination is presentation-only for now.** `GET /api/platform/tenants` and `GET /api/users`
return whole lists. `PagedView<T>` slices in this app, so the controls and the count line are
honest about what is on screen but save the API nothing. Correct at tens of rows, wrong at
thousands — and written so that adding `page`/`pageSize` to those endpoints changes only how
`PagedView` is constructed, not a single call site or view.

---

## 8. Out of scope

- Password change and reset UI — the API has no endpoint for it yet.
- Wildcard TLS and DNS setup. The app resolves the pharmacy from the address; making
  `*.pms.example.com` and any custom domain actually reach the server is deployment work.
- **Tenant switcher** — a dropdown to hop between pharmacies mid-session. With domain-based
  login a user re-signs-in at the other pharmacy's domain instead, and the two cookie schemes
  mean each login carries its own tenant and role. Worth revisiting if it becomes real
  friction for people who genuinely work across pharmacies daily.
- Platform admin management UI (creating further platform admins).
- Dashboard panels beyond the placeholder — those arrive with their own modules.
- Dark mode.
