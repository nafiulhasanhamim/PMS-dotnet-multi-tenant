namespace PMS.WebApi.Access;

/// <summary>
/// Fields withheld from a role <em>inside</em> a response, rather than by refusing the request.
///
/// <para><b>This file is maintained by hand, and it is the only part of the access matrix that
/// is.</b> Everything else on the platform access page is read out of the routing table by
/// <see cref="AccessMatrixBuilder"/> and cannot fall behind the code. This cannot be derived:
/// "the projection does not read the cost column when the caller is an Employee" is a fact
/// about a handler, invisible to routing.</para>
///
/// <para>It lives in its own file for that reason — so that the thing somebody has to remember
/// to update is not buried inside the thing that updates itself.</para>
///
/// <para><b>Forgetting is a build failure, not a silent gap.</b>
/// <c>WithheldFieldCatalogTests</c> finds every <c>CallerMaySee*</c> property on every API
/// controller — the established way a role gates a field — and fails if one is not named in an
/// entry below. So a new role-gated field is caught, provided it follows that convention.
/// A field gated some entirely different way is the one case still able to slip past, which is
/// the reason the convention is worth keeping to.</para>
/// </summary>
public static class WithheldFieldCatalog
{
    /// <summary>
    /// Every declared restriction. <see cref="WithheldFieldDto.EnforcedIn"/> must name the
    /// controller property that decides it, both so a reader can check the claim and so the
    /// guarding test can match the two up.
    /// </summary>
    public static readonly IReadOnlyList<WithheldFieldDto> Entries =
    [
        new WithheldFieldDto(
            "Product catalogue",
            "Sale price, in the product list",
            "Employee",
            "An Employee at the counter needs to answer \"how much is this?\" for one product, "
            + "which the detail page still tells them. What they should not have is the whole "
            + "price list in a single download.",
            "ProductsController.CallerMaySeeListPrices → GetProductsQuery.IncludePrices"),

        new WithheldFieldDto(
            "Stock and batches",
            "Purchase price, everywhere it appears",
            "Employee",
            "What the pharmacy paid, and so its margin. Nobody at the counter needs it. "
            + "Stricter than the sale price above, which an Employee can see on a detail page.",
            "StockController.CallerMaySeePurchasePrices → GetProductStockQuery, GetBatchQuery"),

        // ── Module 5. Neither of these is a column, which is why WithheldKind exists ──────

        new WithheldFieldDto(
            "Sales",
            "Other people's sales, in the list and by direct id",
            "Employee",
            "Counter staff can see what they rang up and not what their colleagues did — a "
            + "day's takings across the shop is the owner's information. Applied inside the "
            + "query rather than to its results, so the row count is theirs too and there is "
            + "no page to scroll to that holds somebody else's sales.",
            "GetSalesQueryHandler (restrictTo) and GetSaleQueryHandler (403 on another "
            + "cashier's invoice)",
            WithheldKind.Rows),

        new WithheldFieldDto(
            "Sales",
            "Selling any product marked as an antibiotic",
            "Employee",
            "Dispensing an antibiotic needs a pharmacist. A policy cannot express it because "
            + "it depends on what is in the cart, not on the endpoint — so the till is open to "
            + "every role and the cart is what gets refused, with 403 and a message telling "
            + "them to call a pharmacist over.",
            "CompleteSaleCommandHandler.Blocked (Error.Forbidden per antibiotic product)",
            WithheldKind.Action),

        new WithheldFieldDto(
            "Sales",
            "Discounting beyond the role's cap",
            "Employee, Pharmacist",
            "A discount moves money out of the business with no stock leaving the shelf, so it "
            + "is capped: 5% for an Employee, 10% for a Pharmacist, unlimited for an Admin. "
            + "Flat amounts are measured against the same percentage of the subtotal, or the "
            + "cap would be a formatting preference rather than a control.",
            "BillingPolicy.IsDiscountAllowed, called by CompleteSaleCommandHandler",
            WithheldKind.Action),
    ];
}
