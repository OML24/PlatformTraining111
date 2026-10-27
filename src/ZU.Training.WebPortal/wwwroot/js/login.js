/* صفحة الدخول واستعادة كلمة المرور. */
(function () {
  'use strict';

  ZU.boot({
    public: true,
    onReady: function () {
      // إن كانت الجلسة قائمة ننتقل مباشرة إلى لوحة المستخدم.
      ZU.api('/api/auth/me').done(function (account) {
        location.href = ZU.homeFor(account);
      });
    }
  });

  function showForgot(show) {
    $('#login-view').toggleClass('d-none', show);
    $('#forgot-view').toggleClass('d-none', !show);
  }

  $('#login-form').on('submit', function (event) {
    event.preventDefault();
    const loginName = $('#login-name').val().trim();
    const password = $('#login-password').val();
    if (!loginName || !password) {
      ZU.alertBox('#login-message', ZU.t('loginHelp'), false);
      return;
    }
    const button = $(this).find('button[type=submit]').prop('disabled', true);
    ZU.api('/api/auth/login', 'POST', { loginName: loginName, password: password, rememberMe: $('#remember-me').is(':checked') })
      .done(function (result) {
        ZU.clearAlert('#login-message');
        if (result.mustChangePassword) { location.href = '/change-password.html'; return; }
        ZU.api('/api/auth/me').done(function (account) { location.href = ZU.homeFor(account); })
          .fail(function () { location.href = '/member.html'; });
      })
      .fail(function (xhr) {
        ZU.alertBox('#login-message',
          (xhr.responseJSON && xhr.responseJSON.message) || 'بيانات الدخول غير صحيحة أو الحساب غير متاح.', false);
      })
      .always(function () { button.prop('disabled', false); });
  });

  $('#open-forgot').on('click', function () { showForgot(true); ZU.clearAlert('#forgot-message'); });
  $('#back-to-login').on('click', function () { showForgot(false); });

  $('#forgot-form').on('submit', function (event) {
    event.preventDefault();
    const loginName = $('#forgot-login').val().trim();
    const email = $('#forgot-email').val().trim();
    if (!loginName || !email) { ZU.alertBox('#forgot-message', 'أدخل الرقم الوظيفي والبريد الجامعي.', false); return; }
    ZU.api('/api/auth/forgot-password', 'POST', { loginName: loginName, email: email })
      .done(function () { ZU.alertBox('#forgot-message', ZU.t('resetSent')); })
      .fail(function () { ZU.alertBox('#forgot-message', ZU.t('resetSent')); });
  });
})();
