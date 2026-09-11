using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PMS.Web.Api;

/// <summary>
/// Every call this app makes to the API. One place, so no page constructs a URL or a header.
///
/// The bearer token is not attached here — <c>ApiTokenHandler</c> does it for every request
/// on this client, so a call added later cannot forget it. The two login endpoints are
/// anonymous and simply have no token stored yet when they run.
///
/// Nothing here throws for an HTTP failure. A rejected login or a duplicate domain is an
/// expected answer that belongs on a page, so every method returns <see cref="ApiResult{T}"/>
/// and the page decides what to show.
/// </summary>
public sealed class PmsApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<PmsApiClient> _logger;

    public PmsApiClient(HttpClient http, ILogger<PmsApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ── authentication ───────────────────────────────────────────────────────────────────

    public Task<ApiResult<AuthResult>> PlatformLoginAsync(
        PlatformLoginRequest request, CancellationToken ct = default) =>
        SendAsync<AuthResult>(HttpMethod.Post, "api/platform/auth/login", request, ct);

    public Task<ApiResult<AuthResult>> TenantLoginAsync(
        TenantLoginRequest request, CancellationToken ct = default) =>
        SendAsync<AuthResult>(HttpMethod.Post, "api/auth/login", request, ct);

    // ── platform ─────────────────────────────────────────────────────────────────────────

    public Task<ApiResult<List<TenantModel>>> GetTenantsAsync(CancellationToken ct = default) =>
        SendAsync<List<TenantModel>>(HttpMethod.Get, "api/platform/tenants", null, ct);

    public Task<ApiResult<TenantModel>> GetTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        SendAsync<TenantModel>(HttpMethod.Get, $"api/platform/tenants/{tenantId}", null, ct);

    public Task<ApiResult<List<TenantUserModel>>> GetTenantUsersAsync(
        Guid tenantId, CancellationToken ct = default) =>
        SendAsync<List<TenantUserModel>>(
            HttpMethod.Get, $"api/platform/tenants/{tenantId}/users", null, ct);

    public Task<ApiResult<TenantModel>> CreateTenantAsync(
        CreateTenantRequest request, CancellationToken ct = default) =>
        SendAsync<TenantModel>(HttpMethod.Post, "api/platform/tenants", request, ct);

    public Task<ApiResult<TenantModel>> UpdateTenantStatusAsync(
        Guid tenantId, TenantStatus status, CancellationToken ct = default) =>
        SendAsync<TenantModel>(HttpMethod.Patch, $"api/platform/tenants/{tenantId}/status",
            new UpdateTenantStatusRequest(status), ct);

    /// <summary>
    /// The role-by-endpoint access matrix, read out of the API's own routing table. Platform
    /// operators only; the API enforces it.
    /// </summary>
    public Task<ApiResult<AccessMatrix>> GetAccessMatrixAsync(CancellationToken ct = default) =>
        SendAsync<AccessMatrix>(HttpMethod.Get, "api/platform/access", null, ct);

    public Task<ApiResult<ProvisionedUser>> CreateTenantAdminAsync(
        Guid tenantId, CreateTenantAdminRequest request, CancellationToken ct = default) =>
        SendAsync<ProvisionedUser>(
            HttpMethod.Post, $"api/platform/tenants/{tenantId}/admin-user", request, ct);

    // ── tenant ───────────────────────────────────────────────────────────────────────────

    public Task<ApiResult<List<TenantUserModel>>> GetUsersAsync(CancellationToken ct = default) =>
        SendAsync<List<TenantUserModel>>(HttpMethod.Get, "api/users", null, ct);

    public Task<ApiResult<ProvisionedUser>> CreateUserAsync(
        CreateTenantUserRequest request, CancellationToken ct = default) =>
        SendAsync<ProvisionedUser>(HttpMethod.Post, "api/users", request, ct);

    public Task<ApiResult<TenantUserModel>> SetUserActiveAsync(
        Guid membershipId, bool isActive, CancellationToken ct = default) =>
        SendAsync<TenantUserModel>(HttpMethod.Patch,
            $"api/users/{membershipId}/{(isActive ? "reactivate" : "deactivate")}", null, ct);

    public Task<ApiResult<MyProfile>> GetMyProfileAsync(CancellationToken ct = default) =>
        SendAsync<MyProfile>(HttpMethod.Get, "api/users/me", null, ct);

    // ── products (Module 2) ──────────────────────────────────────────────────────────────

    /// <summary>
    /// One page of products. <paramref name="listType"/> chooses between the two screens;
    /// both read the same table.
    /// </summary>
    public Task<ApiResult<ApiPage<ProductListItem>>> GetProductsAsync(
        ProductListType listType,
        string? search = null,
        ProductStatusFilter status = ProductStatusFilter.Active,
        bool antibioticOnly = false,
        ProductType? productType = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"type={listType}",
            $"status={status}",
            $"page={page}",
            $"pageSize={pageSize}",
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (antibioticOnly)
        {
            query.Add("antibioticOnly=true");
        }

        if (productType is not null)
        {
            query.Add($"productType={productType}");
        }

        return SendAsync<ApiPage<ProductListItem>>(
            HttpMethod.Get, $"api/products?{string.Join('&', query)}", null, ct);
    }

    public Task<ApiResult<ProductModel>> GetProductAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<ProductModel>(HttpMethod.Get, $"api/products/{id}", null, ct);

    public Task<ApiResult<ProductModel>> CreateProductAsync(
        CreateProductRequest request, CancellationToken ct = default) =>
        SendAsync<ProductModel>(HttpMethod.Post, "api/products", request, ct);

    public Task<ApiResult<ProductModel>> UpdateProductAsync(
        Guid id, UpdateProductRequest request, CancellationToken ct = default) =>
        SendAsync<ProductModel>(HttpMethod.Put, $"api/products/{id}", request, ct);

    public Task<ApiResult<ProductModel>> SetProductActiveAsync(
        Guid id, bool isActive, CancellationToken ct = default) =>
        SendAsync<ProductModel>(HttpMethod.Patch,
            $"api/products/{id}/{(isActive ? "reactivate" : "deactivate")}", null, ct);

    /// <summary>
    /// Imports many catalogue medicines at once. All or nothing: on any row failure nothing is
    /// created and the result carries a row-level reason for each.
    /// </summary>
    public Task<ApiResult<BulkImportResult>> BulkImportProductsAsync(
        BulkImportRequest request, CancellationToken ct = default) =>
        SendAsync<BulkImportResult>(HttpMethod.Post, "api/products/bulk-import", request, ct);

    /// <summary>Sets prices on existing products, several at a time. Also all or nothing.</summary>
    public Task<ApiResult<BulkImportResult>> SetProductPricesAsync(
        SetPricesRequest request, CancellationToken ct = default) =>
        SendAsync<BulkImportResult>(HttpMethod.Post, "api/products/prices", request, ct);

    // ── medicine reference catalogue ─────────────────────────────────────────────────────

    /// <summary>
    /// Forgiving search over the shared catalogue. Check <c>MatchType</c> before rendering:
    /// a Suggestion set must be labelled as approximate.
    /// </summary>
    public Task<ApiResult<CatalogSearchResult>> SearchCatalogAsync(
        string term, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        SendAsync<CatalogSearchResult>(
            HttpMethod.Get,
            $"api/catalog/medicines/search?q={Uri.EscapeDataString(term)}"
                + $"&page={page}&pageSize={pageSize}",
            null, ct);

    public Task<ApiResult<CatalogMedicineSearchItem>> GetCatalogMedicineAsync(
        int id, CancellationToken ct = default) =>
        SendAsync<CatalogMedicineSearchItem>(
            HttpMethod.Get, $"api/catalog/medicines/{id}", null, ct);

    // ── stock (Module 3) ─────────────────────────────────────────────────────────────────

    /// <summary>One page of the stock list — a row per product, aggregated across its batches.</summary>
    public Task<ApiResult<ApiPage<StockListItem>>> GetStockAsync(
        string? search = null,
        StockStatusFilter stockStatus = StockStatusFilter.All,
        ExpiryStatusFilter expiryStatus = ExpiryStatusFilter.All,
        StockProductTypeFilter productType = StockProductTypeFilter.All,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"stockStatus={stockStatus}",
            $"expiryStatus={expiryStatus}",
            $"productType={productType}",
            $"page={page}",
            $"pageSize={pageSize}",
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        return SendAsync<ApiPage<StockListItem>>(
            HttpMethod.Get, $"api/stock?{string.Join('&', query)}", null, ct);
    }

    /// <summary>
    /// One product's stock: summary, live batches in FEFO order, and a page of depleted ones.
    /// </summary>
    public Task<ApiResult<ProductStockModel>> GetProductStockAsync(
        Guid productId,
        int depletedPage = 1,
        int depletedPageSize = 10,
        CancellationToken ct = default) =>
        SendAsync<ProductStockModel>(
            HttpMethod.Get,
            $"api/stock/product/{productId}"
                + $"?depletedPage={depletedPage}&depletedPageSize={depletedPageSize}",
            null, ct);

    public Task<ApiResult<BatchModel>> GetBatchAsync(Guid batchId, CancellationToken ct = default) =>
        SendAsync<BatchModel>(HttpMethod.Get, $"api/stock/batch/{batchId}", null, ct);

    /// <summary>A batch's adjustment history. Admin or Pharmacist; the API enforces it.</summary>
    public Task<ApiResult<ApiPage<StockAdjustmentModel>>> GetBatchAdjustmentsAsync(
        Guid batchId, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        SendAsync<ApiPage<StockAdjustmentModel>>(
            HttpMethod.Get,
            $"api/stock/batch/{batchId}/adjustments?page={page}&pageSize={pageSize}",
            null, ct);

    public Task<ApiResult<BatchCreated>> CreateBatchAsync(
        CreateBatchRequest request, CancellationToken ct = default) =>
        SendAsync<BatchCreated>(HttpMethod.Post, "api/stock/batches", request, ct);

    public Task<ApiResult<BatchModel>> UpdateBatchAsync(
        Guid id, UpdateBatchRequest request, CancellationToken ct = default) =>
        SendAsync<BatchModel>(HttpMethod.Put, $"api/stock/batches/{id}", request, ct);

    public Task<ApiResult<BatchAdjusted>> AdjustBatchAsync(
        Guid id, AdjustBatchRequest request, CancellationToken ct = default) =>
        SendAsync<BatchAdjusted>(HttpMethod.Post, $"api/stock/batches/{id}/adjust", request, ct);

    // ── billing (Module 5) ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The billing screen type-ahead. Unsellable products come back with a reason attached
    /// rather than being filtered out — the screen greys them and says why.
    /// </summary>
    public Task<ApiResult<List<SellableProduct>>> SearchSellableAsync(
        string? search, int limit = 0, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (limit > 0)
        {
            query.Add($"limit={limit}");
        }

        var suffix = query.Count > 0 ? "?" + string.Join('&', query) : string.Empty;

        return SendAsync<List<SellableProduct>>(
            HttpMethod.Get, $"api/products/sellable{suffix}", null, ct);
    }

    public Task<ApiResult<BillingLimits>> GetBillingLimitsAsync(CancellationToken ct = default) =>
        SendAsync<BillingLimits>(HttpMethod.Get, "api/sales/limits", null, ct);

    public Task<ApiResult<SaleCompleted>> CompleteSaleAsync(
        CompleteSalePayload payload, CancellationToken ct = default) =>
        SendAsync<SaleCompleted>(HttpMethod.Post, "api/sales", payload, ct);

    public Task<ApiResult<ApiPage<SaleListItem>>> GetSalesAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? cashier = null,
        SaleStatusFilter status = SaleStatusFilter.All,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"status={status}",
            $"page={page}",
            $"pageSize={pageSize}",
        };

        if (from is { } fromDate)
        {
            query.Add($"from={fromDate:yyyy-MM-dd}");
        }

        if (to is { } toDate)
        {
            query.Add($"to={toDate:yyyy-MM-dd}");
        }

        if (cashier is { } cashierId)
        {
            query.Add($"cashier={cashierId}");
        }

        return SendAsync<ApiPage<SaleListItem>>(
            HttpMethod.Get, $"api/sales?{string.Join('&', query)}", null, ct);
    }

    public Task<ApiResult<List<CashierOption>>> GetCashiersAsync(CancellationToken ct = default) =>
        SendAsync<List<CashierOption>>(HttpMethod.Get, "api/sales/cashiers", null, ct);

    public Task<ApiResult<SaleDetail>> GetSaleAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<SaleDetail>(HttpMethod.Get, $"api/sales/{id}", null, ct);

    public Task<ApiResult<ReturnableSale>> GetReturnableLinesAsync(
        Guid id, CancellationToken ct = default) =>
        SendAsync<ReturnableSale>(HttpMethod.Get, $"api/sales/{id}/returnable-lines", null, ct);

    public Task<ApiResult<SalesReturned>> CreateReturnAsync(
        Guid saleId, CreateReturnPayload payload, CancellationToken ct = default) =>
        SendAsync<SalesReturned>(HttpMethod.Post, $"api/sales/{saleId}/returns", payload, ct);

    public Task<ApiResult<CancelledSale>> CancelSaleAsync(
        Guid saleId, string reason, CancellationToken ct = default) =>
        SendAsync<CancelledSale>(
            HttpMethod.Post, $"api/sales/{saleId}/cancel", new CancelSalePayload(reason), ct);

    // ── alerts (Module 6) ────────────────────────────────────────────────────────────────

    public Task<ApiResult<AlertSummary>> GetAlertSummaryAsync(CancellationToken ct = default) =>
        SendAsync<AlertSummary>(HttpMethod.Get, "api/alerts/summary", null, ct);

    public Task<ApiResult<ApiPage<ExpiringBatch>>> GetExpiringAsync(
        int? days = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };

        if (days is { } window)
        {
            query.Add($"days={window}");
        }

        return SendAsync<ApiPage<ExpiringBatch>>(
            HttpMethod.Get, $"api/alerts/expiring?{string.Join('&', query)}", null, ct);
    }

    public Task<ApiResult<ApiPage<ExpiringBatch>>> GetExpiredAsync(
        int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        SendAsync<ApiPage<ExpiringBatch>>(
            HttpMethod.Get, $"api/alerts/expired?page={page}&pageSize={pageSize}", null, ct);

    public Task<ApiResult<ApiPage<LowStockProduct>>> GetLowStockAsync(
        ProductType? productType = null,
        LowStockStatusFilter status = LowStockStatusFilter.All,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"status={status}",
            $"page={page}",
            $"pageSize={pageSize}",
        };

        if (productType is { } type)
        {
            query.Add($"productType={type}");
        }

        return SendAsync<ApiPage<LowStockProduct>>(
            HttpMethod.Get, $"api/alerts/low-stock?{string.Join('&', query)}", null, ct);
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            message.Content = JsonContent.Create(body, options: Json);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(message, ct);
        }
        catch (HttpRequestException ex)
        {
            // The API being down is not the user's problem to read a stack trace about.
            _logger.LogError(ex, "The API could not be reached for {Method} {Path}.", method, path);

            return ApiResult<T>.Fail(new ApiProblem(
                "Service unavailable",
                "The service is temporarily unavailable. Please try again in a moment.",
                (int)HttpStatusCode.ServiceUnavailable,
                "Api.Unreachable"));
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "The API timed out for {Method} {Path}.", method, path);

            return ApiResult<T>.Fail(new ApiProblem(
                "Timed out",
                "The service took too long to respond. Please try again.",
                (int)HttpStatusCode.GatewayTimeout,
                "Api.Timeout"));
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                // 204 and an empty 200 are both legitimate; T is then whatever default means.
                if (response.StatusCode == HttpStatusCode.NoContent
                    || response.Content.Headers.ContentLength is 0)
                {
                    return ApiResult<T>.Ok(default!);
                }

                var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);

                return value is null
                    ? ApiResult<T>.Fail(new ApiProblem(
                        "Unexpected response", "The service returned no data.", 502, "Api.EmptyBody"))
                    : ApiResult<T>.Ok(value);
            }

            var problem = await ReadProblemAsync(response, ct);

            // 5xx detail is logged, never rendered — it can carry internals.
            if (problem.Failure == ApiFailure.ServerError)
            {
                _logger.LogError(
                    "API returned {Status} for {Method} {Path}: {Detail}",
                    problem.Status, method, path, problem.Detail);
            }

            return ApiResult<T>.Fail(problem);
        }
    }

    private static async Task<ApiProblem> ReadProblemAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(Json, ct);
            if (problem is not null && (problem.Detail ?? problem.Title) is not null)
            {
                // The body's own status can be absent; the response's never is.
                return problem.Status is null
                    ? problem with { Status = (int)response.StatusCode }
                    : problem;
            }
        }
        catch (JsonException)
        {
            // Not a ProblemDetails body — fall through to a status-based message.
        }
        catch (NotSupportedException)
        {
            // Not JSON at all (an HTML error page from a proxy, say).
        }

        return new ApiProblem(
            response.StatusCode.ToString(),
            response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your session has ended. Please sign in again.",
                HttpStatusCode.Forbidden => "You do not have permission to do that.",
                HttpStatusCode.NotFound => "That item no longer exists.",
                _ => "The request could not be completed.",
            },
            (int)response.StatusCode,
            null);
    }
}
