# Users & Authentication

How a person gets access to a pharmacy, and what decides what they can do there.

> **Status: working end to end.** All eleven endpoints, the JWT scheme, the SQL scripts, the
> seeded platform administrator and a Razor UI that consumes the API over HTTP are in place,
> and the whole flow has been exercised against SQL Express: create a pharmacy, give it an
> administrator, sign in there, add staff, deactivate them, suspend the pharmacy. The
> [Build status](#build-status) section is the honest inventory, including what is still
> missing.

## Running it locally

Three things, in order.

**1. Create the tables.** From `database/scripts`, against your SQL Server instance:

```
sqlcmd -S .\SQLEXPRESS -E -C -i 001_CreateDatabase.sql
sqlcmd -S .\SQLEXPRESS -E -C -i 002_CreateTenantsTable.sql
sqlcmd -S .\SQLEXPRESS -E -C -i 003_CreateUsersTable.sql
sqlcmd -S .\SQLEXPRESS -E -C -i 004_CreateUserTenantMembershipsTable.sql
```

Each script sets `ANSI_NULLS` and `QUOTED_IDENTIFIER` on itself, and that is not decoration:
sqlcmd defaults `QUOTED_IDENTIFIER` to **off**, and with it off the tables are created while
every filtered unique index silently is not — including the two that hold the platform-admin
invariant together.

**2. Start the API.** It refuses to start without a signing key, deliberately:

```
setx Jwt__SigningKey "<at least 32 characters>"      # once, or use user-secrets
dotnet run --project src/Presentation/PMS.WebApi
```

On first run it seeds the platform administrator from `PlatformAdmin` configuration and logs
what it did. Development defaults: `platform@pms.local` / `Platform@123`.

**3. Start the web app.** It reads the API's address from `Api:BaseUrl`, so the API has to be
running first:

```
dotnet run --project src/Presentation/PMS.Web
```

Then sign in at `/platform/login`, add a pharmacy, and give it an administrator.

**Signing in as that administrator.** The pharmacy comes from the address, so a pharmacy
stored as `popular-pharmacy` is reached at `popular-pharmacy.localhost` in development
(`Tenancy:BaseDomain` is `localhost` there). Browsers resolve `*.localhost` to loopback on
their own, so no hosts-file entry is needed:

```
http://popular-pharmacy.localhost:5285/login      # email and password only
```

Plain `http://localhost:5285/login` has **no sign-in form** — the address names no pharmacy,
so it shows a signpost that takes you to one instead.

Use **http** locally. Development deliberately skips https redirection and relaxes the session
cookie to `SameAsRequest`, because the dev certificate covers `localhost` but not
`popular-pharmacy.localhost`; every other environment keeps `Secure` and redirection on.

## Overview

There is **one identity table for every human in the system** — the person who operates the
platform and the person behind the counter are both rows in `Users`. Nothing on that row says
what they can do or where.

That is decided entirely by their **membership rows**:

```
User  ──<  UserTenantMembership  >──  Tenant
                  │
              Role, IsActive
```

A membership with a tenant is pharmacy staff. A membership with **no** tenant is a platform
operator. That is the whole of what makes someone a platform admin — there is no second
identity table, no `IsPlatformAdmin` column, no parallel login system.

**There is no public registration anywhere.** Access is only ever granted downward:

```
Platform Admin
   └── creates a Tenant (a pharmacy)
          └── creates that pharmacy's first Admin
                 └── creates its Pharmacists and Employees
```

## Why platform admin is a membership with no tenant

The alternative — a separate `PlatformUsers` table — was rejected because it duplicates
identity. Two tables means two password columns, two hashing paths, two places to disable an
account, and an open question the first time one person needs both kinds of access.

Modelling it as "a membership that points at no pharmacy" keeps one identity and one login
mechanism. The cost is one piece of special handling, described under
[Two query filters](#two-query-filters): `UserTenantMembership.TenantId` has to be nullable,
which means it cannot use the generic tenant filter every other table uses.

That trade was worth it — one nullable column and one hand-written filter, against a
duplicated identity system.

## The three entities

### `Tenant` — a pharmacy

| Field | Notes |
|---|---|
| `Id` | Guid. What every tenant-owned row points at, and what the `tenant_id` claim carries |
| `Name` | Business name, as printed on invoices |
| `DomainName` | **Required, unique.** The key a login is addressed to, e.g. `popular-pharmacy` |
| `Status` | `Trial` / `Active` / `Suspended` |
| `SubscriptionPlan` | Recorded only — no billing or entitlement logic |
| `IsDeleted`, `DeletedOnUtc`, `DeletedBy` | Soft delete; a pharmacy's history must stay readable |

`CanBeUsed` is true when `Status != Suspended && !IsDeleted`. Suspension withdraws **access**;
it deletes nothing, and reactivating restores everything intact.

A pharmacy is named **one of two ways**, both stored in this one column:

| Stored value | Reached at | Available |
|---|---|---|
| `popular-pharmacy` | `popular-pharmacy.pms.example.com` | as soon as the row exists |
| `citycare.com` | `citycare.com` | once that domain points at the platform and has a certificate |

The first costs nothing to onboard — one wildcard DNS record and one wildcard certificate
cover every pharmacy. The second is what a pharmacy asks for when it wants its own branding,
and it needs per-pharmacy setup before anyone there can sign in.

`Tenant.ResolutionCandidates` is what keeps one column holding two kinds of value from being
ambiguous, and it does so with an **order** rather than a flag: the whole host is tried first,
so a pharmacy that owns its address is found by it, and only then the leading label under
`Tenancy:BaseDomain`. Consequently a `DomainName` that sits *under* the base domain is refused
at creation — exact matching wins, so such a row would shadow the pharmacy that legitimately
owns that prefix.

`DomainName` is normalised to a bare lowercase host on the way in — scheme, port, path,
query and any trailing root dot stripped — so `https://CityCare.com/login` and `citycare.com`
are recognised as the
same pharmacy. Without that, two pharmacies could hold what is really the same domain and a
login could not be resolved to one of them.

### `User` — one person, once, platform-wide

| Field | Notes |
|---|---|
| `Id` | Guid |
| `Email` | **Globally unique**, case-folded. The identity anchor |
| `PasswordHash` | BCrypt, work factor 12 |
| `FullName` | |
| `IsGloballyActive` | Platform-level kill switch, independent of any membership |

Deliberately **not** tenant-scoped. A login has to find a user before any pharmacy is known,
so a tenant filter here would make signing in impossible.

Email is unique **globally, not per pharmacy**. That is what allows one account to hold
memberships at several pharmacies: a login supplies a domain and an email, and the email alone
must identify the person.

`IsGloballyActive` and a membership's `IsActive` do different jobs. Turning off the former
locks someone out of every pharmacy at once; deactivating a membership affects only that one.

### `UserTenantMembership` — what someone may do, and where

| Field | Notes |
|---|---|
| `Id` | Guid |
| `TenantId` | **Nullable.** `null` means a platform-level membership |
| `UserId` | |
| `Role` | `PlatformAdmin = 0`, `Admin`, `Pharmacist`, `Employee` |
| `IsActive` | Revoking locks the person out of *this* pharmacy only |
| `JoinedAt` | |

**The role lives here, never on `User`.** The same person can be a Pharmacist at one pharmacy
and an Employee at another; a role on the user row could not express that.

There is no public constructor. Two factory methods enforce the invariant in code:

```csharp
UserTenantMembership.ForTenant(tenantId, userId, role);  // throws for PlatformAdmin
UserTenantMembership.ForPlatform(userId);                // always PlatformAdmin, no tenant
```

## Database constraints

Three rules, enforced by the database rather than only by the factory methods — because the
identity model rests on them.

**Check constraint — `CK_UserTenantMemberships_PlatformAdminHasNoTenant`**

```sql
([Role] = 0 AND [TenantId] IS NULL) OR ([Role] <> 0 AND [TenantId] IS NOT NULL)
```

`PlatformAdmin` if and only if there is no tenant. A `PlatformAdmin` row with a real tenant
would be a pharmacy user holding platform powers; any other role without one would be an
orphan no pharmacy owns.

**`UX_UserTenantMemberships_Tenant_User`** — unique `(TenantId, UserId)` `WHERE TenantId IS NOT NULL`

One membership per person per pharmacy. Filtered to real tenants because SQL Server treats
NULLs as **equal** in a unique index — unfiltered, it would allow only one platform membership
across the entire system.

**`UX_UserTenantMemberships_PlatformPerUser`** — unique `(UserId)` `WHERE TenantId IS NULL`

At most one platform membership per person.

## Two query filters

Tenant isolation is applied by convention, but this module needs **two** mechanisms rather
than one.

**1. The generic filter.** Every entity implementing `ITenantEntity` gets
`TenantId == CurrentTenantId`, attached by reflection in `ApplicationDbContext`. Future
business entities (Medicine, Batch, Sale) opt in simply by implementing the interface. Full
detail in [multi-tenancy.md](multi-tenancy.md).

**2. A hand-written filter for `UserTenantMembership`**, kept out of that loop:

```csharp
modelBuilder.Entity<UserTenantMembership>()
    .HasQueryFilter(m => m.TenantId == CurrentTenantId);
```

It cannot use the generic one because its `TenantId` is **nullable** — the generic filter
compares a non-nullable `Guid`, so it would neither compile against this shape nor mean the
right thing.

What falls out for free: platform rows have `TenantId = null`, and **null never equals a
tenant id**, so they are excluded with no special case. Inside a pharmacy you see that
pharmacy's memberships and nothing else — not another pharmacy's, and not the platform
operators'.

`Users` and `Tenants` carry **no** tenant filter at all. Both must be readable before a tenant
exists, which is exactly what a login does.

## Sanctioned `IgnoreQueryFilters()` exceptions

This is the **complete list for the whole application**. Treat any new one as a signal that an
entity was modelled wrongly.

| # | Where | Why |
|---|---|---|
| 1 | `Tenant` lookup by `DomainName` during login | No tenant context exists yet — this call is what establishes it |
| 2 | `User` lookup by `Email` in any login or user-creation flow | Identity is global, not tenant-scoped |
| 3 | `UserTenantMembership` lookups in platform-admin context, or the single membership check during tenant login | Runs as the tenant context is being established, not yet inside it |
| 4 | `UserTenantMembership` list for a **named** tenant, read by a platform operator (`GET /api/platform/tenants/{id}/users`) | A platform session holds no tenant, so the filter would match nothing. The tenant id is a parameter and the `WHERE` clause on it is the only thing scoping the read — which is why it lives in this file, not a handler |

## Login flow — internals

**Tenant user** — `POST /api/auth/login` with `DomainName`, `Email`, `Password`:

1. Resolve the `Tenant` *(exception 1)*. What arrives is either the host the browser was on
   or whatever someone typed, and `Tenant.ResolutionCandidates` turns it into an ordered list:
   the whole host first, then the leading label under `Tenancy:BaseDomain`. The first match
   wins. Missing, or `CanBeUsed == false` → generic failure.
2. Find the `User` by `Email` *(exception 2)*. Verify the hash and `IsGloballyActive`.
3. Find the `UserTenantMembership` for that `(TenantId, UserId)` *(exception 3)*. Must exist
   and be active.
4. Issue `{ sub, tenant_id, role }` — role from the **membership**, not the user.

**Platform admin** — `POST /api/platform/auth/login` with `Email`, `Password`. No domain. The
membership must have `TenantId IS NULL` and `Role = PlatformAdmin`. Issues
`{ sub, platform_admin: true }` — no tenant, no role.

Every failure returns the same message. Distinguishing "no such domain" from "no such user"
from "your access was revoked" tells an attacker which pharmacies exist and who works there.

### Why there is no "select tenant" step

The domain in the request decides the pharmacy, so a user with access to several never picks
from a list.

Worked example — `pharmacist@popular.com` holds two memberships:

| Signs in at | Resolves to | Token role |
|---|---|---|
| `popular-pharmacy` | Popular Pharmacy | `Pharmacist` |
| `city-pharmacy` | City Pharmacy | `Employee` |

Same email, same password, two independent sessions with different roles. Nothing in the flow
asks "which pharmacy?", and no token ever spans both.

`DomainName` is an explicit field in the request body for now. The lookup is written so it can
be fed from a subdomain or `Host` header later without changing anything downstream of "the
domain has been resolved to a tenant".

## API reference

This is the contract. It is written out in full because the API *is* the product: the Razor
frontend consumes exactly this over HTTP — it holds **no project reference** to any other
assembly in the solution — and a mobile or desktop client will consume the same thing.

### Conventions

Base path `/api`. JSON in, JSON out. Bearer token on every call except the two logins:

```
Authorization: Bearer <jwt>
```

**Errors are always RFC 7807 `ProblemDetails`**, produced by `ApiControllerBase` from the
`Result` returned by a handler:

```json
{
  "title":   "Validation Error",
  "detail":  "Domain name 'popular-pharmacy' is already taken.",
  "status":  400,
  "code":    "Validation.DuplicateDomain",
  "traceId": "0HN7A2B3C4D5E"
}
```

`code` is the machine-readable discriminator — clients switch on that, never on `detail`.
`traceId` is added to every problem response for log correlation.

A **validation** failure carries the per-field messages as well, and those are what a form
should display — its `detail` is only ever the generic sentence:

```json
{
  "title":  "Validation Failed",
  "detail": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name":       ["'Name' must not be empty."],
    "DomainName": ["A domain must be a host such as 'citycare.com' - ..."]
  }
}
```

| Result code shape | HTTP |
|---|---|
| `*.NotFound` | 404 |
| `Validation.*` | 400 |
| `Error.Conflict` | 409 |
| `Error.Unauthorized` | 401 |
| `Error.Forbidden` | 403 |
| anything else | 400 |

Authentication failures on the two login endpoints are the deliberate exception: they always
return the **same** `401` regardless of cause, so nothing reveals which pharmacies exist or
who works there.

### Token shapes

| | Platform admin | Tenant user |
|---|---|---|
| `sub` | user id | user id |
| `platform_admin` | `true` | *absent* |
| `tenant_id` | *absent* | pharmacy id |
| `role` | *absent* | `Admin` / `Pharmacist` / `Employee` |

A platform token carries no tenant, so every tenant query filter it meets resolves to
nothing. Crossing into a pharmacy's data is always an explicit `IgnoreQueryFilters()` at the
call site.

---

### Platform endpoints

Require `platform_admin: true`. All return `403` otherwise.

#### `POST /api/platform/auth/login` — anonymous

```json
{ "email": "ops@pms.app", "password": "..." }
```

`200` → `{ "token": "...", "expiresAtUtc": "2026-09-07T18:00:00Z" }`
`401` → generic invalid credentials.

#### `POST /api/platform/tenants`

```json
{ "name": "Popular Pharmacy", "domainName": "popular-pharmacy", "subscriptionPlan": "Basic" }
```

`201` → the created tenant, `Status: "Trial"`. `Location` header points at the new resource.
`400` `Validation.DuplicateDomain` if the domain is taken.

`domainName` is normalised before the uniqueness check, so `Popular-Pharmacy` and
`https://popular-pharmacy/` collide with an existing `popular-pharmacy`.

#### `GET /api/platform/tenants`

`200` → every tenant, regardless of status.

**Not paginated.** It returns the whole list, and so does `GET /api/users`. The Razor UI
renders pagination controls over the full response, which is honest about what is on screen
but saves the database nothing — fine at tens of pharmacies, wrong at thousands. See
`PagedView<T>` in `PMS.Web`, which is written so that adding `page`/`pageSize` to these two
endpoints changes only how it is constructed.

#### `GET /api/platform/tenants/{tenantId}`

`200` → one tenant. `404` when it does not exist or has been deleted.

#### `GET /api/platform/tenants/{tenantId}/users`

`200` → that pharmacy's memberships, joined to their users — the same shape as
`GET /api/users`, but for a pharmacy the caller is not inside.

Platform-only, and the tenant id in the route is the only thing that scopes it, which is why
the query behind it is *exception 4* rather than an ordinary read. There is deliberately no
write counterpart: staff are added and deactivated by an Admin signed in to that pharmacy, so
this is a window, not a second users screen with its own rules.

#### `PATCH /api/platform/tenants/{tenantId}/status`

```json
{ "status": "Suspended" }
```

`200` → the updated tenant. `404` if unknown.

Suspending takes effect immediately for **existing sessions** too, not just at next login —
see [Suspension takes effect twice](#suspension-takes-effect-twice).

#### `POST /api/platform/tenants/{tenantId}/admin-user`

The only way a pharmacy gets its first user.

```json
{ "email": "admin@popular.com", "fullName": "Ali Ahmed", "password": "..." }
```

Two outcomes, distinguished in the response so the caller knows whether a password was used:

| | |
|---|---|
| **New email** | `201` → `{ "userId", "membershipId", "outcome": "UserCreated" }` — `User` + membership created in one transaction |
| **Existing email** | `200` → `{ "userId", "membershipId", "outcome": "ExistingUserLinked" }` — membership only; **the supplied password is ignored and their existing one is untouched** |

That second path is how one person comes to work at two pharmacies.

---

### Tenant endpoints

#### `POST /api/auth/login` — anonymous

```json
{ "domainName": "popular-pharmacy", "email": "admin@popular.com", "password": "..." }
```

`200` → `{ "token", "expiresAtUtc", "tenant": { "id", "name", "domainName" }, "role": "Admin" }`
`401` → generic invalid credentials, for *every* cause: unknown domain, suspended pharmacy,
unknown email, wrong password, no membership, deactivated membership.

#### `POST /api/users` — `Admin`

```json
{ "email": "pharmacist@popular.com", "fullName": "Rina Khan",
  "password": "...", "role": "Pharmacist" }
```

`role` accepts `Pharmacist` or `Employee` only. `Admin` and `PlatformAdmin` are rejected with
`400 Validation.RoleNotAllowed` — Admins are created by a platform admin, and `PlatformAdmin`
can never exist against a pharmacy at all.

Same two outcomes as the platform admin-user endpoint: `201 UserCreated`, or `200
ExistingUserLinked` with the password ignored.

The new membership is scoped to the caller's own pharmacy — taken from the token, never from
the request body, so an Admin cannot provision into someone else's pharmacy.

#### `GET /api/users` — `Admin`

`200` → the current pharmacy's memberships joined to their users:

```json
[ { "membershipId": "...", "userId": "...", "email": "pharmacist@popular.com",
    "fullName": "Rina Khan", "role": "Pharmacist", "isActive": true,
    "joinedAt": "2026-09-01T09:00:00Z" } ]
```

No explicit tenant filter appears in the query — the membership query filter does it. Another
pharmacy's rows and the platform admins' null-tenant rows are both absent by construction.

#### `PATCH /api/users/{membershipId}/deactivate` · `/reactivate` — `Admin`

`200` → the updated membership. `404` if the membership belongs to another pharmacy — the
query filter makes it invisible, so it is genuinely not found rather than forbidden.

Affects that **membership only**. The person's identity and their access to other pharmacies
are untouched.

#### `GET /api/users/me` — any authenticated tenant user

`200` → `{ "userId", "email", "fullName", "role", "tenant": { "id", "name", "domainName" } }`

`role` is the role **at the current pharmacy**. The same call with a token from another
pharmacy returns a different role for the same person.

---

### What the frontend never sees

Worth stating for whoever builds the Razor app or a mobile client:

- **No tenant picker.** The domain is supplied at login and the token carries the pharmacy
  from then on. There is no endpoint listing "pharmacies you belong to", by design.
- **No `tenant_id` in any request body or path.** It comes from the token. A client that
  tries to pass one is ignored at best.
- **No registration.** There is no self-service signup endpoint to call.


## Suspension takes effect twice

`Status = Suspended` blocks login. It is also checked **per request** by
`TenantResolutionMiddleware`, which returns `403` for a suspended or deleted pharmacy.

The second check is not redundant: without it, suspending a pharmacy would not take effect
until every token already issued had expired, so one suspended this morning would keep
trading until its sessions ran out.

That check deliberately sits at the request edge rather than in the query filter — suspension
withdraws access, it does not mean the data stopped existing. A filter would both cost a join
to `Tenants` on every read and hide a suspended pharmacy's data from the platform operator
trying to help them.

## Tenant-scoped requests

A command or query that only makes sense inside a pharmacy implements `ITenantScopedRequest`.
`TenantValidationBehavior` refuses it when no tenant is resolved, so handlers never check.

Without it such a request would still run: the query filter would compare against `Guid.Empty`
and quietly return nothing, so the caller would see an empty pharmacy rather than an error.
Failing in the pipeline names the problem instead.

## Build status

### Built and tested

| Piece | Where |
|---|---|
| `Tenant`, `User`, `UserTenantMembership` | `PMS.Domain/Entities` |
| `TenantStatus`, `UserRole` | `PMS.Domain/Enums` |
| EF configurations: check constraint, both filtered unique indexes | `PMS.Persistence/Configurations` |
| Generic tenant filter + membership filter | `PMS.Persistence/Contexts/ApplicationDbContext` |
| `TenantEntityInterceptor` — stamps `TenantId`, refuses reassignment | `PMS.Persistence/Interceptors` |
| `ITenantStatusValidator` + `TenantResolutionMiddleware` | Persistence / WebApi |
| `ITenantScopedRequest`, `TenantValidationBehavior` | `PMS.Application` |
| `AuthClaims`, `IPasswordHasher`, `IJwtTokenService` | `PMS.Application` |
| `BCryptPasswordHasher`, `JwtTokenService`, `CurrentTenantService` | `PMS.Infrastructure` |
| JWT bearer scheme + the three authorization policies | `PMS.WebApi/Extensions/AuthenticationExtensions` |
| All 13 endpoints across 4 controllers | `PMS.WebApi/Controllers` |
| `PlatformAdminSeeder` — the first administrator | `PMS.Application/Common/Provisioning` |
| `Tenants`, `Users`, `UserTenantMemberships` tables | `database/scripts/002`–`004` |
| Razor UI: two cookie schemes, 12 routes, its own design system | `PMS.Web` — see [docs/frontend/01-auth-and-layout.md](frontend/01-auth-and-layout.md) |

99 tests pass: tenant isolation, fail-closed behaviour, domain normalisation, the
authorization policies against a live pipeline, and the validation-problem shape.

### Seeding the first administrator

Pharmacies are created by a platform administrator, and platform administrators are created
by nothing — so one has to be seeded. `PlatformAdmin:Enabled` turns it on:

```json
"PlatformAdmin": {
  "Enabled":  true,
  "Email":    "platform@pms.local",
  "FullName": "Platform Administrator",
  "Password": "Platform@123"
}
```

Committed `appsettings.json` has it **off** with a blank password; the development file has it
on. Supply the password as `PlatformAdmin__Password` or through user-secrets for anything
real.

Two limits keep this from being a back door. It never touches an existing password — an email
that already has an account is *granted* platform access and keeps its own credentials. And it
never creates a second platform administrator: once one exists the seeder is a no-op, so
leaving the configuration in place does not let anyone mint administrators by editing a file.

### The Razor frontend

`PMS.Web` has **no project reference to anything else in the solution**. It declares the wire
contract locally (`Api/ApiContracts.cs`) and calls the API through `HttpClient`. That costs a
few duplicated records and buys the only real proof that the API is usable by a client that
cannot cheat — which a mobile app also cannot.

The JWT lives inside the ASP.NET authentication cookie, encrypted by data protection, so it is
out of JavaScript's reach entirely: no `localStorage`, no token rendered into a page. The
cookie's lifetime is pinned to the token's own expiry so the two cannot drift apart.

| Page | Who |
|---|---|
| `/`, `/Login`, `/Platform/Login`, `/Denied` | anonymous |
| `/Platform/Tenants`, `/Platform/Tenants/Create`, `/Platform/Tenants/Admin/{id}` | platform admin |
| `/Dashboard` | any pharmacy user |
| `/Users`, `/Users/Create` | pharmacy `Admin` |

Pages are authorized by folder convention — everything requires a signed-in user unless it
opts out — so a page added without an attribute fails closed rather than open.

Every page routes an API `401` to a fresh sign-in and an API `403` to `/Denied`, dropping the
local cookie in both cases. A cookie that outlives its usefulness — the pharmacy gets
suspended, the membership gets deactivated — would otherwise leave someone apparently signed
in while every call failed.

### Three bugs only running it could find

Worth recording, because the unit, schema and architecture suites were all green throughout:

1. **Inbound claim mapping renamed every claim.** `AddJwtBearer` maps well-known short names
   onto the old WS-* URIs by default, so a token issued with `role` arrived as
   `.../claims/role`; `RequireClaim("role", "Admin")` matched nothing and every pharmacy-Admin
   endpoint answered 403 to a valid Admin token. The tenant-only endpoints kept working, which
   made it look like an authorization bug rather than a renaming one. Fixed with
   `MapInboundClaims = false`; guarded by `AuthorizationPolicyTests`.
2. **`ProblemDetails` was serialized as its declared type**, so the `errors` dictionary was
   dropped from every validation response and a 400 carried no field information at all.
   Fixed by serializing the runtime type; guarded by `ValidationProblemTests`.
3. **`CreateTenant`'s validator matched the raw domain**, rejecting `https://citycare.com/x`
   with a 400 even though the entity, the uniqueness check and the login lookup all normalise
   away the scheme and path — this document claimed URLs were accepted, and the API disagreed.
   The rule now validates the normalised host; guarded by
   `CreateTenantCommandValidatorTests`.

### Not built

1. **`RowVersion` is dead weight.** `AggregateRoot` declares it, so EF selects it on every
   read and the scripts have to carry the column, but nothing calls `IsRowVersion()` — it is
   never written and never checked. Either map it properly (and change the column to
   `ROWVERSION`) or drop it from the base class.
2. **Acceptance tests for the endpoint bodies** — the two-domains-same-user case and the
   constraint rejections are verified by hand against SQL Express, not by a test.
3. **`IDapperService` is registered**, and raw SQL bypasses both the query filters and the
   interceptor. Any procedure or `FromSql` has to filter by `TenantId` in its own `WHERE`.
4. **The no-op exception** — `IdentityQueries.FindUserByEmailAsync` calls
   `IgnoreQueryFilters()` on `Users`, which has no filter to ignore. Harmless, but it reads
   as though it were load-bearing.

### Open question

`User` is **not** soft-deletable — the design gives it only `IsGloballyActive`, and
`UserTenantMembership` cascades on user delete. Since memberships are the record of who had
access where, hard-deleting a user would erase that history. `Users.Email` is also uniquely
indexed without a soft-delete filter, so an address is claimed for good once used.

## Out of scope

- Subdomain / `Host`-header resolution (explicit `DomainName` for now)
- Tenant Admins creating further Admins — `POST /api/users` is Pharmacist/Employee only
- Multiple platform admins, or inviting them
- Billing or trial-expiry enforcement from `SubscriptionPlan`
- Password reset, refresh tokens, MFA

A platform admin's email colliding with a would-be tenant Admin's is **allowed**: `User` is
correctly one global identity, and nothing should stop the same person holding both a platform
membership and a pharmacy one.

## What later modules inherit

Future modules do not think about any of this. Every new entity implements `ITenantEntity` and
is filtered and stamped automatically; handlers consume `ICurrentTenantService` and
`ICurrentUserService` exactly as a single-tenant application would.

Two rules carry forward:

- **Uniqueness is per tenant.** Two pharmacies may legitimately both stock batch `B-100` or
  issue invoice `INV-000001`, so unique indexes must include `TenantId`.
- **No new `IgnoreQueryFilters()`.** The four above are the complete list, and they all live in `IdentityQueries.cs`.
