/*
    The salary generation grid.

    Recomputes net payable as somebody types, and warns on any row where the advances outstanding
    are more than the month can cover.

    WHAT THIS IS NOT: authoritative. Every figure here is a preview. The server recomputes net
    payable from the same rules when the form posts, caps the advance deduction itself, and the
    response reports what actually happened. Nothing computed in this file is submitted - the
    inputs are, and that is all. If this script fails to load, the form still works and still
    produces correct entries; it just stops showing the answer in advance.

    The cap mirrors SalaryEntry.Generate exactly: recoverable is the lesser of what was asked for
    and what the month can bear, and net payable floors at zero. Keeping the two in step matters -
    a preview that promised 0 and a server that produced 2,000 would be worse than no preview.
*/
(function () {
    'use strict';

    var form = document.querySelector('[data-pms-salary-form]');

    if (!form) {
        return;
    }

    var rows = Array.prototype.slice.call(
        form.querySelectorAll('[data-pms-salary-row]'));
    var totalEl = form.querySelector('[data-pms-salary-total]');
    var countEl = form.querySelector('[data-pms-salary-count]');
    var submit = form.querySelector('[data-pms-salary-submit]');
    var period = form.getAttribute('data-period') || 'this month';

    function num(input) {
        if (!input) {
            return 0;
        }

        var value = parseFloat(input.value);

        return isFinite(value) && value > 0 ? value : 0;
    }

    function money(value) {
        return Number(value).toLocaleString(undefined, {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function recalcRow(row) {
        var base = parseFloat(row.getAttribute('data-base')) || 0;
        var bonus = num(row.querySelector('[data-pms-salary-bonus]'));
        var requested = num(row.querySelector('[data-pms-salary-advance]'));
        var other = num(row.querySelector('[data-pms-salary-other]'));

        // Mirrors the domain: what the month can bear, then the deduction capped by it.
        var payable = Math.max(0, base + bonus - other);
        var recovered = Math.min(requested, payable);
        var net = Math.max(0, payable - recovered);
        var carried = Math.max(0, requested - recovered);

        var netEl = row.querySelector('[data-pms-salary-net]');
        var warnEl = row.querySelector('[data-pms-salary-warning]');

        if (netEl) {
            netEl.textContent = money(net);
        }

        if (warnEl) {
            if (carried > 0.004) {
                warnEl.textContent = 'Advances exceed this month\'s payable. '
                    + money(carried) + ' will carry over to next month.';
                warnEl.hidden = false;
            } else {
                warnEl.textContent = '';
                warnEl.hidden = true;
            }
        }

        return net;
    }

    function selected(row) {
        var box = row.querySelector('[data-pms-salary-select]');

        return !box || box.checked;
    }

    function setRowEnabled(row) {
        var on = selected(row);

        // A row nobody is paying should not accept figures - and a greyed row is how somebody
        // sees at a glance who is in this payroll run.
        row.classList.toggle('pms-row-inactive', !on);

        ['[data-pms-salary-bonus]', '[data-pms-salary-advance]',
         '[data-pms-salary-other]', '[data-pms-salary-notes]'].forEach(function (sel) {
            var input = row.querySelector(sel);

            if (input) {
                input.disabled = !on;
            }
        });
    }

    function recalcAll() {
        var total = 0;
        var count = 0;

        rows.forEach(function (row) {
            setRowEnabled(row);

            var net = recalcRow(row);

            if (selected(row)) {
                total += net;
                count += 1;
            }
        });

        if (totalEl) {
            totalEl.textContent = money(total);
        }

        if (countEl) {
            countEl.textContent = count + (count === 1 ? ' selected' : ' selected');
        }

        if (submit) {
            submit.disabled = count === 0;
        }

        form.setAttribute('data-total', total.toFixed(2));
        form.setAttribute('data-count', String(count));
    }

    form.addEventListener('input', recalcAll);
    form.addEventListener('change', recalcAll);

    // The confirmation. Not a nicety: this writes a locked-once-paid record for several people
    // at once, and the totals are the thing worth reading back before it happens.
    form.addEventListener('submit', function (event) {
        var count = Number(form.getAttribute('data-count') || 0);
        var total = Number(form.getAttribute('data-total') || 0);

        if (count === 0) {
            event.preventDefault();
            return;
        }

        var message = 'Generate salary for ' + count
            + (count === 1 ? ' employee' : ' employees') + ' for ' + period
            + '? Total payable: ' + money(total) + '.';

        if (!window.confirm(message)) {
            event.preventDefault();
        }
    });

    recalcAll();
}());
