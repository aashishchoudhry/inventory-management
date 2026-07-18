/*
 * Vanilla JS, no framework. Each module is guarded so a page missing its elements
 * is a no-op rather than an error — every module runs on every page.
 */
(function () {
    'use strict';

    var THEME_KEY = 'app-theme';

    /* ------------------------------------------------------------- theme */

    function initTheme() {
        var toggle = document.querySelector('[data-theme-toggle]');
        var lightIcon = document.querySelector('[data-theme-icon-light]');
        var darkIcon = document.querySelector('[data-theme-icon-dark]');

        function apply(theme) {
            document.documentElement.setAttribute('data-bs-theme', theme);
            var isDark = theme === 'dark';
            if (toggle) { toggle.setAttribute('aria-pressed', String(isDark)); }
            if (lightIcon) { lightIcon.classList.toggle('d-none', isDark); }
            if (darkIcon) { darkIcon.classList.toggle('d-none', !isDark); }
        }

        // The inline script in <head> already set the attribute; sync the button to it.
        apply(document.documentElement.getAttribute('data-bs-theme') || 'light');

        if (!toggle) { return; }

        toggle.addEventListener('click', function () {
            var next = document.documentElement.getAttribute('data-bs-theme') === 'dark'
                ? 'light'
                : 'dark';
            apply(next);
            try { localStorage.setItem(THEME_KEY, next); } catch (e) { /* private mode */ }
        });

        // Follow the OS only while the user has expressed no explicit preference.
        if (window.matchMedia) {
            var mq = window.matchMedia('(prefers-color-scheme: dark)');
            var onChange = function (e) {
                var stored = null;
                try { stored = localStorage.getItem(THEME_KEY); } catch (err) { /* ignore */ }
                if (!stored) { apply(e.matches ? 'dark' : 'light'); }
            };
            if (mq.addEventListener) { mq.addEventListener('change', onChange); }
        }
    }

    /* ----------------------------------------------------------- sidebar */

    function initSidebar() {
        var sidebar = document.querySelector('[data-sidebar]');
        var toggle = document.querySelector('[data-sidebar-toggle]');
        var backdrop = document.querySelector('[data-sidebar-backdrop]');
        if (!sidebar || !toggle) { return; }

        function setOpen(open) {
            sidebar.classList.toggle('is-open', open);
            toggle.setAttribute('aria-expanded', String(open));
            if (backdrop) {
                backdrop.classList.toggle('is-visible', open);
                backdrop.hidden = !open;
            }
            document.body.style.overflow = open ? 'hidden' : '';
        }

        toggle.addEventListener('click', function () {
            setOpen(!sidebar.classList.contains('is-open'));
        });

        if (backdrop) {
            backdrop.addEventListener('click', function () { setOpen(false); });
        }

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && sidebar.classList.contains('is-open')) {
                setOpen(false);
                toggle.focus();
            }
        });

        // Close on navigation within the drawer, otherwise it stays open behind the new page.
        sidebar.addEventListener('click', function (e) {
            if (e.target.closest('a') && window.innerWidth < 992) { setOpen(false); }
        });

        window.addEventListener('resize', function () {
            if (window.innerWidth >= 992) { setOpen(false); }
        });
    }

    /* ------------------------------------------------------ table filter */

    function initTableFilter() {
        var input = document.querySelector('[data-table-filter]');
        if (!input) { return; }

        var table = document.querySelector(input.getAttribute('data-table-filter'));
        if (!table || !table.tBodies.length) { return; }

        var status = document.querySelector('[data-filter-status]');
        var noResults = document.querySelector('[data-filter-empty]');
        var rows = Array.prototype.slice.call(table.tBodies[0].rows);

        input.addEventListener('input', function () {
            var q = input.value.trim().toLowerCase();
            var shown = 0;

            rows.forEach(function (row) {
                var match = q === '' || row.textContent.toLowerCase().indexOf(q) !== -1;
                row.hidden = !match;
                if (match) { shown++; }
            });

            if (status) {
                status.textContent = q === ''
                    ? rows.length + ' items'
                    : shown + ' of ' + rows.length + ' items';
            }
            if (noResults) { noResults.hidden = shown !== 0; }
        });
    }

    /* -------------------------------------------------- password reveal */

    function initPasswordReveal() {
        document.querySelectorAll('[data-reveal-target]').forEach(function (btn) {
            var input = document.querySelector(btn.getAttribute('data-reveal-target'));
            if (!input) { return; }

            btn.addEventListener('click', function () {
                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                btn.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
                btn.querySelectorAll('svg').forEach(function (svg, i) {
                    svg.classList.toggle('d-none', show ? i === 0 : i === 1);
                });
            });
        });
    }

    /* --------------------------------------------------- submit spinner */

    function initSubmitState() {
        document.querySelectorAll('form[data-busy-submit]').forEach(function (form) {
            form.addEventListener('submit', function () {
                // jQuery-validate cancels invalid submits; don't latch the button in that case.
                if (form.checkValidity && !form.checkValidity()) { return; }

                var btn = form.querySelector('[type="submit"]');
                if (!btn || btn.dataset.busy === '1') { return; }

                btn.dataset.busy = '1';
                btn.disabled = true;
                btn.insertAdjacentHTML(
                    'afterbegin',
                    '<span class="spinner-border" role="status" aria-hidden="true"></span>'
                );
            });
        });
    }

    /* ------------------------------------------------- delete confirm */

    function initDeleteConfirm() {
        var modalEl = document.getElementById('confirmDeleteModal');
        if (!modalEl || !window.bootstrap) { return; }

        var modal = new window.bootstrap.Modal(modalEl);
        var nameEl = modalEl.querySelector('[data-confirm-name]');
        var formEl = modalEl.querySelector('form');

        document.querySelectorAll('[data-confirm-delete]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                if (nameEl) { nameEl.textContent = btn.getAttribute('data-item-name') || 'this item'; }
                if (formEl) { formEl.action = btn.getAttribute('data-action') || formEl.action; }
                modal.show();
            });
        });
    }

    /* -------------------------------------------------- alert dismissal */

    function initAutoDismiss() {
        document.querySelectorAll('[data-auto-dismiss]').forEach(function (el) {
            var delay = parseInt(el.getAttribute('data-auto-dismiss'), 10);
            if (!delay || !window.bootstrap) { return; }
            window.setTimeout(function () {
                window.bootstrap.Alert.getOrCreateInstance(el).close();
            }, delay);
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initTheme();
        initSidebar();
        initTableFilter();
        initPasswordReveal();
        initSubmitState();
        initDeleteConfirm();
        initAutoDismiss();
    });
})();
