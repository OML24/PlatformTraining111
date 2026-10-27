/* التحقق العام من الشهادات عبر الرقم المرجعي. */
(function () {
  'use strict';

  ZU.boot({ public: true, onReady: function () {
    const reference = new URLSearchParams(location.search).get('cert');
    if (reference) { $('#cert').val(reference); run(reference); }
  } });

  function run(reference) {
    const host = $('#verify-result').html(`<div class="loading">${ZU.escapeHtml(ZU.t('loading'))}</div>`);
    ZU.api('/api/certificates/verify?cert=' + encodeURIComponent(reference))
      .done(function (result) {
        const revoked = result.status === 1;
        host.html(`
          <div class="panel mb-0" style="border-color:${revoked ? '#f5c9c4' : '#bfe8d8'}">
            <div class="d-flex justify-content-between align-items-start gap-3 flex-wrap">
              <div>
                <span class="badge-soft ${revoked ? 'badge-danger' : 'badge-ok'}">
                  ${ZU.escapeHtml(revoked ? ZU.t('certificateRevoked') : ZU.t('certificateValid'))}
                </span>
                <h3 class="mt-3 mb-1">${ZU.escapeHtml(ZU.pick(result.courseTitleAr, result.courseTitleEn))}</h3>
                <div class="muted">${ZU.escapeHtml(ZU.pick(result.recipientNameAr, result.recipientNameEn))}</div>
              </div>
              <div class="qr-box"><img alt="QR" src="/api/certificates/qr?cert=${encodeURIComponent(result.referenceNumber)}" width="132" height="132"></div>
            </div>
            <hr>
            <dl class="row mb-0">
              <dt class="col-sm-4">${ZU.escapeHtml(ZU.t('referenceNumber'))}</dt><dd class="col-sm-8">${ZU.escapeHtml(result.referenceNumber)}</dd>
              <dt class="col-sm-4">${ZU.escapeHtml(ZU.t('instructor'))}</dt><dd class="col-sm-8">${ZU.escapeHtml(ZU.pick(result.instructorNameAr, result.instructorNameEn))}</dd>
              <dt class="col-sm-4">${ZU.escapeHtml(ZU.t('issuedAt'))}</dt><dd class="col-sm-8">${ZU.escapeHtml(ZU.fmtDate(result.issuedAtUtc))}</dd>
              ${revoked && result.statusReason ? `<dt class="col-sm-4">${ZU.escapeHtml(ZU.t('reviewNote'))}</dt><dd class="col-sm-8 note-red">${ZU.escapeHtml(result.statusReason)}</dd>` : ''}
            </dl>
          </div>`);
      })
      .fail(function (xhr) {
        host.html(`<div class="alert alert-danger mb-0">${ZU.escapeHtml(
          (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('certificateNotFound'))}</div>`);
      });
  }

  $('#verify-form').on('submit', function (event) {
    event.preventDefault();
    const reference = $('#cert').val().trim();
    if (!reference) return;
    history.replaceState(null, '', '/verify.html?cert=' + encodeURIComponent(reference));
    run(reference);
  });
})();
