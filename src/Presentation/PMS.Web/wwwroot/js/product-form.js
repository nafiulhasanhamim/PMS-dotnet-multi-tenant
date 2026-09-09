// ============================================================================
// The product form's unit setup.
//
// Three jobs, all of them about stopping a plausible-looking wrong number:
//
//   1. Presets set up the shape, so nobody faces five raw inputs at once.
//   2. Level checkboxes enable and clear their own fields, so leaving a level
//      off reads as a decision rather than a forgotten field.
//   3. A live summary recalculates as they type. "1 carton = 240 bottles" is
//      obviously wrong at a glance; a 240 sitting in an input is not.
//
// The base-units-per-large rule here MUST match the server's
// Product.BaseUnitsPerLarge: without a middle level, the bulk count is already
// in base units and must not be multiplied.
// ============================================================================

(function () {
    'use strict';

    var form = document.getElementById('product-form');
    if (!form) {
        return;
    }

    var q = function (sel) { return form.querySelector(sel); };

    var preset = q('[data-pms-preset]');
    var baseUnit = q('[data-pms-base-unit]');
    var midToggle = q('[data-pms-level-toggle="mid"]');
    var midUnit = q('[data-pms-mid-unit]');
    var basePerMid = q('[data-pms-base-per-mid]');
    var largeToggle = q('[data-pms-level-toggle="large"]');
    var largeUnit = q('[data-pms-large-unit]');
    var midPerLarge = q('[data-pms-mid-per-large]');
    var summary = q('[data-pms-packing-summary]');

    var midCountLabel = q('[data-pms-mid-count-label]');
    var largeCountLabel = q('[data-pms-large-count-label]');
    var priceBaseLabel = q('[data-pms-price-base-label]');
    var priceMidLabel = q('[data-pms-price-mid-label]');
    var priceLargeLabel = q('[data-pms-price-large-label]');
    var reorderLabel = q('[data-pms-reorder-label]');

    if (!baseUnit || !midToggle || !largeToggle) {
        return;
    }

    // Pluralisation, matching the server's.
    function plural(noun, count) {
        if (count === 1 || !noun) { return noun; }
        var l = noun.toLowerCase();
        if (/(s|x|z|ch|sh)$/.test(l)) { return noun + 'es'; }
        if (/[^aeiou]y$/.test(l)) { return noun.slice(0, -1) + 'ies'; }
        return noun + 's';
    }

    function val(el) { return el && el.value ? el.value.trim() : ''; }

    function num(el) {
        var n = el && el.value ? parseInt(el.value, 10) : NaN;
        return isNaN(n) || n < 1 ? null : n;
    }

    // Enable or disable a level's fields.
    function setLevel(level, on) {
        form.querySelectorAll('[data-pms-level="' + level + '"]').forEach(function (group) {
            group.querySelectorAll('input').forEach(function (input) {
                input.disabled = !on;
                if (!on) {
                    // Unticking clears, so a stale "strip / 10" from a change of
                    // mind is never posted as real configuration.
                    input.value = '';
                }
            });
            group.style.opacity = on ? '1' : '0.5';
        });
    }

    // The live summary, and every unit-aware label.
    function refresh() {
        var bu = val(baseUnit) || 'unit';
        var mu = val(midUnit);
        var lu = val(largeUnit);
        var hasMid = midToggle.checked && mu !== '';
        var hasLarge = largeToggle.checked && lu !== '';
        var perMid = num(basePerMid);
        var perLarge = num(midPerLarge);

        // Question text names the unit actually being counted. Whether the
        // answer is "strips" or "bottles" decides the number typed, and it
        // changes the moment the middle-level checkbox does.
        if (midCountLabel) {
            midCountLabel.textContent = 'How many ' + plural(bu, 2) + ' in one'
                + (mu ? ' ' + mu : '') + '?';
        }

        if (largeCountLabel) {
            var counted = hasMid && mu ? plural(mu, 2) : plural(bu, 2);
            largeCountLabel.textContent = 'How many ' + counted + ' in one'
                + (lu ? ' ' + lu : '') + '?';
        }

        if (priceBaseLabel) { priceBaseLabel.textContent = 'Price per ' + bu; }
        if (priceMidLabel) { priceMidLabel.textContent = 'Price per ' + (mu || 'pack'); }
        if (priceLargeLabel) { priceLargeLabel.textContent = 'Price per ' + (lu || 'bulk pack'); }
        if (reorderLabel) { reorderLabel.textContent = plural(bu, 2); }

        if (!summary) { return; }

        if (!hasMid && !hasLarge) {
            summary.textContent = 'Sold as individual ' + plural(bu, 2) + ' only';
            return;
        }

        var parts = [];

        if (hasLarge && perLarge) {
            if (hasMid && !perMid) {
                // Mid named but its count not entered yet: say what is known
                // rather than compute a wrong total.
                summary.textContent = '1 ' + lu + ' = ' + perLarge + ' ' + plural(mu, perLarge);
                return;
            }

            // The trap: with no middle level, perLarge is ALREADY base units.
            var baseInLarge = hasMid ? perMid * perLarge : perLarge;
            parts.push('1 ' + lu);

            if (hasMid) { parts.push(perLarge + ' ' + plural(mu, perLarge)); }

            parts.push(baseInLarge + ' ' + plural(bu, baseInLarge));
        } else if (hasMid && perMid) {
            parts.push('1 ' + mu);
            parts.push(perMid + ' ' + plural(bu, perMid));
        }

        summary.textContent = parts.length
            ? parts.join(' = ')
            : 'Enter the pack sizes to see a summary.';
    }

    // Presets.
    function applyPreset(value) {
        if (value === 'ThreeLevel') {
            baseUnit.value = baseUnit.value || 'piece';
            midToggle.checked = true;
            largeToggle.checked = true;
            setLevel('mid', true);
            setLevel('large', true);
            midUnit.value = midUnit.value || 'strip';
            largeUnit.value = largeUnit.value || 'box';
        } else if (value === 'UnitAndBulk') {
            midToggle.checked = false;
            largeToggle.checked = true;
            setLevel('mid', false);
            setLevel('large', true);
            largeUnit.value = largeUnit.value || 'carton';
        } else if (value === 'SingleUnit') {
            midToggle.checked = false;
            largeToggle.checked = false;
            setLevel('mid', false);
            setLevel('large', false);
        } else {
            // Custom leaves everything as it is and simply shows all the fields.
            setLevel('mid', midToggle.checked);
            setLevel('large', largeToggle.checked);
        }

        refresh();
    }

    if (preset) {
        preset.addEventListener('change', function () { applyPreset(preset.value); });
    }

    midToggle.addEventListener('change', function () {
        setLevel('mid', midToggle.checked);
        refresh();
    });

    largeToggle.addEventListener('change', function () {
        setLevel('large', largeToggle.checked);
        refresh();
    });

    [baseUnit, midUnit, basePerMid, largeUnit, midPerLarge].forEach(function (el) {
        if (el) { el.addEventListener('input', refresh); }
    });

    // The antibiotic-change warning, on edit only.
    var antibiotic = q('[data-pms-antibiotic]');
    var antibioticWarning = q('[data-pms-antibiotic-warning]');

    if (antibiotic && antibioticWarning) {
        var initial = antibiotic.getAttribute('data-pms-antibiotic-initial') === 'true';

        antibiotic.addEventListener('change', function () {
            // Shown only when the value actually moves away from what was saved -
            // toggling twice puts it back, and the warning goes away with it.
            antibioticWarning.hidden = antibiotic.checked === initial;
        });
    }

    // Initial state, so a form reloaded after a validation failure looks right.
    setLevel('mid', midToggle.checked);
    setLevel('large', largeToggle.checked);
    refresh();
})();
