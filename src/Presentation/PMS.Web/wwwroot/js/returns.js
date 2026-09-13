/*
    The return screen.

    One job: show the cashier what they are about to hand over, before they hand it over.

    The refund on a discounted sale is lower than the price printed on the invoice — 52.50 back
    on a line that reads 60.00 — and a cashier who meets that at the moment of opening the till
    will hesitate in front of the customer, or worse, argue with them. So the figure appears as
    soon as a line and a quantity are chosen, with a note explaining why it is what it is.

    The arithmetic here mirrors SaleMath.RefundFor, including the rule that the return which
    empties a line pays the remainder rather than its own proportion. The server recomputes it
    and the server's answer is what gets paid; this file being wrong would be a misleading
    preview, never a wrong refund. The row's data-refund-all attribute is the server's own
    figure for the whole outstanding quantity, so the common case — returning everything left —
    is not computed here at all.
*/
(function () {
    'use strict';

    var form = document.getElementById('return-form');
    if (!form) {
        return;
    }

    var quantity = form.querySelector('[data-return-quantity]');
    var unitSelect = form.querySelector('[data-return-unit]');
    var available = form.querySelector('[data-return-available]');
    var reason = form.querySelector('[data-return-reason]');
    var preview = form.querySelector('[data-refund-preview]');
    var confirm = form.querySelector('[data-return-confirm]');

    var UNIT_LEVELS = { Base: 0, Mid: 1, Large: 2 };

    function selectedLine() {
        return form.querySelector('[data-return-line]:checked');
    }

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

    /* The unit levels this line's product defines. Base always exists. */
    function unitsFor(line) {
        var units = [{ level: UNIT_LEVELS.Base, name: line.dataset.baseUnit, perUnit: 1 }];

        if (line.dataset.midUnit && line.dataset.basePerMid) {
            units.push({
                level: UNIT_LEVELS.Mid,
                name: line.dataset.midUnit,
                perUnit: parseInt(line.dataset.basePerMid, 10)
            });
        }

        if (line.dataset.largeUnit && line.dataset.basePerLarge) {
            units.push({
                level: UNIT_LEVELS.Large,
                name: line.dataset.largeUnit,
                perUnit: parseInt(line.dataset.basePerLarge, 10)
            });
        }

        return units;
    }

    function fillUnits(line) {
        var units = unitsFor(line);
        var previous = unitSelect.value;

        unitSelect.innerHTML = '';

        units.forEach(function (unit) {
            var option = document.createElement('option');
            option.value = String(unit.level);
            option.textContent = unit.name;
            option.dataset.perUnit = String(unit.perUnit);

            if (String(unit.level) === previous) {
                option.selected = true;
            }

            unitSelect.appendChild(option);
        });
    }

    function update() {
        var line = selectedLine();

        if (!line) {
            available.textContent = '';
            preview.textContent = 'Choose an item to see the refund.';
            confirm.dataset.pmsConfirm = 'Choose an item first.';
            confirm.disabled = true;
            return;
        }

        var returnable = parseInt(line.dataset.returnable, 10) || 0;
        var sold = parseInt(line.dataset.sold, 10) || 0;
        var netLineTotal = parseFloat(line.dataset.netLineTotal) || 0;
        var refunded = parseFloat(line.dataset.refunded) || 0;
        var refundAll = parseFloat(line.dataset.refundAll) || 0;
        var alreadyReturned = sold - returnable;
        var brand = line.dataset.brand || 'this item';

        var option = unitSelect.options[unitSelect.selectedIndex];
        var perUnit = option ? parseInt(option.dataset.perUnit, 10) : 1;
        var unitName = option ? option.textContent : '';
        var entered = parseFloat(quantity.value) || 0;
        var baseUnits = Math.round(entered * perUnit);

        available.textContent = returnable + ' ' + line.dataset.baseUnit + ' left to return';

        if (entered <= 0 || baseUnits <= 0) {
            preview.textContent = 'Enter how much is coming back.';
            confirm.disabled = true;
            return;
        }

        if (baseUnits > returnable) {
            preview.textContent =
                'Only ' + returnable + ' ' + line.dataset.baseUnit + ' of ' + brand
                + ' can still be returned.';
            confirm.disabled = true;
            return;
        }

        // The completing return pays the remainder, so a line returned in pieces still adds up
        // to exactly what the customer paid for it. Taken from the server's own figure when
        // the whole outstanding quantity is coming back.
        var refund = baseUnits === returnable
            ? refundAll
            : round2(baseUnits / sold * netLineTotal);

        preview.textContent = 'Refund: ' + money(refund);

        // Only mention the discount when there was one. Explaining an absent discount is how a
        // clear number becomes a suspicious one.
        if (refunded > 0) {
            preview.textContent +=
                ' (' + money(refunded) + ' has already been refunded on this line)';
        }

        preview.textContent += '. This is the discounted price the customer paid.';

        confirm.dataset.pmsConfirm =
            'Return ' + entered + ' ' + unitName + ' of ' + brand
            + '? This restores stock to the batch it was sold from and refunds '
            + money(refund) + '.';

        confirm.disabled = !reason.value.trim();

        if (!reason.value.trim()) {
            preview.textContent += ' Add a reason to continue.';
        }
    }

    form.addEventListener('change', function (event) {
        if (event.target.matches('[data-return-line]')) {
            fillUnits(event.target);
        }

        update();
    });

    form.addEventListener('input', update);

    form.querySelectorAll('[data-reason-preset]').forEach(function (button) {
        button.addEventListener('click', function () {
            reason.value = button.dataset.reasonPreset;

            // Focus moves on rather than staying on the button: the quick-pick is a shortcut
            // through the field, not a destination.
            reason.focus();
            update();
        });
    });

    var initial = selectedLine();

    if (initial) {
        fillUnits(initial);
    }

    update();
})();
