/*
    The antibiotic mode settings page.

    One job: the Save button confirms only when the change is one that takes a capability away
    from staff who are already working — switching to Required. Every other change saves
    directly.

    Confirming on every save, including Off to Off, would train an owner to click through the
    dialogue, and by the time the one that mattered appeared they would not be reading it.

    Progressive: with no script the button is a plain submit and the form still saves. The
    confirmation is a safeguard, not the enforcement — the API applies whatever mode it is sent.
*/
(function () {
    'use strict';

    var form = document.getElementById('antibiotic-mode-form');
    var button = document.getElementById('save-mode');

    if (!form || !button) {
        return;
    }

    var currentMode = button.dataset.currentMode;

    function selectedMode() {
        var chosen = form.querySelector('input[type="radio"]:checked');
        return chosen ? chosen.dataset.mode : null;
    }

    function needsConfirmation() {
        return selectedMode() === 'Required' && currentMode !== 'Required';
    }

    function apply() {
        if (needsConfirmation()) {
            // site.js binds its modal to [data-pms-confirm], so the attribute's presence is
            // what decides whether a click opens it.
            button.setAttribute('data-pms-confirm', button.dataset.pmsConfirmStored);
            button.type = 'button';
        } else {
            button.removeAttribute('data-pms-confirm');
            button.type = 'submit';
        }
    }

    // Stashed before the first apply() can strip it, so it can be put back.
    button.dataset.pmsConfirmStored = button.getAttribute('data-pms-confirm') || '';

    form.addEventListener('change', apply);
    apply();
})();
