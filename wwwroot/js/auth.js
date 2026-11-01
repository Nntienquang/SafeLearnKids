(function () {
    'use strict';
    var $ = window.jQuery;

    // Replace jQuery's numeric Range adapter for boolean consent before unobtrusive parsing.
    if ($ && $.validator && $.validator.unobtrusive) {
        $.validator.addMethod('agreetrue', function (_, element) { return element.checked; });
        $.validator.unobtrusive.adapters.addBool('agreetrue');
        document.querySelectorAll('[data-auth-form] [data-auth-agree]').forEach(function (input) {
            input.setAttribute('data-val-agreetrue', input.getAttribute('data-val-range') || 'Hãy xác nhận bạn là phụ huynh và đồng ý điều khoản.');
            ['data-val-range', 'data-val-range-min', 'data-val-range-max'].forEach(function (name) { input.removeAttribute(name); });
        });
    }

    function init() {
        var toggles = document.querySelectorAll('[data-password-toggle]');
        function concealPasswords() {
            toggles.forEach(function (button) {
                var input = document.getElementById(button.dataset.passwordToggle);
                if (!input) return;
                input.type = 'password';
                button.textContent = 'Hiện';
                button.setAttribute('aria-label', button.dataset.showLabel);
                button.setAttribute('aria-pressed', 'false');
            });
        }
        toggles.forEach(function (button) {
            var input = document.getElementById(button.dataset.passwordToggle);
            if (!input) return;
            button.dataset.showLabel = button.getAttribute('aria-label');
            button.hidden = false;
            button.addEventListener('click', function (event) {
                // Also suppress the shared UI's fallback button toast.
                event.preventDefault();
                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                button.textContent = show ? 'Ẩn' : 'Hiện';
                button.setAttribute('aria-label', show ? button.dataset.showLabel.replace(/^Hiện/, 'Ẩn') : button.dataset.showLabel);
                button.setAttribute('aria-pressed', String(show));
            });
        });

        var meter = document.getElementById('r-meter');
        var password = document.getElementById('Password');
        if (meter && password) {
            var labels = ['Độ mạnh tham khảo; mật khẩu sẽ được kiểm tra khi đăng ký.', 'Độ mạnh tham khảo: yếu.', 'Độ mạnh tham khảo: trung bình.', 'Độ mạnh tham khảo: khá.', 'Độ mạnh tham khảo: mạnh.'];
            var paint = function () {
                var value = password.value;
                var score = value ? Number(value.length >= 8) + Number(/[A-Z]/.test(value) && /[a-z]/.test(value)) + Number(/\d/.test(value)) + Number(/[^A-Za-z0-9]/.test(value) || value.length >= 12) : 0;
                meter.querySelectorAll('i').forEach(function (bar, i) {
                    bar.style.background = i < score ? (score < 2 ? 'var(--rose)' : score < 3 ? 'var(--sun)' : 'var(--green)') : 'var(--surface-2)';
                });
                document.getElementById('password-strength').textContent = labels[value ? Math.max(1, score) : 0];
            };
            password.addEventListener('input', paint);
            paint();
        }

        var resetters = [];
        document.querySelectorAll('[data-auth-form]').forEach(function (form) {
            var button = form.querySelector('button[type="submit"]');
            var status = form.querySelector('[data-submit-status]');
            if (!button) return;
            var originalLabel = button.textContent;
            function reset() {
                delete form.dataset.submitting;
                form.removeAttribute('aria-busy');
                button.disabled = false;
                button.textContent = originalLabel;
                if (status) status.textContent = '';
            }
            resetters.push(reset);
            if ($) {
                var validator = $(form).data('validator');
                if (validator) {
                    validator.settings.focusInvalid = false;
                    // Avoid moving controls between pointer-down and click when blur changes errors.
                    validator.settings.onfocusout = false;
                }
                $(form).on('invalid-form.auth', function (_, invalidValidator) {
                    reset();
                    if (status) status.textContent = 'Vui lòng kiểm tra các mục được đánh dấu.';
                    var first = invalidValidator.errorList[0];
                    if (first) first.element.focus();
                });
            }
            form.addEventListener('submit', function (event) {
                if (event.defaultPrevented) return;
                if (form.dataset.submitting) { event.preventDefault(); return; }
                if ($ && $.validator && !$(form).valid()) { event.preventDefault(); return; }
                form.dataset.submitting = 'true';
                form.setAttribute('aria-busy', 'true');
                button.disabled = true;
                button.textContent = form.dataset.submitLabel || 'Đang xử lý…';
                if (status) status.textContent = 'Đang xử lý. Vui lòng chờ.';
            });
        });

        var feedback = Array.from(document.querySelectorAll('[data-auth-feedback]')).find(function (element) {
            return element.getClientRects().length && element.textContent.trim();
        });
        var firstError = document.querySelector('[data-auth-form] .input-validation-error:not([type="hidden"])');
        if (feedback) feedback.focus();
        else if (firstError) firstError.focus();

        window.addEventListener('pageshow', function () {
            resetters.forEach(function (reset) { reset(); });
            concealPasswords();
        });
    }
    // The earlier jQuery ready callback parses unobtrusive rules before init.
    if ($) $(init);
    else if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
