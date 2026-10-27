/* مساحة عمل المحاضر: المقترحات، المحاضرات، الحضور، الامتحان، الأثر، الشهادات. */
(function () {
  'use strict';

  const S = ZU.state;
  let courses = [];
  let tracks = [];
  let faculties = [];
  let detail = null;
  let activeTab = 'lectures';

  ZU.boot({
    page: 'instructor',
    onReady: function () {
      if (!ZU.requirePermission('page.instructor')) return;
      Promise.all([
        ZU.api('/api/courses').done(rows => { courses = rows || []; }),
        ZU.api('/api/courses/tracks').done(rows => { tracks = rows || []; renderTrackOptions(); }),
        ZU.api('/api/courses/faculties').done(rows => { faculties = rows || []; renderFacultyOptions(); })
      ]).always(function () { renderCourses(); });
    }
  });

  /* ------------------------------------------------------------------ shared */
  const statusNames = {
    0: 'draft', 1: 'submitted', 2: 'revisionRequested', 3: 'rejected', 4: 'approved',
    5: 'inProgress', 6: 'ended', 7: 'cancelled'
  };

  function statusBadge(status) {
    const map = { 4: 'badge-navy', 5: 'badge-warn', 6: 'badge-ok', 7: 'badge-danger', 2: 'badge-warn', 3: 'badge-danger' };
    return `<span class="badge-soft ${map[status] || 'badge-muted'}">${ZU.escapeHtml(ZU.t(statusNames[status]))}</span>`;
  }

  function modeName(mode) { return ZU.t(mode === 1 ? 'online' : mode === 2 ? 'hybrid' : 'inPerson'); }

  function mine(course) {
    return course.instructorUserId === S.account.id || course.createdByUserId === S.account.id;
  }

  function renderTrackOptions() {
    $('#c-track').html(tracks.map(track =>
      `<option value="${track.id}">${ZU.escapeHtml(ZU.pick(track.nameAr, track.nameEn))}</option>`).join(''));
  }

  function renderFacultyOptions() {
    $('#c-faculty').html('<option value="">—</option>' + faculties.map(faculty =>
      `<option value="${faculty.id}">${ZU.escapeHtml(ZU.pick(faculty.nameAr, faculty.nameEn))}</option>`).join(''));
  }

  /* ------------------------------------------------------------------ list */
  function renderCourses() {
    const host = $('#course-list');
    const rows = courses.filter(mine);
    if (!rows.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
    host.html(rows.map(course => `
      <article class="entity-card">
        <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
          <h4>${ZU.escapeHtml(ZU.pick(course.titleAr, course.titleEn))}</h4>
          ${statusBadge(course.status)}
        </div>
        <div class="meta">
          ${ZU.escapeHtml(ZU.t('instructor'))}: ${ZU.escapeHtml(course.actualInstructorNameAr || '—')}<br>
          ${ZU.escapeHtml(course.hasExam ? ZU.t('examRequired') : ZU.t('examNotRequired'))}
          ${course.qualityOperated ? ` · ${ZU.escapeHtml(ZU.t('qualityOperated'))}` : ''}
        </div>
        ${course.reviewNoteAr ? `<div class="note-red mt-2">${ZU.escapeHtml(course.reviewNoteAr)}</div>` : ''}
        <div class="d-flex gap-2 mt-3 flex-wrap">
          <button class="btn btn-sm btn-primary manage-course" data-id="${course.id}" data-i18n="manage">إدارة</button>
          ${course.status === 2 ? `<button class="btn btn-sm btn-outline-primary resubmit-course" data-id="${course.id}" data-i18n="resubmit">إعادة الإرسال</button>` : ''}
        </div>
      </article>`).join(''));
    ZU.applyLanguage();
  }

  /* ------------------------------------------------------------------ create */
  $('#new-course-toggle').on('click', function () {
    $('#create-panel').removeClass('d-none');
    ZU.initDatePickers($('#create-panel')[0]);
    $('html, body').animate({ scrollTop: $('#create-panel').offset().top - 90 }, 200);
  });
  $('#close-create').on('click', () => $('#create-panel').addClass('d-none'));

  function createPayload() {
    const start = ZU.toIso($('#c-start').val());
    const end = ZU.toIso($('#c-end').val());
    const deadline = ZU.toIso($('#c-eval').val());
    if (!start || !end) throw new Error(ZU.t('datesRequired'));
    if (new Date(end) <= new Date(start)) throw new Error(ZU.t('endAfterStart'));
    return {
      titleAr: $('#c-title-ar').val().trim(),
      titleEn: $('#c-title-en').val().trim() || null,
      trainingTrackId: $('#c-track').val(),
      objectivesAr: $('#c-obj-ar').val().trim(),
      objectivesEn: null,
      targetAudienceAr: $('#c-audience-ar').val().trim(),
      targetAudienceEn: null,
      deliveryMode: Number($('#c-mode').val()),
      plannedHours: Number($('#c-hours').val()),
      plannedStartUtc: start,
      plannedEndUtc: end,
      facultyId: $('#c-faculty').val() || null,
      instructorUserId: S.account.id,
      actualInstructorNameAr: ZU.pick(S.account.nameAr, S.account.nameEn),
      actualInstructorNameEn: S.account.nameEn || null,
      qualityOperated: $('#c-quality').is(':checked'),
      hasExam: $('#c-exam').is(':checked'),
      enrollmentMode: Number($('#c-enroll').val()),
      evaluationDeadlineUtc: deadline
    };
  }

  $('#create-form').on('submit', function (event) {
    event.preventDefault();
    let payload;
    try { payload = createPayload(); } catch (error) { ZU.alertBox('#create-message', error.message, false); return; }
    if (!payload.titleAr || !payload.objectivesAr || !payload.targetAudienceAr || !payload.trainingTrackId) {
      ZU.alertBox('#create-message', 'أكمل الحقول الإلزامية.', false); return;
    }
    ZU.api('/api/courses', 'POST', payload)
      .done(function () {
        ZU.clearAlert('#create-message');
        ZU.toast(ZU.t('saved'), true);
        $('#create-form')[0].reset();
        $('#create-panel').addClass('d-none');
        reloadCourses();
      })
      .fail(function (xhr) {
        ZU.alertBox('#create-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false);
      });
  });

  $('#course-list').on('click', '.resubmit-course', function () {
    const id = $(this).data('id');
    let payload;
    try { payload = createPayload(); } catch (error) { ZU.toast(error.message, false); return; }
    ZU.api(`/api/courses/${id}/resubmit`, 'PUT', payload)
      .done(function () { ZU.toast(ZU.t('saved'), true); reloadCourses(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  function reloadCourses() { ZU.api('/api/courses').done(rows => { courses = rows || []; renderCourses(); }); }

  /* ------------------------------------------------------------------ manage */
  $('#course-list').on('click', '.manage-course', function () { openManage($(this).data('id')); });
  $('#close-manage').on('click', () => $('#manage-panel').addClass('d-none'));
  $('#refresh-course').on('click', () => detail && openManage(detail.course.id));

  $('#manage-tabs').on('click', 'button[data-tab]', function () {
    activeTab = $(this).data('tab');
    $('#manage-tabs button').removeClass('active');
    $(this).addClass('active');
    $('.tab-body').addClass('d-none');
    $('#tab-' + activeTab).removeClass('d-none');
  });

  function openManage(courseId) {
    ZU.api(`/api/courses/${courseId}`).done(function (result) {
      detail = result;
      $('#manage-panel').removeClass('d-none');
      $('#manage-title').text(ZU.pick(result.course.titleAr, result.course.titleEn));
      $('#manage-meta').html([
        statusBadge(result.course.status),
        `${ZU.escapeHtml(ZU.t('plannedHours'))}: ${result.plannedHours}`,
        `${ZU.escapeHtml(ZU.fmtDateTime(result.plannedStartUtc))} ← ${ZU.escapeHtml(ZU.fmtDateTime(result.plannedEndUtc))}`,
        `${ZU.escapeHtml(ZU.t('participants'))}: ${result.participants.length}`,
        `${ZU.escapeHtml(ZU.t('lectures'))}: ${result.lectures.length}`
      ].join(' · '));
      renderLectures();
      renderAttendance();
      renderExam();
      renderActions();
      loadImpactRoster();
      loadCertificates();
      $('html, body').animate({ scrollTop: $('#manage-panel').offset().top - 90 }, 220);
    }).fail(function () { ZU.toast(ZU.t('failed'), false); });
  }

  /* ------------------------------------------------------------------ lectures */
  function renderLectures() {
    const host = $('#tab-lectures');
    const lectures = detail.lectures || [];
    host.html(`
      <form id="lecture-form" class="row g-2 align-items-end mb-4">
        <div class="col-md-4">
          <label class="form-label" data-i18n="titleAr">عنوان المحاضرة</label>
          <input id="l-title" class="form-control" maxlength="300" required>
        </div>
        <div class="col-md-3">
          <label class="form-label" data-i18n="startsAt">وقت البدء</label>
          <input id="l-start" class="form-control" data-datetime required>
        </div>
        <div class="col-md-2">
          <label class="form-label" data-i18n="mode">طريقة التنفيذ</label>
          <select id="l-mode" class="form-select">
            <option value="0">${ZU.escapeHtml(ZU.t('inPerson'))}</option>
            <option value="1">${ZU.escapeHtml(ZU.t('online'))}</option>
            <option value="2">${ZU.escapeHtml(ZU.t('hybrid'))}</option>
          </select>
        </div>
        <div class="col-md-3">
          <label class="form-label" data-i18n="onlineLink">رابط الاجتماع</label>
          <input id="l-link" class="form-control" dir="ltr" placeholder="https://teams.microsoft.com/...">
        </div>
        <div class="col-12"><button class="btn btn-primary" type="submit" data-i18n="addLecture">إضافة محاضرة</button></div>
      </form>
      ${lectures.length ? lectures.map(lecture => `
        <div class="lecture-row" data-lecture="${lecture.id}">
          <div class="d-flex justify-content-between align-items-start gap-2 flex-wrap">
            <div>
              <strong>${lecture.sequenceNumber}. ${ZU.escapeHtml(lecture.titleAr)}</strong>
              <div class="small-muted">${ZU.escapeHtml(ZU.fmtDateTime(lecture.startsAtUtc))} · ${ZU.escapeHtml(modeName(lecture.mode))}</div>
            </div>
            <div class="d-flex gap-2 flex-wrap">
              <button class="btn btn-sm btn-outline-primary code-btn" data-id="${lecture.id}" data-i18n="activateCode">كود الحضور</button>
              <button class="btn btn-sm btn-outline-primary material-btn" data-id="${lecture.id}" data-i18n="materials">المرفقات</button>
            </div>
          </div>
          <div class="code-host mt-2"></div>
          <div class="material-host mt-2"></div>
        </div>`).join('') : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`}`);
    ZU.initDatePickers(host[0]);
  }

  $('#tab-lectures').on('submit', '#lecture-form', function (event) {
    event.preventDefault();
    const payload = {
      nameAr: $('#l-title').val().trim(),
      nameEn: null,
      startsAtUtc: ZU.toIso($('#l-start').val()),
      mode: Number($('#l-mode').val()),
      onlineLink: $('#l-link').val().trim() || null
    };
    if (!payload.nameAr || !payload.startsAtUtc) { ZU.toast('أكمل بيانات المحاضرة.', false); return; }
    ZU.api(`/api/courses/${detail.course.id}/lectures`, 'POST', payload)
      .done(function () { ZU.toast(ZU.t('saved'), true); openManage(detail.course.id); })
      .fail(function () { ZU.toast(ZU.t('failed'), false); });
  });

  $('#tab-lectures').on('click', '.code-btn', function () {
    const id = $(this).data('id');
    const host = $(this).closest('.lecture-row').find('.code-host');
    ZU.api(`/api/courses/lectures/${id}/activate-code`, 'POST')
      .done(function (result) {
        host.html(`<div class="code-pill">${ZU.escapeHtml(result.attendanceCode)}
          <span class="small-muted">${ZU.escapeHtml(ZU.t('expiresAt'))}: ${ZU.escapeHtml(ZU.fmtDateTime(result.expiresAtUtc))}</span></div>`);
        ZU.toast(ZU.t('codeActivated'), true);
      })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#tab-lectures').on('click', '.material-btn', function () {
    const id = $(this).data('id');
    const host = $(this).closest('.lecture-row').find('.material-host');
    ZU.api(`/api/courses/lectures/${id}/materials`).done(function (rows) {
      host.html(`
        ${rows.length ? rows.map(row => `<a class="btn btn-sm btn-outline-secondary me-2 mb-2" target="_blank" rel="noopener"
            href="/api/courses/materials/${row.id}/file">${ZU.escapeHtml(row.originalName)}</a>`).join('') : ''}
        <form class="upload-material d-flex gap-2 mt-1" enctype="multipart/form-data">
          <input type="file" class="form-control form-control-sm" style="max-width:280px" required>
          <button class="btn btn-sm btn-primary" type="submit" data-i18n="upload">رفع</button>
        </form>`);
      ZU.applyLanguage();
    });
  });

  $('#tab-lectures').on('submit', '.upload-material', function (event) {
    event.preventDefault();
    const file = $(this).find('input[type=file]')[0].files[0];
    if (!file) return;
    const lectureId = $(this).closest('.lecture-row').data('lecture');
    const data = new FormData();
    data.append('file', file);
    ZU.apiForm(`/api/courses/lectures/${lectureId}/materials`, 'POST', data)
      .done(function () { ZU.toast(ZU.t('saved'), true); openManage(detail.course.id); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------------ attendance */
  function renderAttendance() {
    const host = $('#tab-attendance');
    const lectures = detail.lectures || [];
    const participants = detail.participants || [];
    if (!lectures.length || !participants.length) {
      host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
      return;
    }
    const marks = {};
    (detail.attendance || []).forEach(row => { marks[row.lectureId + '|' + row.enrollmentId] = row.isPresent; });

    host.html(`
      <div class="table-responsive">
        <table class="table table-sm align-middle">
          <thead><tr>
            <th>${ZU.escapeHtml(ZU.t('member'))}</th>
            ${lectures.map(lecture => `<th class="text-center" title="${ZU.escapeHtml(ZU.fmtDateTime(lecture.startsAtUtc))}">${lecture.sequenceNumber}</th>`).join('')}
            <th class="text-center">%</th>
          </tr></thead>
          <tbody>
            ${participants.map(participant => `
              <tr data-enrollment="${participant.enrollmentId}">
                <td>${ZU.escapeHtml(ZU.pick(participant.nameAr, participant.nameEn))}<div class="small-muted">${ZU.escapeHtml(participant.userId.slice(0, 8))}</div></td>
                ${lectures.map(lecture => {
                  const present = marks[lecture.id + '|' + participant.enrollmentId];
                  return `<td class="text-center">
                    <input type="checkbox" class="form-check-input attendance-mark" data-lecture="${lecture.id}"
                      ${present ? 'checked' : ''}>
                  </td>`;
                }).join('')}
                <td class="text-center">${lectures.length ? Math.round((participant.attendedLectures / lectures.length) * 100) : 0}%</td>
              </tr>`).join('')}
          </tbody>
        </table>
      </div>
      <div class="small-muted">${ZU.escapeHtml(ZU.t('attendanceHint'))}</div>`);
  }

  $('#tab-attendance').on('change', '.attendance-mark', function () {
    const lectureId = $(this).data('lecture');
    const enrollmentId = $(this).closest('tr').data('enrollment');
    const isPresent = $(this).is(':checked');
    ZU.api(`/api/courses/lectures/${lectureId}/attendance/${enrollmentId}`, 'PUT', { isPresent: isPresent })
      .done(function () { ZU.toast(ZU.t('saved'), true); })
      .fail(function () {
        ZU.toast(ZU.t('failed'), false);
        $(this).prop('checked', !isPresent);
      });
  });

  /* ------------------------------------------------------------------ exam */
  function renderExam() {
    const host = $('#tab-exam');
    const participants = detail.participants || [];
    const examName = detail.examNameAr;
    host.html(`
      <div class="d-flex gap-2 flex-wrap align-items-end mb-3">
        <div style="min-width:320px">
          <label class="form-label" data-i18n="examName">اسم الامتحان</label>
          <input id="exam-name" class="form-control" value="${ZU.escapeHtml(examName || '')}" ${examName ? 'readonly' : ''}>
        </div>
        ${examName ? '' : `<button class="btn btn-primary" id="create-exam" type="button" data-i18n="createExam">إنشاء امتحان</button>`}
        <button class="btn btn-outline-primary" id="toggle-exam-required" type="button">
          ${detail.course.hasExam ? ZU.escapeHtml(ZU.t('examRequired')) : ZU.escapeHtml(ZU.t('examNotRequired'))}
        </button>
      </div>
      ${participants.length ? `
      <div class="table-responsive">
        <table class="table table-sm align-middle">
          <thead><tr>
            <th>${ZU.escapeHtml(ZU.t('member'))}</th>
            <th>${ZU.escapeHtml(ZU.t('attendance'))}</th>
            <th>${ZU.escapeHtml(ZU.t('exam'))}</th>
            <th style="width:220px">${ZU.escapeHtml(ZU.t('outcome'))}</th>
          </tr></thead>
          <tbody>
            ${participants.map(participant => `
              <tr data-enrollment="${participant.enrollmentId}">
                <td>${ZU.escapeHtml(ZU.pick(participant.nameAr, participant.nameEn))}</td>
                <td>${participant.attendedLectures}/${participant.totalLectures}</td>
                <td>${ZU.escapeHtml(participant.examOutcome === 1 ? ZU.t('passed') : participant.examOutcome === 2 ? ZU.t('failedExam') : ZU.t('notRecorded'))}</td>
                <td>
                  <select class="form-select form-select-sm exam-outcome" ${examName ? '' : 'disabled'}>
                    <option value="0" ${participant.examOutcome === 0 ? 'selected' : ''}>${ZU.escapeHtml(ZU.t('notRecorded'))}</option>
                    <option value="1" ${participant.examOutcome === 1 ? 'selected' : ''}>${ZU.escapeHtml(ZU.t('passed'))}</option>
                    <option value="2" ${participant.examOutcome === 2 ? 'selected' : ''}>${ZU.escapeHtml(ZU.t('failedExam'))}</option>
                  </select>
                </td>
              </tr>`).join('')}
          </tbody>
        </table>
      </div>` : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`}`);
  }

  $('#tab-exam').on('click', '#create-exam', function () {
    const name = $('#exam-name').val().trim();
    if (!name) { ZU.toast('أدخل اسم الامتحان.', false); return; }
    ZU.api(`/api/courses/${detail.course.id}/exam`, 'POST', { nameAr: name, nameEn: null })
      .done(function () { ZU.toast(ZU.t('saved'), true); openManage(detail.course.id); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#tab-exam').on('click', '#toggle-exam-required', function () {
    ZU.api(`/api/courses/${detail.course.id}/exam-requirement`, 'PUT', { required: !detail.course.hasExam })
      .done(function () { ZU.toast(ZU.t('saved'), true); openManage(detail.course.id); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#tab-exam').on('change', '.exam-outcome', function () {
    const enrollmentId = $(this).closest('tr').data('enrollment');
    ZU.api(`/api/courses/${detail.course.id}/exam/results/${enrollmentId}`, 'PUT', { outcome: Number($(this).val()) })
      .done(function () { ZU.toast(ZU.t('saved'), true); openManage(detail.course.id); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------------ impact */
  function loadImpactRoster() {
    ZU.api(`/api/impact/courses/${detail.course.id}/roster`).done(function (rows) {
      const host = $('#tab-impact');
      if (!rows || !rows.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
      host.html(`<div class="table-responsive"><table class="table table-sm align-middle">
        <thead><tr>
          <th>${ZU.escapeHtml(ZU.t('member'))}</th>
          <th>${ZU.escapeHtml(ZU.t('file'))}</th>
          <th>${ZU.escapeHtml(ZU.t('status'))}</th>
          <th>${ZU.escapeHtml(ZU.t('actions'))}</th>
        </tr></thead>
        <tbody>
          ${rows.map(row => `
            <tr>
              <td>${ZU.escapeHtml(row.recipientNameAr || row.memberNameAr || row.nameAr || '—')}</td>
              <td>${row.submissionId ? `<a target="_blank" rel="noopener" href="/api/impact/${row.submissionId}/file">${ZU.escapeHtml(row.originalName || ZU.t('file'))}</a>` : '—'}</td>
              <td>${ZU.escapeHtml(row.status === 2 ? ZU.t('impactApproved') : row.status === 1 ? ZU.t('impactPending') : row.status === 3 ? ZU.t('impactReturned') : ZU.t('notRecorded'))}</td>
              <td class="d-flex gap-2">
                ${row.submissionId ? `<button class="btn btn-sm btn-outline-success impact-review" data-id="${row.submissionId}" data-approve="true" data-i18n="approve">اعتماد</button>
                <button class="btn btn-sm btn-outline-danger impact-review" data-id="${row.submissionId}" data-approve="false" data-i18n="return">إعادة</button>` : ''}
              </td>
            </tr>`).join('')}
        </tbody></table></div>`);
      ZU.applyLanguage();
    }).fail(function () { $('#tab-impact').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#tab-impact').on('click', '.impact-review', function () {
    const id = $(this).data('id');
    const approve = $(this).data('approve') === true || $(this).data('approve') === 'true';
    const note = approve ? null : (prompt('سبب الإعادة (إلزامي):') || '');
    if (!approve && !note) return;
    ZU.api(`/api/impact/${id}/review`, 'PUT', { approve: approve, noteAr: note })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadImpactRoster(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------------ certificates */
  function loadCertificates() {
    ZU.api(`/api/certificates/courses/${detail.course.id}`).done(function (rows) {
      const host = $('#tab-certificates');
      host.html(`
        <div class="d-flex justify-content-between align-items-center mb-3 flex-wrap gap-2">
          <div class="small-muted">${ZU.escapeHtml(ZU.t('certificateGate'))}</div>
          <button class="btn btn-primary" id="issue-certificates" data-i18n="issueCertificates">إصدار شهادات المستحقين</button>
        </div>
        ${rows && rows.length ? `<div class="table-responsive"><table class="table table-sm">
          <thead><tr><th>${ZU.escapeHtml(ZU.t('courses'))}</th><th>${ZU.escapeHtml(ZU.t('referenceNumber'))}</th>
            <th>${ZU.escapeHtml(ZU.t('status'))}</th><th>${ZU.escapeHtml(ZU.t('actions'))}</th></tr></thead>
          <tbody>${rows.map(row => `
            <tr>
              <td>${ZU.escapeHtml(ZU.pick(row.courseTitleAr || '', row.courseTitleEn || '') || '—')}
                <div class="small-muted">${ZU.escapeHtml(ZU.fmtDate(row.issuedAtUtc))}</div></td>
              <td>${ZU.escapeHtml(row.referenceNumber || '—')}</td>
              <td>${row.status === 0 ? `<span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('certificateValid'))}</span>`
                : row.status === 1 ? `<span class="badge-soft badge-danger">${ZU.escapeHtml(ZU.t('certificateRevoked'))}</span>`
                : `<span class="badge-soft badge-muted">${ZU.escapeHtml(ZU.t('status'))}</span>`}</td>
              <td class="d-flex gap-2">
                ${row.id ? `<a class="btn btn-sm btn-outline-primary" target="_blank" href="/api/certificates/${row.id}/file">PDF</a>` : ''}
                ${row.status === 0 ? `<button class="btn btn-sm btn-outline-danger revoke-cert" data-id="${row.id}" data-i18n="revoke">إلغاء</button>` : ''}
              </td>
            </tr>`).join('')}</tbody></table></div>`
          : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`}`);
      ZU.applyLanguage();
    }).fail(function () { $('#tab-certificates').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#tab-certificates').on('click', '#issue-certificates', function () {
    ZU.api(`/api/certificates/courses/${detail.course.id}/issue`, 'POST')
      .done(function (result) { ZU.toast(`${ZU.t('issued')}: ${result.issued}`, true); loadCertificates(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#tab-certificates').on('click', '.revoke-cert', function () {
    const reason = prompt('سبب إلغاء الشهادة:');
    if (!reason) return;
    ZU.api(`/api/certificates/${$(this).data('id')}/revoke`, 'POST', { reason: reason })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadCertificates(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------------ actions */
  function renderActions() {
    const host = $('#tab-actions');
    const course = detail.course;
    host.html(`
      <div class="row g-3">
        <div class="col-md-6">
          <div class="border rounded-3 p-3 h-100">
            <h4 class="h6">${ZU.escapeHtml(ZU.t('evaluationDeadline'))}</h4>
            <div class="d-flex gap-2 mt-2">
              <input id="new-deadline" class="form-control" data-datetime value="${ZU.escapeHtml(ZU.toLocalInput(detail.evaluationDeadlineUtc))}">
              <button class="btn btn-outline-primary" id="extend-deadline" type="button" data-i18n="extend">تمديد</button>
            </div>
          </div>
        </div>
        <div class="col-md-6">
          <div class="border rounded-3 p-3 h-100">
            <h4 class="h6">${ZU.escapeHtml(ZU.t('addParticipant'))}</h4>
            <div class="d-flex gap-2 mt-2">
              <input id="participant-search" class="form-control" data-i18n-placeholder="search" placeholder="بحث بالاسم أو الرقم">
              <button class="btn btn-outline-primary" id="search-participant" type="button" data-i18n="search">بحث</button>
            </div>
            <div id="participant-results" class="mt-2"></div>
          </div>
        </div>
        <div class="col-md-6">
          <div class="border rounded-3 p-3 h-100">
            <h4 class="h6">${ZU.escapeHtml(ZU.t('endCourse'))}</h4>
            <p class="small-muted">إغلاق الدورة يفتح التقييم ويمكّن من إصدار الشهادات.</p>
            <button class="btn btn-outline-primary" id="end-course" type="button" ${course.status === 6 ? 'disabled' : ''} data-i18n="endCourse">إنهاء الدورة</button>
          </div>
        </div>
        <div class="col-md-6">
          <div class="border rounded-3 p-3 h-100">
            <h4 class="h6">${ZU.escapeHtml(ZU.t('cancelCourse'))}</h4>
            <p class="small-muted">يُرسل طلب إلغاء إلى الجودة لاعتماده وإصدار الكتاب الرسمي.</p>
            <textarea id="cancel-reason" class="form-control mb-2" rows="2" maxlength="1000" placeholder="سبب الإلغاء"></textarea>
            <input id="cancel-file" type="file" class="form-control mb-2" accept=".pdf,.docx">
            <button class="btn btn-outline-danger" id="request-cancel" type="button" data-i18n="cancelCourse">طلب إلغاء</button>
          </div>
        </div>
      </div>`);
    ZU.initDatePickers(host[0]);
  }

  $('#tab-actions').on('click', '#extend-deadline', function () {
    const deadline = ZU.toIso($('#new-deadline').val());
    if (!deadline) { ZU.toast(ZU.t('datesRequired'), false); return; }
    ZU.api(`/api/courses/${detail.course.id}/evaluation-deadline`, 'PUT', { deadlineUtc: deadline })
      .done(function () { ZU.toast(ZU.t('saved'), true); openManage(detail.course.id); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#tab-actions').on('click', '#search-participant', function () {
    const query = $('#participant-search').val().trim();
    if (!query) return;
    ZU.api('/api/courses/directory-users?query=' + encodeURIComponent(query))
      .done(function (rows) {
        $('#participant-results').html((rows || []).slice(0, 8).map(user => `
          <button type="button" class="btn btn-sm btn-outline-secondary me-1 mb-1 add-participant" data-id="${user.id}">
            ${ZU.escapeHtml(ZU.pick(user.nameAr, user.nameEn))} · ${ZU.escapeHtml(user.loginName)}
          </button>`).join('') || `<div class="small-muted">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
      });
  });

  $('#tab-actions').on('click', '.add-participant', function () {
    ZU.api(`/api/courses/${detail.course.id}/participants`, 'POST', { userId: $(this).data('id') })
      .done(function () { ZU.toast(ZU.t('saved'), true); openManage(detail.course.id); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#tab-actions').on('click', '#end-course', function () {
    if (!confirm('تأكيد إنهاء الدورة؟')) return;
    ZU.api(`/api/courses/${detail.course.id}/end`, 'POST')
      .done(function () { ZU.toast(ZU.t('saved'), true); openManage(detail.course.id); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#tab-actions').on('click', '#request-cancel', function () {
    const reason = $('#cancel-reason').val().trim();
    const file = $('#cancel-file')[0].files[0];
    if (!reason) { ZU.toast('اذكر سبب الإلغاء.', false); return; }
    if (!file) { ZU.toast('أرفق الكتاب الرسمي (PDF أو DOCX).', false); return; }
    const data = new FormData();
    data.append('reasonAr', reason);
    data.append('file', file);
    ZU.apiForm(`/api/course-cancellations/${detail.course.id}`, 'POST', data)
      .done(function () { ZU.toast(ZU.t('saved'), true); reloadCourses(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });
})();
