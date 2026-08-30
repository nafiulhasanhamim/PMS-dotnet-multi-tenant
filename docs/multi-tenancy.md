# Multi-Tenancy

One deployment serves many pharmacies. Each is a **tenant**, and no tenant may ever see
another's data.

## The four decisions

| Decision | Choice |
|---|---|
| Isolation | Shared database, shared schema, `TenantId` column on every tenant-owned table |
| Tenant resolution | A signed `tenant_id` claim on the authenticated principal |
| User scope | One user belongs to exactly one tenant |
| Cross-tenant access | A `PlatformAdmin` role exists, but must cross the boundary explicitly |

### Isolation: shared database

Cheapest to run and to migrate — one schema, one migration, one backup, however many
pharmacies. The trade-off is that isolation is **enforced by code, not by the database**: a
query that escapes the filter reads everyone's data. That is why the filter is applied by
convention rather than by hand (below), and why the tests in
`tests/PMS.SchemaTests/Tenancy/` matter more than any others in the system.

If a pharmacy ever needs physical separation — a regulator demanding it, or one tenant
outgrowing the rest — the move is database-per-tenant, and it means changing connection
resolution rather than the domain.

### Resolution: a claim, never caller input

`TenantContext` reads `tenant_id` from `ClaimsPrincipal`. That claim is signed into the
token at login, so a caller cannot edit it. A header or a route segment could be changed by
hand, so neither is trusted. The same mechanism works unchanged for the API, Razor, and a
future mobile or desktop client.

## How isolation is enforced

Two conventions, both in the persistence layer. An entity opts into both simply by
implementing `ITenantEntity` — there is nothing to remember per entity, per query, or per
handler.

**Reads** — `ApplicationDbContext.ApplyQueryFilters` walks the model and attaches a global
query filter to every `ITenantEntity`.

**Writes** — `TenantEntityInterceptor` stamps `TenantId` on insert, and refuses to move an
existing row to a different tenant. Handlers never set `TenantId`; one that forgot would
create a row no query could return, and one that set it from user input would let a caller
write into another pharmacy's data.

### Fail closed

`ITenantContext.TenantId` is `Guid.Empty` when nothing resolves, and the filter compares
against it directly. An unauthenticated or misconfigured request therefore matches **no
rows**. Empty is not a wildcard: the failure mode must be an empty screen, never another
pharmacy's stock.

Writing with no resolved tenant throws rather than producing an orphan row.

### Platform admin

Being a `PlatformAdmin` does **not** widen the query filter. Crossing tenants is always an
explicit `IgnoreQueryFilters()` at the call site, so reading another pharmacy's data is
something you can find by searching for it, rather than something that happens quietly
because of who is signed in.

## Two traps this design avoids

Both are easy to write, neither fails loudly, and both leak data.

### 1. Baking the tenant into the cached model

EF Core builds the model **once per context type** and caches it. If the filter captures a
tenant id while the model is being built — the shape `docs/COOKBOOK.md` Recipe 9 shows,
injecting `ITenantContext` into an `IEntityTypeConfiguration` and reading it there — then
whichever pharmacy happened to trigger the first request is baked in, and every later
request for every other pharmacy is filtered to that first tenant.

The fix is that the filter reads `ApplicationDbContext.CurrentTenantId`, a member of the
context. EF Core translates a context member in a filter into a query **parameter**, re-read
on each execution.

> The cookbook recipe in this repository predates this decision and shows the unsafe shape.
> Follow this document, not that recipe.

Pinned by `TheFilterIsReEvaluatedPerContext_NotBakedIntoTheCachedModel`.

### 2. `HasQueryFilter` replaces, it does not combine

Calling it twice on one entity keeps only the last filter. Applying soft-delete in one pass
and tenancy in another would silently drop one of them — losing the soft-delete half
resurrects deleted rows, losing the tenant half exposes every pharmacy to every other.

So the two conditions are composed into a **single** filter per entity, chosen by which
interfaces the entity implements.

Pinned by `SoftDeleteAndTenantFilters_BothApply`.

## Writing a tenant-owned entity

```csharp
public sealed class Medicine : BaseAuditableAggregateRoot<Guid>, ITenantEntity, ISoftDelete
{
    public Guid TenantId { get; private set; }   // stamped on insert; never set by hand
    // ...
}
```

That is the whole opt-in. Filtering and stamping follow automatically.

**Uniqueness is per tenant, not global.** Two pharmacies may legitimately both stock batch
`B-100` or issue invoice `INV-000001`. Unique indexes must include `TenantId`:

```csharp
builder.HasIndex(m => new { m.TenantId, m.Sku }).IsUnique();
```

`Tenant` itself is the exception to all of this: it is the tenant list, so it is not
tenant-owned and its `Slug` is globally unique.

## Tests

`tests/PMS.SchemaTests/Tenancy/TenantIsolationTests.cs` — 8 tests, all behavioural, run
against two tenants sharing one database:

- a query returns only the current tenant's rows
- insert stamps the tenant without the caller setting it
- no resolved tenant sees nothing rather than everything
- writing without a resolved tenant throws
- moving a row between tenants throws
- soft-delete and tenant filters both apply
- platform admin crosses the boundary only by asking explicitly
- the filter is re-evaluated per context, not baked into the cached model

Verified by mutation: removing the tenant filter fails 4 of them, and reintroducing the
two-pass replacement trap fails the test written for it.

## Not done yet

**Authentication does not exist.** `Program.cs` calls `UseAuthentication()` but no scheme is
registered, so nothing issues the `tenant_id` claim today. Until that is built, every request
resolves `Guid.Empty` and — by design — sees nothing. Auth is the next piece, and it must
issue tenant and role claims together.
