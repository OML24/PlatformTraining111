/* لوحة عميد الكلية: المقترحات، التكليف، الاحتياجات، والإلغاء. */
(function () {
  'use strict';

  const S = ZU.state;
  let courses = [];
  let tracks = [];
  const loaded = { courses: true, propose: true };

  ZU.boot({
    page: 'dean',
    onReady: function () {
      if (!ZU.requirePermission('page.dean')) return;
      ZU.api('/api/courses/tracks').done(rows => {
        tracks = rows || [];
        $('#p-track').html(tracks.map(track =>
          `<option value="${track.id}">${ZU.escapeHtml(ZU.pick(track.nameAr, track.nameEn))}</option>`).join(''));
      });
      loadCourses();
      loadStats();
      ZU.initDatePickers($('#tab-propose')[0]);
    }
  });

  $('#dean-tabs').on('click', 'button[data-tab]', function () {
    const tab = $(this).data('tab');
    $('#dean-tabs button').removeClass('active');
    $(this).addClass('active');
    $('main.app-main > section.panel').addClass('d-none');
    $('#tab-' + tab).removeClass('d-none');
    if (!loaded[tab]) {
      loaded[tab] = true;
      if (tab === 'needs') loadNeeds();
      if (tab === 'cancel') { loadCancellations(); renderCancellable(); }
    }
  });

  const statusNames = { 0: 'draft', 1: 'submitted', 2: 'revisionRequested', 3: 'rejected', 4: 'approved', 5: 'inProgress', 6: 'ended', 7: 'cancelled' };

  function badge(status) {
    const map = { 4: 'badge-navy', 5: 'badge-warn', 6: 'badge-ok', 7: 'badge-danger', 2: 'badge-warn', 3: 'badge-danger' };
    return `<span class="badge-soft ${map[status] || 'badge-muted'}">${ZU.escapeHtml(ZU.t(statusNames[status]))}</span>`;
  }

  /* ------------------------------------------------------------- stats */
  function loadStats() {
    ZU.api('/api/statistics/overview').done(function (overview) {
      const cards = [
        ['courses', overview.courses], ['activeCourses', overview.activeCourses],
        ['endedCourses', overview.endedCourses], ['cancelledCourses', overview.cancelledCourses],
        ['enrollments', overview.enrollments], ['validCertificates', overview.validCertificates],
        ['averageAttendance', Math.round(overview.averageAttendancePercent) + '%']
      ];
      $('#dean-stats').html(cards.map(([key, value]) => `
        <div class="stat-card"><span class="stat-value">${ZU.escapeHtml(String(value))}</span>
          <span class="stat-label">${ZU.escapeHtml(ZU.t(key))}</span></div>`).join(''));
    });
  }

  /* ------------------------------------------------------------- courses */
  function loadCourses() {
    ZU.api('/api/courses').done(function (rows) {
      courses = rows || [];
      renderCancellable();
      const host = $('#dean-courses');
      if (!courses.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
      host.html(`<div class="table-responsive"><table class="table align-middle">
        <thead><tr>
          <th>${ZU.escapeHtml(ZU.t('title'))}</th>
          <th>${ZU.escapeHtml(ZU.t('instructor'))}</th>
          <th>${ZU.escapeHtml(ZU.t('status'))}</th>
          <th>${ZU.escapeHtml(ZU.t('actions'))}</th>
        </tr></thead>
        <tbody>${courses.map(course => `<tr data-id="${course.id}">
          <td><strong>${ZU.escapeHtml(ZU.pick(course.titleAr, course.titleEn))}</strong>
            ${course.reviewNoteAr ? `<div class="note-red small">${ZU.escapeHtml(course.reviewNoteAr)}</div>` : ''}</td>
          <td>${ZU.escapeHtml(course.actualInstructorNameAr || '—')}</td>
          <td>${badge(course.status)}</td>
          <td class="d-flex gap-2 flex-wrap">
            ${course.status === 2 ? `<button class="btn btn-sm btn-outline-primary edit-proposal" data-id="${course.id}" data-i18n="resubmit">إعادة الإرسال</button>` : ''}
            <button class="btn btn-sm btn-outline-secondary documents-btn" data-id="${course.id}" data-i18n="documents">الوثائق</button>
            ${course.status === 4 || course.status === 5 ? `<button class="btn btn-sm btn-outline-danger cancel-course" data-id="${course.id}" data-i18n="cancelCourse">طلب إلغاء</button>` : ''}
          </td>
        </tr>`).join('')}</tbody></table></div>`);
      ZU.applyLanguage();
    }).fail(function () { $('#dean-courses').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#refresh-dean').on('click', function () { loadCourses(); loadStats(); });

  $('#dean-courses').on('click', '.documents-btn', function () {
    const id = $(this).data('id');
    ZU.api(`/api/courses/${id}/documents`).done(function (rows) {
      const html = rows && rows.length
        ? rows.map(row => `<a class="btn btn-sm btn-outline-secondary me-2 mb-2" target="_blank"
            href="/api/official-documents/${row.id}">${ZU.escapeHtml(row.originalName)}</a>`).join('')
        : `<div class="small-muted">${ZU.escapeHtml(ZU.t('noDocuments'))}</div>`;
      const box = $(`<div class="panel mt-2"></div>`).html(html);
      $('.docs-panel').remove();
      box.addClass('docs-panel').insertAfter($('#dean-courses'));
    });
  });

  $('#dean-courses').on('click', '.edit-proposal', function () {
    $('#dean-tabs button[data-tab=propose]').trigger('click');
    $('#propose-panel-id').remove();
    $('#propose-form').data('courseId', $(this).data('id'));
    ZU.toast(ZU.t('editProposalHint'));
    ZU.api(`/api/courses/${$(this).data('id')}`).done(function (detail) {
      $('#p-title').val(detail.course.titleAr);
      $('#p-obj').val(detail.objectivesAr);
      $('#p-audience').val(detail.targetAudienceAr);
      $('#p-hours').val(detail.plannedHours);
      $('#p-mode').val(String(detail.deliveryMode));
      $('#p-track').val(detail.course.trainingTrackId);
      $('#p-start').val(ZU.toLocalInput(detail.plannedStartUtc));
      $('#p-end').val(ZU.toLocalInput(detail.plannedEndUtc));
      $('#p-eval').val(ZU.toLocalInput(detail.evaluationDeadlineUtc));
      $('#p-exam').prop('checked', detail.course.hasExam);
    });
  });

  $('#dean-courses').on('click', '.cancel-course', function () {
    const id = $(this).data('id');
    $('#dean-tabs button[data-tab=cancel]').trigger('click');
    $('#dc-course').val(id);
    $('#dc-reason').focus();
  });

  function renderCancellable() {
    const options = courses.filter(course => course.status === 4 || course.status === 5)
      .map(course => `<option value="${course.id}">${ZU.escapeHtml(ZU.pick(course.titleAr, course.titleEn))}</option>`)
      .join('');
    $('#dc-course').html(options || `<option value="">${ZU.escapeHtml(ZU.t('noRows'))}</option>`);
  }

  $('#dean-cancel-form').on('submit', function (event) {
    event.preventDefault();
    const id = $('#dc-course').val();
    const file = $('#dc-file')[0].files[0];
    const reason = $('#dc-reason').val().trim();
    if (!id) { ZU.alertBox('#cancel-message', 'لا توجد دورة قابلة للإلغاء.', false); return; }
    if (!reason || !file) { ZU.alertBox('#cancel-message', 'أدخل السبب وأرفق الكتاب الرسمي.', false); return; }
    const data = new FormData();
    data.append('reasonAr', reason);
    data.append('file', file);
    ZU.apiForm(`/api/course-cancellations/${id}`, 'POST', data)
      .done(function () {
        ZU.clearAlert('#cancel-message');
        ZU.toast(ZU.t('saved'), true);
        $('#dean-cancel-form')[0].reset();
        loadCancellations();
        loadCourses();
      })
      .fail(function (xhr) {
        ZU.alertBox('#cancel-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false);
      });
  });

  /* ------------------------------------------------------------- propose */
  $('#p-source').on('change', function () {
    const external = $(this).val() === 'external';
    $('#external-box').toggleClass('d-none', !external);
    $('#internal-box').toggleClass('d-none', external);
  });

  $('#p-search-btn').on('click', function () {
    const query = $('#p-search').val().trim();
    if (!query) return;
    ZU.api('/api/courses/directory-users?query=' + encodeURIComponent(query) +
      '&facultyId=' + encodeURIComponent(S.account.facultyId || ''))
      .done(function (rows) {
        const list = (rows || []).slice(0, 8);
        $('#p-search-results').html(list.length ? list.map(user => `<button type="button"
          class="btn btn-sm btn-outline-secondary me-1 mb-1 pick-instructor" data-id="${user.id}"
          data-name="${ZU.escapeHtml(ZU.pick(user.nameAr, user.nameEn))}">
          ${ZU.escapeHtml(ZU.pick(user.nameAr, user.nameEn))} · ${ZU.escapeHtml(user.loginName)}</button>`).join('')
          : ZU.escapeHtml(ZU.t('noRows')));
      });
  });

  $('#p-search-results').on('click', '.pick-instructor', function () {
    $('#p-instructor-id').val($(this).data('id'));
    $('#p-search-results').html(`<span class="badge-soft badge-ok">${ZU.escapeHtml($(this).data('name'))}</span>`);
  });

  $('#propose-form').on('submit', function (event) {
    event.preventDefault();
    const external = $('#p-source').val() === 'external';
    const courseId = $(this).data('courseId');
    const start = ZU.toIso($('#p-start').val());
    const end = ZU.toIso($('#p-end').val());
    if (!start || !end) { ZU.alertBox('#propose-message', ZU.t('datesRequired'), false); return; }
    if (new Date(end) <= new Date(start)) { ZU.alertBox('#propose-message', ZU.t('endAfterStart'), false); return; }
    const externalName = $('#p-ext-name').val().trim();
    const payload = {
      titleAr: $('#p-title').val().trim(),
      titleEn: $('#p-title-en').val().trim() || null,
      trainingTrackId: $('#p-track').val(),
      objectivesAr: $('#p-obj').val().trim(),
      objectivesEn: null,
      targetAudienceAr: $('#p-audience').val().trim(),
      targetAudienceEn: null,
      deliveryMode: Number($('#p-mode').val()),
      plannedHours: Number($('#p-hours').val()),
      plannedStartUtc: start,
      plannedEndUtc: end,
      facultyId: S.account.facultyId || null,
      instructorUserId: external ? null : ($('#p-instructor-id').val() || null),
      actualInstructorNameAr: external ? externalName : null,
      actualInstructorNameEn: null,
      qualityOperated: false,
      hasExam: $('#p-exam').is(':checked'),
      enrollmentMode: Number($('#p-enroll').val()),
      evaluationDeadlineUtc: ZU.toIso($('#p-eval').val())
    };
    if (!payload.titleAr || !payload.objectivesAr || !payload.targetAudienceAr) {
      ZU.alertBox('#propose-message', 'أكمل الحقول الإلزامية.', false); return;
    }
    if (!external && !payload.instructorUserId) {
      ZU.alertBox('#propose-message', 'اختر المحاضر من أعضاء كليتك.', false); return;
    }
    if (external && !externalName) {
      ZU.alertBox('#propose-message', 'أدخل اسم الخبير الخارجي.', false); return;
    }
    const letter = $('#p-ext-letter')[0].files[0];
    const cv = $('#p-ext-cv')[0].files[0];
    if (external && (!letter || !cv)) {
      ZU.alertBox('#propose-message', 'أرفق الكتاب الرسمي والسيرة الذاتية.', false); return;
    }

    const call = courseId
      ? ZU.api(`/api/courses/${courseId}/resubmit`, 'PUT', payload)
      : ZU.api('/api/courses', 'POST', payload);

    call.done(function (result) {
      const newId = courseId || result.id;
      if (!external) {
        ZU.clearAlert('#propose-message');
        ZU.toast(ZU.t('saved'), true);
        $('#propose-form')[0].reset();
        $('#propose-form').removeData('courseId');
        loadCourses();
        return;
      }
      const letterData = new FormData();
      letterData.append('file', letter);
      ZU.apiForm(`/api/courses/${newId}/documents?kind=ExternalTrainerLetter`, 'POST', letterData)
        .always(function () {
          const cvData = new FormData();
          cvData.append('file', cv);
          ZU.apiForm(`/api/courses/${newId}/documents?kind=ExternalTrainerCv`, 'POST', cvData)
            .done(function () {
              ZU.toast(ZU.t('saved'), true);
              $('#propose-form')[0].reset();
              loadCourses();
            })
            .fail(function (xhr) {
              ZU.alertBox('#propose-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false);
            });
        });
    }).fail(function (xhr) {
      ZU.alertBox('#propose-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false);
    });
  });

  /* ------------------------------------------------------------- needs */
  function loadNeeds() {
    ZU.api('/api/training-needs').done(function (rows) {
      const host = $('#dean-needs');
      if (!rows || !rows.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
      host.html(`<div class="table-responsive"><table class="table align-middle">
        <thead><tr><th>${ZU.escapeHtml(ZU.t('title'))}</th><th>${ZU.escapeHtml(ZU.t('status'))}</th>
          <th>${ZU.escapeHtml(ZU.t('reviewNote'))}</th><th>${ZU.escapeHtml(ZU.t('documents'))}</th></tr></thead>
        <tbody>${rows.map(row => `<tr>
          <td>${ZU.escapeHtml(ZU.pick(row.titleAr, row.titleEn))}</td>
          <td>${row.status === 2 ? `<span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('approved'))}</span>`
            : row.status === 3 ? `<span class="badge-soft badge-danger">${ZU.escapeHtml(ZU.t('rejected'))}</span>`
            : row.status === 1 ? `<span class="badge-soft badge-warn">${ZU.escapeHtml(ZU.t('revisionRequested'))}</span>`
            : `<span class="badge-soft badge-muted">${ZU.escapeHtml(ZU.t('pending'))}</span>`}</td>
          <td class="note-red">${ZU.escapeHtml(row.reviewNoteAr || '')}</td>
          <td>${(row.documentIds || []).map(id => `<a class="btn btn-sm btn-outline-secondary" target="_blank"
            href="/api/official-documents/${id}">${ZU.escapeHtml(ZU.t('file'))}</a>`).join(' ')}</td>
        </tr>`).join('')}</tbody></table></div>`);
    }).fail(function () { $('#dean-needs').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#dean-need-form').on('submit', function (event) {
    event.preventDefault();
    const file = $('#dn-file')[0].files[0];
    if (!file) { ZU.alertBox('#need-message', 'أرفق الكتاب الرسمي.', false); return; }
    const data = new FormData();
    data.append('titleAr', $('#dn-title').val().trim());
    data.append('descriptionAr', $('#dn-desc').val().trim());
    data.append('file', file);
    ZU.apiForm('/api/training-needs', 'POST', data)
      .done(function () {
        ZU.clearAlert('#need-message');
        ZU.toast(ZU.t('saved'), true);
        $('#dean-need-form')[0].reset();
        loadNeeds();
      })
      .fail(function (xhr) { ZU.alertBox('#need-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------- cancellations */
  function loadCancellations() {
    ZU.api('/api/course-cancellations').done(function (rows) {
      const host = $('#dean-cancel');
      if (!rows || !rows.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
      host.html(`<h2>${ZU.escapeHtml(ZU.t('cancellations'))}</h2>
        <div class="table-responsive"><table class="table align-middle">
        <thead><tr><th>${ZU.escapeHtml(ZU.t('courses'))}</th><th>${ZU.escapeHtml(ZU.t('reason'))}</th>
          <th>${ZU.escapeHtml(ZU.t('status'))}</th><th>${ZU.escapeHtml(ZU.t('reviewNote'))}</th>
          <th>${ZU.escapeHtml(ZU.t('documents'))}</th></tr></thead>
        <tbody>${rows.map(row => `<tr>
          <td>${ZU.escapeHtml(row.courseTitleAr)}</td>
          <td class="small-muted">${ZU.escapeHtml(row.reasonAr)}</td>
          <td>${row.status === 2 ? `<span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('approved'))}</span>`
            : row.status === 3 ? `<span class="badge-soft badge-danger">${ZU.escapeHtml(ZU.t('rejected'))}</span>`
            : `<span class="badge-soft badge-warn">${ZU.escapeHtml(ZU.t('pending'))}</span>`}</td>
          <td class="note-red">${ZU.escapeHtml(row.reviewNoteAr || '')}</td>
          <td>${(row.documentIds || []).map(id => `<a class="btn btn-sm btn-outline-secondary" target="_blank"
            href="/api/official-documents/${id}">${ZU.escapeHtml(ZU.t('file'))}</a>`).join(' ')}</td>
        </tr>`).join('')}</tbody></table></div>`);
    }).fail(function () { $('#dean-cancel').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }
})();
