/*
    The settings page.

    One job, and it is the same one antibiotic-settings.js does for Module 7's focused screen:
    the Save button confirms only when the change is one that takes a capability away from staff
    who are already working — switching antibiotic capture to Required. Every other change saves
    directly.

    Confirming on every save would train an owner to click through the dialogue, and by the time
    the one that mattered appeared they would not be reading it. That matters more here than on
    the focused screen, because this page is saved for ordinary reasons — correcting a phone
    number — far more often than for that one.

    Progressive: with no script the button is a plain submit and the form still saves. The
    confirmation is a courtesy, not the enforcement — the API applies whatever mode it is sent,
    and refuses whatever it should refuse.
*/
(function () {
    'use strict';

    var form = document.getElementById('settings-form');
    var button = document.getElementById('save-settings');

    if (!form || !button) {
        return;
    }

    var currentMode = button.dataset.currentMode;

    function selectedMode() {
        var chosen = form.querySelector('input[name$="AntibioticPrescriptionMode"]:checked');

        return chosen ? chosen.dataset.mode : null;
    }

    function needsConfirmation() {
        return selectedMode() === 'Required' && currentMode !== 'Required';
    }

    function apply() {
        if (needsConfirmation()) {
            // site.js binds its modal to [data-pms-confirm], so the attribute's presence is what
            // decides whether a click opens it.
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
}());
