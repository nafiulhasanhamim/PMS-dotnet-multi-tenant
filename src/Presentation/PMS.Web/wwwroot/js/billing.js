/*
    The till.

    Three jobs, and it is worth being clear which is which:

      1. Search. Debounced fetch to the page's own Search handler, which proxies the API so the
         browser never holds a token. Results render as a listbox the cashier can arrow
         through; an unsellable product appears dimmed with its reason rather than missing.

      2. The cart. Rows are real form inputs, added and removed here and posted by an ordinary
         form submit. No JSON body: the antiforgery token, the validation redisplay and the
         redirect-on-success all work the way they do on every other form in this app.

      3. The live bill. Subtotal, discount, net and change, recomputed on every keystroke
         because a round trip per digit would make the screen feel broken.

    NONE OF (3) IS SENT. The form posts product ids, quantities and unit levels. The server
    prices the sale from the products, splits the discount with its own arithmetic and works out
    the change. What is on screen is a preview of a calculation performed somewhere the customer
    cannot reach — so this file being wrong is a display bug, never a pricing one.
*/
(function () {
    'use strict';

    var form = document.getElementById('billing-form');
    if (!form) {
        return;
    }

    var search = document.getElementById('product-search');
    var results = document.getElementById('search-results');
    var template = document.getElementById('cart-row-template');
    var body = document.getElementById('cart-body');
    var empty = document.getElementById('cart-empty');
    var prescription = document.getElementById('prescription-panel');
    var completeButton = document.getElementById('complete-sale');
    var blockedNote = form.querySelector('[data-complete-blocked]');

    var discountType = form.querySelector('[data-discount-type]');
    var discountValue = form.querySelector('[data-discount-value]');
    var discountError = form.querySelector('[data-discount-error]');
    var cashInput = form.querySelector('[data-cash-received]');
    var cashError = form.querySelector('[data-cash-error]');
    var capNote = document.getElementById('discount-cap');

    // Null means unlimited (an Admin). The value comes from the API, which is also what
    // enforces it — see BillingLimits.
    var maxDiscountPercent = null;
    if (capNote && capNote.dataset.maxDiscountPercent) {
        maxDiscountPercent = parseFloat(capNote.dataset.maxDiscountPercent);
    }

    var UNIT_LEVELS = { Base: 0, Mid: 1, Large: 2 };
    var nextIndex = body ? body.querySelectorAll('[data-cart-row]').length : 0;
    var options = [];
    var highlighted = -1;
    var searchTimer = null;

    // ── money ────────────────────────────────────────────────────────────────────────────

    /*
        Two decimals, away from zero — matching SaleMath.Round on the server, so the preview
        agrees with the receipt. JavaScript has no decimal type, so this is binary floating
        point rounded at the end; over a cart of a few items the error cannot reach a paisa,
        and the figure that gets charged is the server's regardless.
    */
    function round2(value) {
        if (!isFinite(value)) {
            return 0;
        }

        return Math.round((value + Number.EPSILON) * 100) / 100;
    }

    function money(value) {
        return round2(value).toLocaleString('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function number(input) {
        var parsed = parseFloat(input && input.value);
        return isFinite(parsed) ? parsed : 0;
    }

    // ── search ───────────────────────────────────────────────────────────────────────────

    function closeResults() {
        results.hidden = true;
        results.innerHTML = '';
        options = [];
        highlighted = -1;
        search.setAttribute('aria-expanded', 'false');
        search.removeAttribute('aria-activedescendant');
    }

    function renderResults(rows) {
        results.innerHTML = '';
        options = rows;
        highlighted = -1;

        if (!rows.length) {
            var none = document.createElement('li');
            none.className = 'pms-search__option pms-search__option--blocked';
            none.textContent = 'Nothing matched that.';
            results.appendChild(none);
            results.hidden = false;
            search.setAttribute('aria-expanded', 'true');
            return;
        }

        rows.forEach(function (row, index) {
            var item = document.createElement('li');
            item.className = 'pms-search__option';
            item.id = 'search-option-' + index;
            item.setAttribute('role', 'option');
            item.setAttribute('aria-selected', 'false');
            item.dataset.index = String(index);

            if (!row.canSell) {
                item.classList.add('pms-search__option--blocked');
                item.setAttribute('aria-disabled', 'true');
            }

            var left = document.createElement('span');

            var name = document.createElement('span');
            name.className = 'pms-search__name';
            name.textContent = row.brandName + (row.strength ? ' ' + row.strength : '');
            left.appendChild(name);

            if (row.isAntibiotic) {
                var badge = document.createElement('span');
                badge.className = 'pms-badge pms-badge--warning';
                badge.textContent = 'AB';
                name.appendChild(document.createTextNode(' '));
                name.appendChild(badge);
            }

            if (row.genericName) {
                var generic = document.createElement('span');
                generic.className = 'pms-search__meta';
                generic.textContent = row.genericName;
                left.appendChild(generic);
            }

            // The reason, in the dropdown, next to the thing it is about. This is the whole
            // point of returning unsellable products rather than filtering them out.
            if (row.blockedReason) {
                var reason = document.createElement('span');
                reason.className = 'pms-search__reason';
                reason.textContent = row.blockedReason;
                left.appendChild(reason);
            }

            var right = document.createElement('span');
            right.className = 'pms-search__right';
            right.textContent = row.formattedAvailable;

            item.appendChild(left);
            item.appendChild(right);

            item.addEventListener('mousedown', function (event) {
                // mousedown, not click: the input's blur would close the list first.
                event.preventDefault();
                choose(index);
            });

            results.appendChild(item);
        });

        results.hidden = false;
        search.setAttribute('aria-expanded', 'true');
    }

    function highlight(index) {
        var items = results.querySelectorAll('[role="option"]');

        for (var i = 0; i < items.length; i++) {
            items[i].setAttribute('aria-selected', i === index ? 'true' : 'false');
        }

        highlighted = index;

        if (index >= 0 && items[index]) {
            search.setAttribute('aria-activedescendant', items[index].id);
            items[index].scrollIntoView({ block: 'nearest' });
        }
    }

    function runSearch() {
        var term = search.value.trim();

        if (term.length < 2) {
            closeResults();
            return;
        }

        fetch(form.action.split('?')[0] + '?handler=Search&q=' + encodeURIComponent(term), {
            headers: { 'Accept': 'application/json' }
        })
            .then(function (response) {
                return response.json().then(function (payload) {
                    return { ok: response.ok, payload: payload };
                });
            })
            .then(function (result) {
                if (!result.ok) {
                    // An empty dropdown would read as "no such product", which is a different
                    // and much worse thing to tell a cashier than "the search failed".
                    renderResults([]);
                    var note = results.firstChild;
                    if (note) {
                        note.textContent = result.payload && result.payload.error
                            ? result.payload.error
                            : 'Search failed. Try again.';
                    }
                    return;
                }

                renderResults(result.payload || []);
            })
            .catch(function () {
                renderResults([]);
                var note = results.firstChild;
                if (note) {
                    note.textContent = 'Search failed. Try again.';
                }
            });
    }

    // ── the cart ─────────────────────────────────────────────────────────────────────────

    /* The unit levels a product actually defines, with their prices. Base always exists. */
    function unitOptionsFor(row) {
        var units = [];

        if (row.pricePerBase !== null && row.pricePerBase !== undefined) {
            units.push({
                level: UNIT_LEVELS.Base,
                name: row.baseUnitName,
                price: row.pricePerBase,
                perUnit: 1
            });
        }

        if (row.midUnitName && row.basePerMid && row.pricePerMid !== null
            && row.pricePerMid !== undefined) {
            units.push({
                level: UNIT_LEVELS.Mid,
                name: row.midUnitName,
                price: row.pricePerMid,
                perUnit: row.basePerMid
            });
        }

        if (row.largeUnitName && row.baseUnitsPerLarge && row.pricePerLarge !== null
            && row.pricePerLarge !== undefined) {
            units.push({
                level: UNIT_LEVELS.Large,
                name: row.largeUnitName,
                price: row.pricePerLarge,
                perUnit: row.baseUnitsPerLarge
            });
        }

        return units;
    }

    function nameRowInputs(tr, index) {
        var fields = tr.querySelectorAll('[data-field]');

        for (var i = 0; i < fields.length; i++) {
            fields[i].name = 'Input.Items[' + index + '].' + fields[i].dataset.field;
        }
    }

    function fillUnitSelect(select, units, selectedLevel) {
        select.innerHTML = '';

        units.forEach(function (unit) {
            var option = document.createElement('option');
            option.value = String(unit.level);
            option.textContent = unit.name;
            option.dataset.price = String(unit.price);
            option.dataset.perUnit = String(unit.perUnit);

            if (unit.level === selectedLevel) {
                option.selected = true;
            }

            select.appendChild(option);
        });
    }

    function choose(index) {
        var row = options[index];

        if (!row) {
            return;
        }

        if (!row.canSell) {
            // Refused here as well as server-side. Clicking a greyed row and having nothing
            // happen is worse than a message, so the reason goes into the live region under
            // the search box where a screen reader will read it out.
            var help = document.getElementById('search-help');
            if (help) {
                help.textContent = row.blockedReason || 'That product cannot be sold.';
            }
            return;
        }

        addRow(row);
        search.value = '';
        closeResults();
        search.focus();
    }

    function addRow(row) {
        var units = unitOptionsFor(row);

        if (!units.length) {
            return;
        }

        var fragment = template.content.cloneNode(true);
        var tr = fragment.querySelector('[data-cart-row]');

        tr.dataset.productId = row.id;
        tr.dataset.available = String(row.availableInBaseUnits);
        tr.dataset.antibiotic = row.isAntibiotic ? 'true' : 'false';
        tr.dataset.units = JSON.stringify(units);

        nameRowInputs(tr, nextIndex);
        nextIndex++;

        function field(name) {
            return tr.querySelector('[data-field="' + name + '"]');
        }

        field('ProductId').value = row.id;
        field('BrandName').value = row.brandName;
        field('Strength').value = row.strength || '';
        field('UnitName').value = units[0].name;
        field('UnitPrice').value = String(units[0].price);
        field('AvailableInBaseUnits').value = String(row.availableInBaseUnits);
        field('FormattedAvailable').value = row.formattedAvailable;
        field('IsAntibiotic').value = row.isAntibiotic ? 'true' : 'false';
        field('UnitOptionsJson').value = tr.dataset.units;

        tr.querySelector('[data-cart-name]').textContent = row.brandName;
        tr.querySelector('[data-cart-strength]').textContent = row.strength || '';
        tr.querySelector('[data-cart-ab]').hidden = !row.isAntibiotic;
        tr.querySelector('[data-cart-available]').textContent =
            row.formattedAvailable + ' available';

        var quantity = tr.querySelector('[data-cart-quantity]');
        quantity.setAttribute('aria-label', 'Quantity of ' + row.brandName);

        var unitSelect = tr.querySelector('[data-cart-unit]');
        unitSelect.setAttribute('aria-label', 'Unit for ' + row.brandName);
        fillUnitSelect(unitSelect, units, units[0].level);

        var remove = tr.querySelector('[data-cart-remove]');
        remove.setAttribute('aria-label', 'Remove ' + row.brandName + ' from the bill');

        body.appendChild(fragment);

        recalculate();
        quantity.focus();
        quantity.select();
    }

    /* Rows the server redisplayed after a rejection: rebuild their dropdown from data-units. */
    function rehydrate() {
        var rows = body ? body.querySelectorAll('[data-cart-row]') : [];

        for (var i = 0; i < rows.length; i++) {
            var tr = rows[i];
            var raw = tr.dataset.units;

            if (!raw) {
                continue;
            }

            var units;

            try {
                units = JSON.parse(raw);
            } catch (error) {
                continue;
            }

            var select = tr.querySelector('[data-cart-unit]');
            var selected = parseInt(select.value, 10);
            fillUnitSelect(select, units, selected);
        }
    }

    // ── the live bill ────────────────────────────────────────────────────────────────────

    function recalculate() {
        var rows = body.querySelectorAll('[data-cart-row]');
        var subtotal = 0;
        var anyAntibiotic = false;
        var anyInvalid = false;

        for (var i = 0; i < rows.length; i++) {
            var tr = rows[i];
            var quantity = number(tr.querySelector('[data-cart-quantity]'));
            var select = tr.querySelector('[data-cart-unit]');
            var option = select.options[select.selectedIndex];

            var price = option ? parseFloat(option.dataset.price) : 0;
            var perUnit = option ? parseInt(option.dataset.perUnit, 10) : 1;
            var available = parseInt(tr.dataset.available, 10) || 0;

            // Changing the unit changes the price. Kept in the hidden fields too, so a
            // rejected sale redraws the row as the cashier left it.
            var priceCell = tr.querySelector('[data-cart-price]');
            priceCell.textContent = money(price);
            tr.querySelector('[data-field="UnitPrice"]').value = String(price);
            tr.querySelector('[data-field="UnitName"]').value = option ? option.textContent : '';

            var lineTotal = round2(quantity * price);
            tr.querySelector('[data-cart-line-total]').textContent = money(lineTotal);
            subtotal = round2(subtotal + lineTotal);

            if (tr.dataset.antibiotic === 'true') {
                anyAntibiotic = true;
            }

            // Stock, in base units, against what this row is asking for.
            var neededBase = quantity * perUnit;
            var error = tr.querySelector('[data-cart-error]');
            var invalid = quantity <= 0 || neededBase > available;

            if (invalid) {
                anyInvalid = true;
                error.hidden = false;
                error.textContent = quantity <= 0
                    ? 'Enter a quantity of at least one.'
                    : 'Only ' + available + ' available.';
            } else {
                error.hidden = true;
                error.textContent = '';
            }

            tr.classList.toggle('pms-cart__row--invalid', invalid);
        }

        form.querySelector('[data-bill-subtotal]').textContent = money(subtotal);

        if (empty) {
            empty.hidden = rows.length > 0;
        }

        // The discount, previewed the same way the server computes it: a percentage of the
        // subtotal or a flat amount, capped at the subtotal so the bill cannot go negative.
        var entered = number(discountValue);
        var isPercent = discountType.value === 'Percent' || discountType.value === '0';
        var discount = entered <= 0
            ? 0
            : round2(isPercent ? subtotal * entered / 100 : entered);

        if (discount > subtotal) {
            discount = subtotal;
        }

        // The role cap, measured in taka for both discount types — a percentage cap that only
        // applied to percentages would be worked around by using the other button.
        var overCap = false;

        if (maxDiscountPercent !== null && subtotal > 0) {
            var cap = round2(subtotal * maxDiscountPercent / 100);

            if (discount > cap) {
                overCap = true;
                discountError.hidden = false;
                discountError.textContent =
                    'Your maximum is ' + maxDiscountPercent + '%, which is ' + money(cap)
                    + ' on this bill.';
            }
        }

        if (!overCap) {
            discountError.hidden = true;
            discountError.textContent = '';
        }

        form.querySelector('[data-bill-discount]').textContent = money(discount);

        var net = round2(subtotal - discount);
        form.querySelector('[data-bill-net]').textContent = money(net);

        var cash = number(cashInput);
        var change = round2(cash - net);
        form.querySelector('[data-bill-change]').textContent = money(change < 0 ? 0 : change);

        var shortCash = rows.length > 0 && cash < net;

        if (shortCash) {
            cashError.hidden = false;
            cashError.textContent = 'Cash received is less than the ' + money(net) + ' owed.';
        } else {
            cashError.hidden = true;
            cashError.textContent = '';
        }

        // The prescription panel follows the cart. Emptying the cart of antibiotics hides it
        // again, and the fields are left as they were rather than cleared — a cashier who
        // removed the wrong row should not have to type it all in again.
        if (prescription) {
            prescription.hidden = !anyAntibiotic;
        }

        var missingPrescription = anyAntibiotic && !prescriptionComplete();

        // Say why, always. A disabled button with no explanation is how a fast screen becomes
        // a slow one: the cashier clicks, nothing happens, and they start hunting.
        var reason = '';

        if (rows.length === 0) {
            reason = 'Add an item to start.';
        } else if (anyInvalid) {
            reason = 'Fix the quantities marked above.';
        } else if (overCap) {
            reason = 'The discount is over your limit.';
        } else if (shortCash) {
            reason = 'Enter the cash received.';
        } else if (missingPrescription) {
            reason = 'Fill in the prescription details and tick verified.';
        }

        completeButton.disabled = reason !== '';
        blockedNote.textContent = reason;
        blockedNote.hidden = reason === '';
    }

    /*
        Whether the prescription panel is satisfied.

        Trivially true unless the pharmacy is in Required mode. Under Off there is no panel in
        the DOM at all; under Optional there is one, and gating the Complete button on it would
        make "optional" a lie — the point of that mode is that a cashier records what they have
        and the sale goes through either way.

        The flag comes from the server on the panel itself, so the screen and the sale agree
        about which rules are in force.
    */
    function prescriptionComplete() {
        if (!prescription) {
            return true;
        }

        if (prescription.dataset.prescriptionRequired !== 'true') {
            return true;
        }

        var required = prescription.querySelectorAll(
            'input[type="text"], input[type="date"], input:not([type])');

        for (var i = 0; i < required.length; i++) {
            if (!required[i].value.trim()) {
                return false;
            }
        }

        var verified = prescription.querySelector('input[type="checkbox"]');

        return !!verified && verified.checked;
    }

    // ── wiring ───────────────────────────────────────────────────────────────────────────

    search.addEventListener('input', function () {
        window.clearTimeout(searchTimer);

        // 180ms: long enough that a fast typist makes one request instead of eight, short
        // enough that the list feels like it is keeping up.
        searchTimer = window.setTimeout(runSearch, 180);
    });

    search.addEventListener('keydown', function (event) {
        if (results.hidden) {
            if (event.key === 'Enter') {
                // Nothing open yet: search now rather than submitting the form, which is what
                // a bare Enter in a text input inside a form would otherwise do.
                event.preventDefault();
                window.clearTimeout(searchTimer);
                runSearch();
            }
            return;
        }

        if (event.key === 'ArrowDown') {
            event.preventDefault();
            highlight(Math.min(highlighted + 1, options.length - 1));
        } else if (event.key === 'ArrowUp') {
            event.preventDefault();
            highlight(Math.max(highlighted - 1, 0));
        } else if (event.key === 'Enter') {
            event.preventDefault();

            // Enter with nothing highlighted takes the first sellable row, which is what a
            // cashier who typed an exact brand name expects.
            if (highlighted >= 0) {
                choose(highlighted);
            } else {
                for (var i = 0; i < options.length; i++) {
                    if (options[i].canSell) {
                        choose(i);
                        return;
                    }
                }
            }
        } else if (event.key === 'Escape') {
            closeResults();
        }
    });

    search.addEventListener('blur', function () {
        // Deferred, so a mousedown on an option is handled before the list disappears.
        window.setTimeout(closeResults, 120);
    });

    // Delegated, so rows added later need no wiring of their own.
    body.addEventListener('input', recalculate);
    body.addEventListener('change', recalculate);

    body.addEventListener('click', function (event) {
        var button = event.target.closest('[data-cart-remove]');

        if (!button) {
            return;
        }

        var row = button.closest('[data-cart-row]');

        if (row) {
            row.remove();
            recalculate();
            search.focus();
        }
    });

    [discountType, discountValue, cashInput].forEach(function (input) {
        input.addEventListener('input', recalculate);
        input.addEventListener('change', recalculate);
    });

    if (prescription) {
        prescription.addEventListener('input', recalculate);
        prescription.addEventListener('change', recalculate);
    }

    form.addEventListener('submit', function () {
        // Guards against a double-tap on a slow connection producing two sales. The button is
        // disabled after the browser has collected the form, so its value still posts.
        window.setTimeout(function () {
            completeButton.disabled = true;
            completeButton.classList.add('is-busy');
        }, 0);
    });

    rehydrate();
    recalculate();
    search.focus();
})();
