/* دليل الأعضاء: بحث وفلترة بالكلية والقسم. */
(function () {
  'use strict';

  ZU.boot({ onReady: function () {
    if (!ZU.requirePermission('members.directory.view')) return;
    ZU.api('/api/members/faculties').done(function (rows) {
      $('#dir-faculty').append((rows || []).map(faculty =>
        `<option value="${faculty.id}">${ZU.escapeHtml(ZU.pick(faculty.nameAr, faculty.nameEn))}</option>`).join(''));
    });
    search();
  } });

  $('#dir-faculty').on('change', function () {
    const facultyId = $(this).val();
    $('#dir-department').html('<option value="">—</option>');
    if (!facultyId) return;
    ZU.api('/api/members/departments?facultyId=' + encodeURIComponent(facultyId)).done(function (rows) {
      $('#dir-department').append((rows || []).map(dept =>
        `<option value="${dept.id}">${ZU.escapeHtml(ZU.pick(dept.nameAr, dept.nameEn))}</option>`).join(''));
    });
  });

  $('#dir-search').on('click', search);
  $('#dir-query').on('keypress', event => { if (event.which === 13) search(); });

  function search() {
    const query = $('#dir-query').val().trim();
    const facultyId = $('#dir-faculty').val();
    const departmentId = $('#dir-department').val();
    const url = '/api/members?query=' + encodeURIComponent(query) +
      '&facultyId=' + encodeURIComponent(facultyId || '') +
      '&departmentId=' + encodeURIComponent(departmentId || '');
    ZU.api(url).done(function (result) {
      const items = result.items || [];
      $('#dir-results').html(items.length ? items.map(member => `
        <article class="entity-card member-card">
          <div class="d-flex gap-3 align-items-start">
            <div class="avatar">${ZU.escapeHtml((member.nameAr || '؟').slice(0, 1))}</div>
            <div>
              <h4>${ZU.escapeHtml(ZU.pick(member.nameAr, member.nameEn))}</h4>
              <div class="small-muted">${ZU.escapeHtml(member.facultyNameAr || '—')}
                ${member.departmentNameAr ? ' · ' + ZU.escapeHtml(member.departmentNameAr) : ''}</div>
              ${member.expertiseAr ? `<div class="small-muted">${ZU.escapeHtml(member.expertiseAr)}</div>` : ''}
              <div class="mt-2">
                <span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('validCertificates'))}: ${member.validCertificates}</span>
              </div>
            </div>
          </div>
        </article>`).join('') : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
      $('#dir-results').append(`<div class="small-muted mt-2">${ZU.escapeHtml(ZU.t('total'))}: ${result.total}</div>`);
    }).fail(function () { $('#dir-results').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }
})();
