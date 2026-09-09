// ============================================================================
// PMS — the small amount of behaviour the UI actually needs.
//
// No jQuery. Bootstrap 5's bundle needs none, and neither does anything here.
// ============================================================================

(function () {
    'use strict';

    // ── Sidebar, below tablet width ─────────────────────────────────────────
    var shell = document.querySelector('.pms-shell');
    var toggle = document.querySelector('[data-pms-nav-toggle]');
    var backdrop = document.querySelector('.pms-backdrop');

    function closeNav() {
        if (shell) {
            shell.classList.remove('pms-shell--nav-open');
        }
        if (toggle) {
            toggle.setAttribute('aria-expanded', 'false');
        }
    }

    if (toggle && shell) {
        toggle.addEventListener('click', function () {
            var open = shell.classList.toggle('pms-shell--nav-open');
            toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
    }

    if (backdrop) {
        backdrop.addEventListener('click', closeNav);
    }

    // ── Double-submit guard ─────────────────────────────────────────────────
    //
    // Disabling the button after submit is not cosmetic: a second POST of
    // "create user" is a second user, and staff on a slow connection will click
    // again. The button is disabled *after* the browser has collected the form,
    // so its value still posts.
    document.querySelectorAll('form[data-pms-guard]').forEach(function (form) {
        form.addEventListener('submit', function () {
            if (form.dataset.pmsSubmitted === 'true') {
                return;
            }

            // Leave an invalid form alone — the person still has to fix it.
            if (typeof form.checkValidity === 'function' && !form.checkValidity()) {
                return;
            }

            form.dataset.pmsSubmitted = 'true';

            form.querySelectorAll('button[type="submit"]').forEach(function (button) {
                button.classList.add('pms-busy');
                button.setAttribute('aria-busy', 'true');

                // Deferred so the value is included in the request body.
                window.setTimeout(function () {
                    button.disabled = true;
                }, 0);
            });
        });
    });

    // ── Confirmation modals ─────────────────────────────────────────────────
    //
    // One modal element per page, reused. The trigger carries the wording and
    // the form to submit, so a page adds a confirmed action by adding data
    // attributes rather than another modal.
    var modalEl = document.getElementById('pmsConfirmModal');

    if (modalEl && window.bootstrap) {
        var modal = new window.bootstrap.Modal(modalEl);
        var titleEl = modalEl.querySelector('[data-pms-confirm-title]');
        var bodyEl = modalEl.querySelector('[data-pms-confirm-body]');
        var okEl = modalEl.querySelector('[data-pms-confirm-ok]');
        var pendingFormId = null;

        document.querySelectorAll('[data-pms-confirm]').forEach(function (trigger) {
            trigger.addEventListener('click', function (event) {
                event.preventDefault();

                titleEl.textContent = trigger.dataset.pmsConfirmTitle || 'Are you sure?';
                bodyEl.textContent = trigger.dataset.pmsConfirm || '';
                okEl.textContent = trigger.dataset.pmsConfirmOk || 'Confirm';
                pendingFormId = trigger.dataset.pmsConfirmForm || null;

                modal.show();
            });
        });

        // Cancel is the default-focused control: the safe option should be the
        // one that a reflexive Enter press picks.
        modalEl.addEventListener('shown.bs.modal', function () {
            var cancel = modalEl.querySelector('[data-pms-confirm-cancel]');
            if (cancel) {
                cancel.focus();
            }
        });

        okEl.addEventListener('click', function () {
            // A trigger naming a form that does not exist used to do nothing at all: no
            // submit, no error, a button that looked fine and was dead. It cost a bug report.
            // Now it says so in the console and leaves the modal open, so the button never
            // pretends to have worked.
            var form = pendingFormId ? document.getElementById(pendingFormId) : null;

            if (!form) {
                console.error(
                    'pms-confirm: no form with id "' + pendingFormId + '". The element with '
                    + 'data-pms-confirm-form must name a form on this page.');
                return;
            }

            okEl.classList.add('pms-busy');
            okEl.disabled = true;
            form.submit();
        });
    }
})();
