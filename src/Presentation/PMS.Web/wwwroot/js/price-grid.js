// The price grid: copy-down, and keeping the save button honest.
//
// Everything here is a convenience over a form that works without it. The server counts the
// missing prices too and refuses "Save all" if any are blank, so a browser with scripting off
// gets a message instead of a silent half-save.
(function () {
    "use strict";

    function prices(column) {
        return Array.prototype.slice.call(
            document.querySelectorAll('[data-price-column="' + column + '"]'));
    }

    // Fills a column from its first filled value down.
    //
    // Identical values, not proportional. A proportional fill would need a reference pack size
    // per row and would quietly produce a different number for every row - which is impossible
    // to check at a glance on fifty rows, and this control exists to save checking.
    function wireCopyDown() {
        document.querySelectorAll("[data-copy-down]").forEach(function (button) {
            button.addEventListener("click", function () {
                var column = button.getAttribute("data-copy-down");
                var inputs = prices(column);
                if (!inputs.length) { return; }

                var source = null;
                for (var i = 0; i < inputs.length; i++) {
                    if (inputs[i].value !== "") { source = inputs[i].value; break; }
                }

                if (source === null) {
                    inputs[0].focus();
                    return;
                }

                inputs.forEach(function (input) { input.value = source; });
                updateMissing();
            });
        });
    }

    // Counts rows with an empty price box and keeps the hint and the button in step. The
    // wording matches the server's refusal, so somebody who posts anyway sees the same number.
    function updateMissing() {
        var inputs = Array.prototype.slice.call(
            document.querySelectorAll(".pms-grid__price"));
        if (!inputs.length) { return; }

        var rows = {};
        inputs.forEach(function (input) {
            var row = input.closest("tr");
            if (!row) { return; }
            var key = row.rowIndex;
            if (!(key in rows)) { rows[key] = true; }
            if (input.value === "") { rows[key] = false; }
        });

        var missing = Object.keys(rows).filter(function (key) { return !rows[key]; }).length;

        var hint = document.querySelector("[data-missing-hint]");
        var saveAll = document.querySelector("[data-save-all]");

        // The whole sentence is rewritten, not just the digit. Swapping the number alone is
        // how "1 items are missing prices" happens - and one item is exactly the case
        // somebody is looking at closely, because it is the last one they have to fix.
        if (hint) {
            var singular = hint.getAttribute("data-noun-singular") || "item";
            var plural = hint.getAttribute("data-noun-plural") || (singular + "s");
            var suffix = hint.getAttribute("data-hint-suffix") || "";

            hint.textContent = missing + " " + (missing === 1 ? singular : plural)
                + (missing === 1 ? " is " : " are ") + suffix;
            hint.hidden = missing === 0;
        }

        if (saveAll) { saveAll.disabled = missing > 0; }
    }

    // "Save without prices" goes through the same form and the same handler; only a hidden
    // flag differs. Set before the confirmation modal opens, because the modal submits with
    // form.submit(), which carries no button value of its own.
    function wireFieldSetters() {
        document.querySelectorAll("[data-set-field]").forEach(function (trigger) {
            trigger.addEventListener("click", function () {
                trigger.getAttribute("data-set-field").split("&").forEach(function (pair) {
                    var parts = pair.split("=");
                    var field = document.querySelector('[name="' + parts[0] + '"]');
                    if (field) { field.value = parts[1]; }
                });
            });
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        wireCopyDown();
        wireFieldSetters();

        document.querySelectorAll(".pms-grid__price").forEach(function (input) {
            input.addEventListener("input", updateMissing);
        });

        updateMissing();
    });
})();
