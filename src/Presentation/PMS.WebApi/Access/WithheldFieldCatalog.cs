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
            "Selling an antibiotic — only while the pharmacy is in Required mode",
            "Employee",
            "Conditional since Module 7, and the condition matters: this restriction applies "
            + "ONLY when the pharmacy's AntibioticPrescriptionMode is Required. Under Off (the "
            + "default) and Optional an Employee dispenses an antibiotic like any other "
            + "product. A policy could not express it either way, because it depends on the "
            + "cart and on a per-tenant setting rather than on the endpoint — so the till is "
            + "open to every role and the cart is what gets refused, with 403.",
            "BillingPolicy.MaySellAntibiotics, called by CompleteSaleCommandHandler.Blocked",
            WithheldKind.Action),

        // ── Module 7 ─────────────────────────────────────────────────────────────────────

        new WithheldFieldDto(
            "Antibiotic register",
            "The register and its CSV export",
            "Employee",
            "The deliberate asymmetry of the module: an Employee may be able to SELL an "
            + "antibiotic — that depends on the pharmacy's mode, see above — but never to read "
            + "the register. Selling is counter work; the register is the regulatory record of "
            + "what colleagues dispensed and to which named patients, which is oversight. This "
            + "one is a policy rather than a projection, so it also appears in the derived "
            + "matrix; it is declared here because the asymmetry is the thing somebody "
            + "reviewing access would otherwise read as a mistake.",
            "AntibioticsController [Authorize(TenantWriterPolicy)]",
            WithheldKind.Action),

        new WithheldFieldDto(
            "Pharmacy settings",
            "Changing the antibiotic prescription mode",
            "Employee, Pharmacist",
            "Reading the mode is open to every role — the till needs it on every load to know "
            + "whether to draw the prescription panel. Changing it alters what staff may sell "
            + "and what the pharmacy records against the law, which is an owner's decision.",
            "SettingsController.SetAntibioticMode [Authorize(TenantAdminPolicy)]",
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
