// Module 4 — the multi-line purchase form.
//
// Three jobs, and one deliberate non-job.
//
//   1. Add and remove rows, renumbering the indexed input names so the collection binds.
//   2. Rebuild each row's two unit dropdowns from the chosen product's own configuration —
//      Napa offers piece/strip/box, the handwash bottle/carton, saline bag only. A shared unit
//      list would offer a strip of handwash.
//   3. Show live helper text and a running total.
//
// The non-job: NOTHING here is authoritative. Quantity and price are posted in the unit the
// person chose, exactly as Add Stock posts them, and the server converts through Module 2's
// helpers. The "= 200 pieces" text is a convenience; if it ever disagreed with the server the
// server would still be right, and nothing computed here is stored.
//
// The form works without this script, just less pleasantly: the rows the server rendered still
// post, the selects still submit, and every rule that matters is enforced server-side.

(function () {
    'use strict';

    var form = document.getElementById('purchase-form');

    if (!form) {
        return;
    }

    var linesHost = document.getElementById('purchase-lines');
    var template = document.getElementById('purchase-line-template');
    var addButton = document.getElementById('add-line');
    var totalOutput = document.getElementById('purchase-total');

    var products = {};

    try {
        (JSON.parse(form.dataset.products || '[]') || []).forEach(function (p) {
            products[p.id] = p;
        });
    } catch (e) {
        // A malformed payload must not take the form down: without the catalogue the unit
        // dropdowns fall back to the base unit only, and the server still validates everything.
        products = {};
    }

    // UnitLevel on the server: Base = 0, Mid = 1, Large = 2. These values go on the wire.
    var BASE = 0;
    var MID = 1;
    var LARGE = 2;

    function money(value) {
        return (Math.round(value * 100) / 100).toLocaleString('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function plural(word, count) {
        if (!word) {
            return '';
        }

        return count === 1 ? word : word + 's';
    }

    /// How many base units one of the given level contains, for the chosen product.
    function baseUnitsIn(product, level) {
        if (!product) {
            return 1;
        }

        if (level === LARGE) {
            return product.baseUnitsPerLarge || 1;
        }

        if (level === MID) {
            return product.basePerMid || 1;
        }

        return 1;
    }

    /// The levels this product actually defines. A product with no mid unit must not offer one.
    function unitOptions(product) {
        var options = [{ value: BASE, label: product ? product.baseUnitName : 'unit' }];

        if (product && product.midUnitName && product.basePerMid) {
            options.push({ value: MID, label: product.midUnitName });
        }

        if (product && product.largeUnitName && product.baseUnitsPerLarge) {
            options.push({ value: LARGE, label: product.largeUnitName });
        }

        return options;
    }

    function fillUnitSelect(select, product) {
        var previous = select.value;

        select.innerHTML = '';

        unitOptions(product).forEach(function (option) {
            var element = document.createElement('option');
            element.value = option.value;
            element.textContent = option.label;
            select.appendChild(element);
        });

        // Keep the level if the new product still has it; otherwise fall back to base, which
        // every product has.
        var stillValid = Array.prototype.some.call(select.options, function (o) {
            return o.value === previous;
        });

        select.value = stillValid ? previous : String(BASE);
    }

    function selectedProduct(row) {
        var select = row.querySelector('.line-product');

        return select && select.value ? products[select.value] : null;
    }

    function refreshRow(row) {
        var product = selectedProduct(row);

        var quantityUnit = row.querySelector('.line-quantity-unit');
        var priceUnit = row.querySelector('.line-price-unit');

        fillUnitSelect(quantityUnit, product);
        fillUnitSelect(priceUnit, product);

        var quantity = parseFloat(row.querySelector('.line-quantity').value) || 0;
        var price = parseFloat(row.querySelector('.line-price').value) || 0;

        var quantityInBase = quantity * baseUnitsIn(product, parseInt(quantityUnit.value, 10));
        var pricePerBase = price / baseUnitsIn(product, parseInt(priceUnit.value, 10));

        var quantityHelp = row.querySelector('.line-quantity-help');
        var priceHelp = row.querySelector('.line-price-help');

        if (product && quantity > 0) {
            quantityHelp.textContent =
                '= ' + quantityInBase + ' ' + plural(product.baseUnitName, quantityInBase);
        } else {
            quantityHelp.textContent = '';
        }

        if (product && price > 0) {
            priceHelp.textContent =
                '= ' + money(pricePerBase) + ' per ' + product.baseUnitName;
        } else {
            priceHelp.textContent = '';
        }

        var lineTotal = quantityInBase * pricePerBase;

        row.querySelector('.line-total').textContent = money(lineTotal || 0);

        // Expiry is required for a medicine and optional otherwise — Module 3's rule, reflected
        // here per row once a product is chosen. The server enforces it either way; this is so
        // somebody is not told about it only after pressing save.
        var isMedicine = product && product.productType === 0;
        var requiredMark = row.querySelector('.line-expiry-required');
        var expiryHelp = row.querySelector('.line-expiry-help');
        var expiryInput = row.querySelector('.line-expiry');

        if (requiredMark) {
            requiredMark.hidden = !isMedicine;
        }

        if (expiryHelp) {
            expiryHelp.textContent = product
                ? (isMedicine ? 'Required for a medicine.' : 'Optional for this item.')
                : '';
        }

        if (expiryInput) {
            expiryInput.required = !!isMedicine;
        }

        // Non-blocking loss warning: cost per base unit above the sale price per base unit.
        var warning = row.querySelector('.line-loss-warning');

        if (warning) {
            var sells = product && typeof product.pricePerBase === 'number'
                ? product.pricePerBase
                : null;

            if (sells !== null && pricePerBase > sells && price > 0) {
                warning.hidden = false;
                warning.textContent =
                    'This costs ' + money(pricePerBase) + ' per ' + product.baseUnitName
                    + ' but sells at ' + money(sells) + '. It would sell at a loss.';
            } else {
                warning.hidden = true;
                warning.textContent = '';
            }
        }

        return lineTotal || 0;
    }

    function refreshAll() {
        var total = 0;

        rows().forEach(function (row) {
            total += refreshRow(row);
        });

        totalOutput.textContent = money(total);
    }

    function rows() {
        return Array.prototype.slice.call(
            linesHost.querySelectorAll('.purchase-line'));
    }

    /// Renumbers every row's input names after an add or a remove.
    ///
    /// Without this, removing the middle of three rows would leave Input.Lines[0] and
    /// Input.Lines[2] — and the model binder stops at the first gap, silently dropping the
    /// third row from the purchase.
    function renumber() {
        rows().forEach(function (row, index) {
            row.dataset.index = index;

            row.querySelectorAll('[name]').forEach(function (field) {
                field.name = field.name.replace(/Input\.Lines\[\d+\]/, 'Input.Lines[' + index + ']');
            });

            row.querySelectorAll('[id]').forEach(function (field) {
                var newId = field.id.replace(/-\d+$/, '-' + index);
                var label = row.querySelector('label[for="' + field.id + '"]');

                field.id = newId;

                if (label) {
                    label.setAttribute('for', newId);
                }
            });
        });

        // A single row cannot be removed: a purchase with no items is not a purchase, and the
        // server refuses it anyway. Disabling is clearer than an error after the fact.
        var only = rows().length === 1;

        rows().forEach(function (row) {
            row.querySelector('.remove-line').disabled = only;
        });
    }

    function filterSelect(select, term) {
        var needle = (term || '').trim().toLowerCase();

        Array.prototype.forEach.call(select.options, function (option) {
            if (!option.value) {
                return;
            }

            option.hidden = needle.length > 0
                && option.textContent.toLowerCase().indexOf(needle) === -1;
        });
    }

    addButton.addEventListener('click', function () {
        var markup = template.innerHTML.replace(/__INDEX__/g, String(rows().length));
        var holder = document.createElement('div');

        holder.innerHTML = markup.trim();

        var row = holder.firstElementChild;

        linesHost.appendChild(row);
        renumber();
        refreshAll();

        var productSelect = row.querySelector('.line-product');

        if (productSelect) {
            productSelect.focus();
        }
    });

    // Delegated, so rows added later need no wiring of their own.
    linesHost.addEventListener('click', function (event) {
        var button = event.target.closest('.remove-line');

        if (!button || button.disabled) {
            return;
        }

        var row = button.closest('.purchase-line');

        if (row && rows().length > 1) {
            row.remove();
            renumber();
            refreshAll();
        }
    });

    linesHost.addEventListener('input', function (event) {
        if (event.target.classList.contains('line-product-filter')) {
            var row = event.target.closest('.purchase-line');
            filterSelect(row.querySelector('.line-product'), event.target.value);
            return;
        }

        refreshAll();
    });

    linesHost.addEventListener('change', function () {
        refreshAll();
    });

    var supplierFilter = document.getElementById('supplier-filter');
    var supplierSelect = document.getElementById('supplier-select');

    if (supplierFilter && supplierSelect) {
        supplierFilter.addEventListener('input', function () {
            filterSelect(supplierSelect, supplierFilter.value);
        });
    }

    renumber();
    refreshAll();
})();
