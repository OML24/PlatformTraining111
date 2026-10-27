/* تغيير كلمة المرور الإلزامي بعد أول دخول. */
(function () {
  'use strict';

  ZU.boot({
    allowPasswordChange: true,
    onReady: function (account) {
      if (!account) return;
      ZU.api('/api/settings/password-policy').done(function (policy) {
        $('#new-password').attr('minlength', policy.minimumLength).attr('maxlength', policy.maximumLength);
        $('#policy-note').text(`${policy.minimumLength}–${policy.maximumLength} ` +
          (ZU.state.lang === 'ar' ? 'حرفًا على الأقل، ويُمنع استخدام رقمك الوظيفي.' : 'characters, and your login must not appear.'));
      });
    }
  });

  $('#change-form').on('submit', function (event) {
    event.preventDefault();
    const oldPassword = $('#old-password').val();
    const newPassword = $('#new-password').val();
    const confirm = $('#confirm-password').val();
    if (!oldPassword || !newPassword) { ZU.alertBox('#password-message', 'أكمل جميع الحقول.', false); return; }
    if (newPassword !== confirm) { ZU.alertBox('#password-message', 'تأكيد كلمة المرور غير مطابق.', false); return; }

    const button = $(this).find('button[type=submit]').prop('disabled', true);
    ZU.api('/api/auth/change-password', 'POST', { oldPassword: oldPassword, newPassword: newPassword, rememberMe: false })
      .done(function () {
        ZU.toast(ZU.t('saved'), true);
        location.href = '/dashboard.html';
      })
      .fail(function (xhr) {
        ZU.alertBox('#password-message',
          (xhr.responseJSON && xhr.responseJSON.message) || 'تعذر تغيير كلمة المرور.', false);
      })
      .always(function () { button.prop('disabled', false); });
  });
})();
