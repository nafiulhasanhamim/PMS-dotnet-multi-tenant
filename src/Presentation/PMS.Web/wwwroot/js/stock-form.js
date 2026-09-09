// Stock forms: unit dropdowns, live conversions, and the guards.
//
// No jQuery, no framework. Everything here is a convenience over a form that already works
// without it: the unit dropdowns are rendered server-side from the selected product, and every
// rule this file expresses is also enforced by the API. If this file fails to load, the form
// still submits and the server still refuses what it should.
(function () {
    "use strict";

    function num(value) {
        var parsed = parseFloat(value);
        return isNaN(parsed) ? 0 : parsed;
    }

    function plural(noun, count) {
        if (count === 1 || !noun) { return noun; }
        var lower = noun.toLowerCase();
        if (/(s|x|z|ch|sh)$/.test(lower)) { return noun + "es"; }
        if (/[^aeiou]y$/.test(lower)) { return noun.slice(0, -1) + "ies"; }
        return noun + "s";
    }

    // ── The product picker on the add form ───────────────────────────────────────────────
    //
    // A filter box over a real <select> rather than a custom autocomplete widget: it keeps
    // keyboard behaviour and screen-reader support for free, and a pharmacy has hundreds of
    // products, not thousands.
    function wireProductFilter() {
        var filter = document.querySelector("[data-stock-product-filter]");
        var select = document.getElementById("productId");
        if (!filter || !select) { return; }

        var all = Array.prototype.slice.call(select.options).map(function (option) {
            return { value: option.value, text: option.text, search: option.text.toLowerCase() };
        });

        filter.addEventListener("input", function () {
            var term = filter.value.trim().toLowerCase();
            var selected = select.value;

            select.innerHTML = "";
            all.forEach(function (item) {
                if (term && item.value && item.search.indexOf(term) === -1) { return; }
                var option = document.createElement("option");
                option.value = item.value;
                option.text = item.text;
                if (item.value === selected) { option.selected = true; }
                select.appendChild(option);
            });
        });
    }

    // Changing the product reloads the page, because the unit dropdowns, the expiry
    // requirement and the existing-batch panel all come from the server for that product.
    // Rebuilding them client-side would be a second implementation of rules the server
    // already owns.
    function wireProductReload() {
        var select = document.getElementById("productId");
        if (!select || !select.hasAttribute("data-stock-reload-on-change")) { return; }

        select.addEventListener("change", function () {
            if (!select.value) { return; }
            var form = select.getAttribute("data-stock-reload-form");
            var target = form ? document.getElementById(form) : select.form;
            if (target) { target.submit(); }
        });
    }

    // ── Live conversions ─────────────────────────────────────────────────────────────────

    function unitsPerSelected(select) {
        if (!select) { return 1; }
        var option = select.options[select.selectedIndex];
        return option ? num(option.getAttribute("data-base-units")) || 1 : 1;
    }

    function unitNameOf(select) {
        if (!select) { return ""; }
        var option = select.options[select.selectedIndex];
        return option ? (option.getAttribute("data-unit-name") || option.text) : "";
    }

    // "= 200 pieces total". The single best defence against a mistyped pack size: 20 strips
    // and 200 strips look almost identical in a number input and unmistakable in this line.
    function wireQuantityHelper() {
        var input = document.querySelector("[data-stock-quantity]");
        var unit = document.querySelector("[data-stock-quantity-unit]");
        var output = document.querySelector("[data-stock-quantity-help]");
        if (!input || !unit || !output) { return; }

        var baseUnit = output.getAttribute("data-base-unit") || "units";

        function update() {
            var entered = num(input.value);
            var total = entered * unitsPerSelected(unit);

            if (!entered) {
                output.textContent = "";
                return;
            }

            if (total !== Math.floor(total)) {
                // Refused by the server rather than rounded, so say so before the round trip:
                // silently turning half a strip into 0 or 1 would lose or invent stock.
                output.textContent = "That is not a whole number of "
                    + plural(baseUnit, 2) + ".";
                output.className = "pms-form-help text-danger";
                return;
            }

            output.textContent = "= " + total + " " + plural(baseUnit, total) + " total";
            output.className = "pms-form-help";
        }

        input.addEventListener("input", update);
        unit.addEventListener("change", update);
        update();
    }

    // "= ৳0.80 per piece", and the loss warning.
    function wirePriceHelper() {
        var input = document.querySelector("[data-stock-price]");
        var unit = document.querySelector("[data-stock-price-unit]");
        var output = document.querySelector("[data-stock-price-help]");
        if (!input || !unit || !output) { return; }

        var baseUnit = output.getAttribute("data-base-unit") || "unit";
        var salePrice = num(output.getAttribute("data-sale-price"));
        var warning = document.querySelector("[data-stock-loss-warning]");

        function update() {
            var entered = num(input.value);

            if (!input.value) {
                output.textContent = "";
                if (warning) { warning.hidden = true; }
                return;
            }

            var perBase = entered / (unitsPerSelected(unit) || 1);
            output.textContent = "= " + perBase.toFixed(4).replace(/0+$/, "").replace(/\.$/, "")
                + " per " + baseUnit;

            // Non-blocking. A pharmacy really does buy above its own list price sometimes and
            // reprice afterwards; refusing the entry would leave the stock unrecorded, which
            // is worse than recording it with a warning attached.
            if (warning) {
                var atALoss = salePrice > 0 && perBase > salePrice;
                warning.hidden = !atALoss;
                if (atALoss) {
                    var text = warning.querySelector("[data-stock-loss-text]");
                    if (text) {
                        text.textContent = "This batch would cost " + perBase.toFixed(2)
                            + " per " + baseUnit + " but sells for " + salePrice.toFixed(2)
                            + ". It would sell at a loss.";
                    }
                }
            }
        }

        input.addEventListener("input", update);
        unit.addEventListener("change", update);
        update();
    }

    // ── The adjustment form ──────────────────────────────────────────────────────────────

    function wireQuickReasons() {
        var field = document.getElementById("reason");
        if (!field) { return; }

        document.querySelectorAll("[data-stock-reason]").forEach(function (button) {
            button.addEventListener("click", function () {
                field.value = button.getAttribute("data-stock-reason");
                // Left editable on purpose: the quick pick is a starting point, and the
                // detail somebody adds after it is the part worth reading in six months.
                field.focus();
            });
        });
    }

    // "Set correct quantity" means the number entered IS the new total, so the label and the
    // live preview have to change with it - otherwise somebody types 175 meaning "remove 175".
    //
    // The kind is read from data-adjust-kind and not from the radio's value. The values are the
    // enum names, because that is what the tag helper compares to decide which radio is
    // checked; an earlier version of this compared them to "0"/"1"/"2" and so never matched,
    // which left the field labelled "How much to add" while Remove was selected and previewed
    // a removal as an addition.
    function wireAdjustmentMode() {
        var radios = document.querySelectorAll("[data-stock-adjust-type]");
        var label = document.querySelector("[data-stock-adjust-label]");
        var preview = document.querySelector("[data-stock-adjust-preview]");
        var input = document.querySelector("[data-stock-quantity]");
        var unit = document.querySelector("[data-stock-quantity-unit]");
        if (!radios.length) { return; }

        var current = preview ? num(preview.getAttribute("data-current")) : 0;
        var baseUnit = preview ? (preview.getAttribute("data-base-unit") || "units") : "units";

        function selectedKind() {
            var checked = document.querySelector("[data-stock-adjust-type]:checked");
            return checked ? (checked.getAttribute("data-adjust-kind") || "add") : "add";
        }

        function update() {
            var kind = selectedKind();

            if (label) {
                label.textContent = kind === "correction"
                    ? "What is the correct quantity?"
                    : (kind === "remove" ? "How much to remove" : "How much to add");
            }

            if (!preview || !input) { return; }

            var entered = num(input.value) * unitsPerSelected(unit);

            if (!input.value) {
                preview.textContent = "";
                return;
            }

            var after = kind === "correction"
                ? entered
                : (kind === "remove" ? current - entered : current + entered);

            if (after < 0) {
                preview.textContent = "There are only " + current + " "
                    + plural(baseUnit, current) + " in this batch.";
                preview.className = "pms-form-help text-danger";
                return;
            }

            preview.textContent = current + " -> " + after + " " + plural(baseUnit, after)
                + (kind === "correction" ? " (a change of " + (after - current) + ")" : "");
            preview.className = "pms-form-help";
        }

        radios.forEach(function (radio) { radio.addEventListener("change", update); });
        if (input) { input.addEventListener("input", update); }
        if (unit) { unit.addEventListener("change", update); }
        update();
    }

    document.addEventListener("DOMContentLoaded", function () {
        wireProductFilter();
        wireProductReload();
        wireQuantityHelper();
        wirePriceHelper();
        wireQuickReasons();
        wireAdjustmentMode();
    });
})();
