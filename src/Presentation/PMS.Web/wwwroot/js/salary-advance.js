/*
    The advance form's employee picker.

    One job: show what the chosen employee already owes, beneath the dropdown, before anybody
    types an amount. Somebody about to hand over more cash should see what is outstanding.

    The figure is NOT fetched. Every option carries it on a data attribute, put there when the
    page rendered, so this cannot fail halfway and cannot show a stale number from a previous
    selection. With JavaScript off the option text names the outstanding total too - this only
    makes it more prominent.
*/
(function () {
    'use strict';

    var select = document.querySelector('[data-pms-advance-employee]');
    var note = document.querySelector('[data-pms-advance-outstanding]');

    if (!select || !note) {
        return;
    }

    var idle = note.textContent;

    function money(value) {
        return Number(value).toLocaleString(undefined, {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function update() {
        var option = select.options[select.selectedIndex];

        if (!option || !option.value) {
            note.textContent = idle;
            note.classList.remove('figure-negative');
            return;
        }

        var outstanding = Number(option.getAttribute('data-outstanding') || 0);
        var salary = Number(option.getAttribute('data-salary') || 0);

        if (outstanding > 0) {
            note.textContent = 'Already outstanding: ' + money(outstanding)
                + '. That will come out of a future salary before anything recorded here does.';
            note.classList.add('figure-negative');
        } else {
            note.textContent = 'Nothing outstanding. Monthly salary ' + money(salary) + '.';
            note.classList.remove('figure-negative');
        }
    }

    select.addEventListener('change', update);
    update();
}());
