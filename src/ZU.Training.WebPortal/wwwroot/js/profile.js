/* الملف الشخصي للعضو: البيانات المهنية والشهادات. */
(function () {
  'use strict';

  ZU.boot({ onReady: function () {
    loadProfile();
    loadCertificates();
  } });

  function loadProfile() {
    ZU.api('/api/members/me').done(function (detail) {
      const member = detail.member || detail.Member || {};
      $('#profile-head').html(`
        <div class="d-flex align-items-center gap-3 flex-wrap">
          <div class="avatar">${ZU.escapeHtml((member.nameAr || '؟').slice(0, 1))}</div>
          <div>
            <h3 class="h5 mb-0">${ZU.escapeHtml(ZU.pick(member.nameAr, member.nameEn))}</h3>
            <div class="small-muted">${ZU.escapeHtml(member.loginName || '')} ·
              ${ZU.escapeHtml(ZU.pick(member.facultyNameAr || '—', member.facultyNameEn || ''))}
              ${member.departmentNameAr ? ' · ' + ZU.escapeHtml(member.departmentNameAr) : ''}</div>
          </div>
          <div class="badge-soft badge-ok ms-auto">${ZU.escapeHtml(ZU.t('validCertificates'))}: ${member.validCertificates || 0}</div>
        </div>`);
      $('#pf-expertise').val(member.expertiseAr || '');
      $('#pf-bio').val(detail.bioAr || '');
      $('#pf-email').val(detail.contactEmail || '');
      $('#pf-phone').val(detail.phone || '');
      $('#pf-office').val(detail.officeAr || '');
    }).fail(function () { $('#profile-head').html(''); });
  }

  $('#profile-form').on('submit', function (event) {
    event.preventDefault();
    ZU.api('/api/members/me', 'PUT', {
      bioAr: $('#pf-bio').val().trim() || null,
      bioEn: null,
      expertiseAr: $('#pf-expertise').val().trim() || null,
      expertiseEn: null,
      contactEmail: $('#pf-email').val().trim() || null,
      phone: $('#pf-phone').val().trim() || null,
      officeAr: $('#pf-office').val().trim() || null
    }).done(function () { ZU.toast(ZU.t('saved'), true); loadProfile(); })
      .fail(function (xhr) { ZU.alertBox('#profile-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  function loadCertificates() {
    ZU.api('/api/certificates/mine').done(function (profile) {
      const certificates = profile.certificates || [];
      $('#certificate-cards').html(certificates.length ? certificates.map(card => `
        <article class="entity-card certificate-card">
          <div class="d-flex justify-content-between align-items-start gap-2">
            <div>
              <h4>${ZU.escapeHtml(ZU.pick(card.courseTitleAr, card.courseTitleEn))}</h4>
              <div class="small-muted">${ZU.escapeHtml(ZU.t('referenceNumber'))}: ${ZU.escapeHtml(card.referenceNumber)}</div>
              <div class="small-muted">${ZU.escapeHtml(ZU.t('issuedAt'))}: ${ZU.escapeHtml(ZU.fmtDate(card.issuedAtUtc))}</div>
            </div>
            <div class="qr-box"><img alt="QR" width="96" height="96"
              src="/api/certificates/qr?cert=${encodeURIComponent(card.referenceNumber)}"></div>
          </div>
          <div class="mt-2">
            ${card.status === 0 ? `<span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('certificateValid'))}</span>`
              : `<span class="badge-soft badge-danger">${ZU.escapeHtml(ZU.t('certificateRevoked'))}</span>`}
            ${card.statusReason ? `<span class="small-muted">${ZU.escapeHtml(card.statusReason)}</span>` : ''}
          </div>
          <div class="d-flex gap-2 mt-3">
            ${card.canDownload ? `<a class="btn btn-sm btn-primary" href="/api/certificates/${card.id}/file"
                target="_blank" data-i18n="downloadCertificate">تنزيل الشهادة</a>` : ''}
            <a class="btn btn-sm btn-outline-primary" target="_blank"
               href="${ZU.escapeHtml(card.verificationUrl || ('/verify.html?cert=' + encodeURIComponent(card.referenceNumber)))}"
               data-i18n="verify">تحقق</a>
          </div>
        </article>`).join('') : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noCertificates'))}</div>`);

      const participations = profile.participations || [];
      $('#participation-list').html(participations.length ? `
        <h3 class="h6">${ZU.escapeHtml(ZU.t('participationStatus'))}</h3>
        <div class="table-responsive"><table class="table table-sm align-middle">
          <thead><tr><th>${ZU.escapeHtml(ZU.t('courses'))}</th><th>${ZU.escapeHtml(ZU.t('attendance'))}</th>
            <th>${ZU.escapeHtml(ZU.t('exam'))}</th><th>${ZU.escapeHtml(ZU.t('evaluation'))}</th>
            <th>${ZU.escapeHtml(ZU.t('impact'))}</th><th>${ZU.escapeHtml(ZU.t('eligible'))}</th></tr></thead>
          <tbody>${participations.map(row => `<tr>
            <td>${ZU.escapeHtml(ZU.pick(row.courseTitleAr, row.courseTitleEn))}</td>
            <td>${row.attendedLectures}/${row.totalLectures}</td>
            <td>${!row.hasExam ? '—' : row.examOutcome === 1 ? '✓' : row.examOutcome === 2 ? '✗' : '—'}</td>
            <td>${row.evaluationCompleted ? '✓' : '—'}</td>
            <td>${row.impactApproved ? '✓' : '—'}</td>
            <td>${row.isEligible ? `<span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('eligible'))}</span>`
              : `<span class="badge-soft badge-warn">${ZU.escapeHtml(ZU.t('notEligible'))}</span>`}</td>
          </tr>`).join('')}</tbody></table></div>` : '');
      ZU.applyLanguage();
    }).fail(function () {
      $('#certificate-cards').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noCertificates'))}</div>`);
    });
  }
})();
