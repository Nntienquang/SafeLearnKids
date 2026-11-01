/* SafeLearn Kids · public pages (login / register): demo account helper + password strength meter.
   Validation and authentication are done on the server (ASP.NET Core Identity + jQuery unobtrusive validation). */
(function () {
    'use strict';
    document.addEventListener('click', function (e) {
        var a = e.target.closest('[data-demo]');
        if (!a) return;
        e.preventDefault();
        var d = a.dataset.demo.split('|');
        var email = document.getElementById('Email'), pass = document.getElementById('Password');
        if (email) email.value = d[0];
        if (pass) { pass.value = d[1]; pass.focus(); }
    });

    var meter = document.getElementById('r-meter'), pw = document.getElementById('Password');
    if (meter && pw) {
        var bars = meter.querySelectorAll('i');
        var score = function (v) {
            var c = 0;
            if (v.length >= 8) c++;
            if (/[A-Z]/.test(v) && /[a-z]/.test(v)) c++;
            if (/\d/.test(v)) c++;
            if (/[^A-Za-z0-9]/.test(v) || v.length >= 12) c++;
            return c;
        };
        var paint = function () {
            var s = score(pw.value);
            bars.forEach(function (b, i) {
                b.style.background = i < s ? (s < 2 ? 'var(--rose)' : s < 3 ? 'var(--sun)' : 'var(--green)') : 'var(--surface-2)';
            });
        };
        pw.addEventListener('input', paint);
        paint();
    }
    if (window.jQuery && window.jQuery.validator) {
        var origRange = window.jQuery.validator.methods.range;
        if (origRange) {
            window.jQuery.validator.methods.range = function (value, element, param) {
                if (element.type === 'checkbox') {
                    return element.checked;
                }
                return origRange.call(this, value, element, param);
            };
        }
        window.jQuery.validator.addMethod('mustbetrue', function (value, element) {
            return element.checked;
        });
        if (window.jQuery.validator.unobtrusive) {
            window.jQuery.validator.unobtrusive.adapters.addBool('mustbetrue');
        }
    }
})();
