// Module 4 — the payment form's direction toggle.
//
// One job: hide the "Against a bill" row when money is coming back, because a refund or a
// write-off settles the ACCOUNT and cannot name a bill. The server enforces that with a
// validator and a CHECK constraint; this only stops somebody filling in a field that will be
// ignored.
//
// Without the script the row stays visible and any purchase selected on it is dropped
// server-side, so nothing breaks — it is just less obvious why.

(function () {
    'use strict';

    var radios = document.querySelectorAll('input[name="Input.Direction"]');
    var againstRow = document.getElementById('against-row');

    if (!radios.length || !againstRow) {
        return;
    }

    function apply() {
        var chosen = document.querySelector('input[name="Input.Direction"]:checked');
        var incoming = chosen && chosen.dataset.direction === 'in';

        againstRow.hidden = !!incoming;

        // Cleared rather than merely hidden: a hidden select still posts, and posting a bill on
        // a refund would earn a validation error for something the person cannot see.
        if (incoming) {
            var select = againstRow.querySelector('select');

            if (select) {
                select.value = '';
            }
        }
    }

    Array.prototype.forEach.call(radios, function (radio) {
        radio.addEventListener('change', apply);
    });

    apply();
})();
