/* لوحة عضو هيئة التدريس: الدورات، الحضور بالكود، التقييم، الأثر، الشهادة. */
(function () {
  'use strict';

  const gateLabels = {
    CourseNotEnded: 'gateCourseNotEnded', NoLectures: 'gateNoLectures', InsufficientAttendance: 'gateAttendance',
    ExamNotPassed: 'gateExam', EvaluationMissing: 'gateEvaluation', ImpactNotApproved: 'gateImpact'
  };
  const statusLabels = {
    0: 'draft', 1: 'submitted', 2: 'revisionRequested', 3: 'rejected', 4: 'approved', 5: 'inProgress', 6: 'ended', 7: 'cancelled'
  };

  let courses = [];
  let current = null;

  ZU.boot({ page: 'member', onReady: function () { loadCourses(); } });

  function statusBadge(status) {
    const map = { 4: 'badge-navy', 5: 'badge-warn', 6: 'badge-ok', 7: 'badge-danger', 1: 'badge-muted', 2: 'badge-warn', 3: 'badge-danger' };
    return `<span class="badge-soft ${map[status] || 'badge-muted'}">${ZU.escapeHtml(ZU.t(statusLabels[status]))}</span>`;
  }

  function loadCourses() {
    ZU.api('/api/courses').done(function (rows) {
      courses = rows || [];
      render();
    });
  }

  function render() {
    const term = ($('#course-filter').val() || '').toString().trim();
    const host = $('#course-list');
    const rows = courses.filter(course => !term ||
      (course.titleAr || '').includes(term) || (course.titleEn || '').toLowerCase().includes(term.toLowerCase()));
    if (!rows.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
    host.html(rows.map(course => `
      <article class="entity-card">
        <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
          <h4>${ZU.escapeHtml(ZU.pick(course.titleAr, course.titleEn))}</h4>
          ${statusBadge(course.status)}
        </div>
        <div class="meta">
          ${ZU.escapeHtml(ZU.t('instructor'))}: ${ZU.escapeHtml(course.actualInstructorNameAr || '—')}<br>
          ${course.hasExam ? ZU.escapeHtml(ZU.t('examRequired')) : ZU.escapeHtml(ZU.t('examNotRequired'))}
          ${course.enrolled ? ` · <span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('enrolled'))}</span>` : ''}
        </div>
        <div class="d-flex gap-2 mt-3">
          <button class="btn btn-sm btn-primary open-course" data-id="${course.id}" data-i18n="view">عرض</button>
          ${course.canSelfEnroll ? `<button class="btn btn-sm btn-outline-primary enroll-course" data-id="${course.id}" data-i18n="enroll">تسجيل</button>` : ''}
        </div>
      </article>`).join(''));
    ZU.applyLanguage();
  }

  $('#course-filter').on('input', render);

  $('#course-list').on('click', '.enroll-course', function () {
    const id = $(this).data('id');
    ZU.api(`/api/courses/${id}/enroll`, 'POST').done(function () {
      ZU.toast(ZU.t('saved'), true); loadCourses(); if (current && current.course.id === id) openCourse(id);
    });
  });

  $('#course-list').on('click', '.open-course', function () { openCourse($(this).data('id')); });
  $('#close-detail').on('click', function () { $('#course-detail').addClass('d-none'); current = null; });

  function openCourse(id) {
    ZU.api(`/api/courses/${id}`).done(function (detail) {
      current = detail;
      $('#course-detail').removeClass('d-none');
      $('#detail-title').text(ZU.pick(detail.course.titleAr, detail.course.titleEn));
      $('#detail-meta').html([
        `${ZU.escapeHtml(ZU.t('status'))}: ${ZU.escapeHtml(ZU.t(statusLabels[detail.course.status]))}`,
        `${ZU.escapeHtml(ZU.t('instructor'))}: ${ZU.escapeHtml(detail.course.actualInstructorNameAr || '—')}`,
        `${ZU.escapeHtml(ZU.t('plannedHours'))}: ${detail.plannedHours}`,
        `${ZU.escapeHtml(ZU.t('startAt'))}: ${ZU.escapeHtml(ZU.fmtDateTime(detail.plannedStartUtc))}`,
        `${ZU.escapeHtml(ZU.t('endAt'))}: ${ZU.escapeHtml(ZU.fmtDateTime(detail.plannedEndUtc))}`
      ].join(' · '));
      renderLectures(detail);
      loadGate(id);
      loadEvaluation(id);
      loadImpact(id);
      renderExam(detail);
      $('html, body').animate({ scrollTop: $('#course-detail').offset().top - 90 }, 240);
    });
  }

  function renderLectures(detail) {
    const host = $('#lectures-box');
    const lectures = detail.lectures || [];
    const total = lectures.length;
    const attended = lectures.filter(x => x.isPresent === true).length;
    const required = Math.ceil(total * 0.75);
    const percent = total ? Math.round(attended / total * 100) : 0;
    if (!total) {
      host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('gateNoLectures'))}</div>`);
      return;
    }
    host.html(`
      <div class="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-2">
        <h3 class="h6 mb-0">${ZU.escapeHtml(ZU.t('lectures'))} — ${ZU.escapeHtml(ZU.pick(detail.course.titleAr, detail.course.titleEn))}</h3>
        <span class="small-muted">${ZU.escapeHtml(ZU.t('attended'))}: ${attended}/${total} (${percent}%) — ${ZU.escapeHtml(ZU.t('gateAttendance'))}: ≥75%</span>
      </div>
      <div class="progress-thin ${percent >= 75 ? 'ok' : percent >= 50 ? 'warn' : ''} mb-3"><span style="width:${percent}%"></span></div>
      ${lectures.map(lecture => `
        <div class="lecture-row">
          <div class="d-flex justify-content-between align-items-start gap-2 flex-wrap">
            <div>
              <strong>${ZU.escapeHtml(lecture.titleAr)}</strong>
              <div class="small-muted">
                ${ZU.escapeHtml(ZU.fmtDateTime(lecture.startsAtUtc))} ·
                ${ZU.escapeHtml(lecture.mode === 1 ? ZU.t('online') : lecture.mode === 2 ? ZU.t('hybrid') : ZU.t('inPerson'))}
                ${lecture.codeIsActive ? ` · <span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('codeActive'))} ${ZU.escapeHtml(ZU.fmtDateTime(lecture.codeExpiresAtUtc))}</span>` : ''}
                ${lecture.isPresent === true ? ' · <span class="badge-soft badge-ok">✓</span>' : ''}
              </div>
            </div>
            <div class="d-flex gap-2">
              ${lecture.onlineLink ? `<a class="btn btn-sm btn-outline-primary" target="_blank" rel="noopener" href="${ZU.escapeHtml(lecture.onlineLink)}">Teams</a>` : ''}
              <button class="btn btn-sm btn-outline-primary materials-btn" data-lecture="${lecture.id}" data-i18n="materials">المرفقات</button>
            </div>
          </div>
          <form class="d-flex gap-2 mt-2 attendance-form" data-lecture="${lecture.id}">
            <input class="form-control form-control-sm" style="max-width:140px" inputmode="numeric" maxlength="4"
                   placeholder="${ZU.escapeHtml(ZU.t('attendanceCode'))}" ${lecture.codeIsActive ? '' : 'disabled'}>
            <button class="btn btn-sm btn-primary" type="submit" ${lecture.codeIsActive ? '' : 'disabled'}
                    data-i18n="enterCode">تأكيد الحضور</button>
          </form>
          <div class="materials-host mt-2"></div>
        </div>`).join('')}`);
    ZU.applyLanguage();
  }

  $('#lectures-box').on('submit', '.attendance-form', function (event) {
    event.preventDefault();
    const lectureId = $(this).data('lecture');
    const code = $(this).find('input').val().trim();
    if (!code) return;
    ZU.api(`/api/courses/lectures/${lectureId}/attendance-code`, 'POST', { code: code })
      .done(function () {
        ZU.toast(ZU.t('saved'), true);
        if (current) openCourse(current.course.id);
      })
      .fail(function (xhr) {
        ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || 'الكود غير صحيح أو منتهي.', false);
      });
  });

  $('#lectures-box').on('click', '.materials-btn', function () {
    const lectureId = $(this).data('lecture');
    const host = $(this).closest('.lecture-row').find('.materials-host');
    if (host.data('loaded')) { host.empty().removeData('loaded'); return; }
    ZU.api(`/api/courses/lectures/${lectureId}/materials`).done(function (rows) {
      host.data('loaded', true).html(rows.length
        ? rows.map(row => `<a class="btn btn-sm btn-outline-secondary me-2 mb-1" target="_blank" rel="noopener"
             href="${ZU.state.apiBase}/api/courses/materials/${row.id}/file">${ZU.escapeHtml(row.originalName)}</a>`).join('')
        : `<div class="small-muted">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
    });
  });

  function loadGate(courseId) {
    ZU.api(`/api/courses/${courseId}/certificate-eligibility`).done(function (gate) {
      const reasons = (gate.reasons || []).map(reason =>
        `<li>${ZU.escapeHtml(ZU.t(gateLabels[reason] || reason))}</li>`).join('');
      $('#gate-box').html(`
        <div class="panel mb-0" style="border-color:${gate.isEligible ? '#bfe8d8' : '#f2ddb4'}">
          <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
            <strong>${ZU.escapeHtml(ZU.t('certificateGate'))}</strong>
            <span class="badge-soft ${gate.isEligible ? 'badge-ok' : 'badge-warn'}">
              ${ZU.escapeHtml(gate.isEligible ? ZU.t('eligible') : ZU.t('notEligible'))}
            </span>
          </div>
          <div class="small-muted mt-2">
            ${ZU.escapeHtml(ZU.t('attended'))}: ${gate.attendedLectures}/${gate.totalLectures} ·
            ${ZU.escapeHtml(ZU.t('exam'))}: ${ZU.escapeHtml(gate.examOutcome === 1 ? ZU.t('passed') : gate.examOutcome === 2 ? ZU.t('failedExam') : ZU.t('notRecorded'))} ·
            ${ZU.escapeHtml(ZU.t('evaluation'))}: ${gate.evaluationCompleted ? '✓' : '—'} ·
            ${ZU.escapeHtml(ZU.t('impact'))}: ${gate.impactApproved ? '✓' : '—'}
          </div>
          ${reasons ? `<ul class="mt-2 mb-0 small-muted">${reasons}</ul>` : ''}
          ${gate.existingCertificateStatus === 0
            ? `<a class="btn btn-sm btn-primary mt-3" href="/member-profile.html#certificates" data-i18n="downloadCertificate">تنزيل الشهادة</a>` : ''}
        </div>`);
      ZU.applyLanguage();
    }).fail(function () { $('#gate-box').empty(); });
  }

  function loadEvaluation(courseId) {
    const host = $('#evaluation-box');
    ZU.api(`/api/evaluation/courses/${courseId}/form`).done(function (form) {
      if (form.alreadySubmitted) {
        host.html(`<div class="alert alert-success mb-0">${ZU.escapeHtml(ZU.t('evaluation'))} ✓</div>`);
        return;
      }
      host.html(`
        <h3 class="h6">${ZU.escapeHtml(ZU.t('evaluation'))}</h3>
        <form id="evaluation-form">
          ${(form.questions || []).map(question => `
            <div class="mb-3">
              <div class="form-label">${ZU.escapeHtml(ZU.pick(question.textAr, question.textEn))}</div>
              ${question.options.map(option => `
                <div class="form-check">
                  <input class="form-check-input" type="radio" name="q_${question.id}" id="o_${option.id}"
                         value="${option.id}">
                  <label class="form-check-label" for="o_${option.id}">${ZU.escapeHtml(ZU.pick(option.textAr, option.textEn))}</label>
                </div>`).join('')}
            </div>`).join('')}
          <button class="btn btn-primary" type="submit" data-i18n="submitEvaluation">إرسال التقييم</button>
        </form>`);
      ZU.applyLanguage();
    }).fail(function (xhr) {
      const message = (xhr.responseJSON && xhr.responseJSON.message) || '';
      host.html(message.includes('closed') || message.includes('period')
        ? `<div class="alert alert-warning mb-0">${ZU.escapeHtml(ZU.t('evaluationClosed'))}</div>` : '');
    });
  }

  $('#evaluation-box').on('submit', '#evaluation-form', function (event) {
    event.preventDefault();
    const answers = [];
    let missing = false;
    $(this).find('.form-label').each(function () { });
    $(this).find('input[type=radio]:checked').each(function () {
      answers.push({ questionId: $(this).attr('name').slice(2), optionId: $(this).val() });
    });
    const expected = $(this).find('input[type=radio]').map(function () { return $(this).attr('name'); }).get()
      .filter((value, index, self) => self.indexOf(value) === index).length;
    if (answers.length !== expected) missing = true;
    if (missing) { ZU.toast('أجب عن جميع الأسئلة.', false); return; }
    ZU.api(`/api/evaluation/courses/${current.course.id}/responses`, 'POST', { answers: answers })
      .done(function () { ZU.toast(ZU.t('saved'), true); openCourse(current.course.id); })
      .fail(function () { ZU.toast(ZU.t('failed'), false); });
  });

  function loadImpact(courseId) {
    const host = $('#impact-box');
    ZU.api(`/api/impact/courses/${courseId}/mine`).done(function (rows) {
      const list = (rows || []).map(row => `
        <div class="d-flex justify-content-between align-items-center gap-2 border rounded-3 p-2 mb-2">
          <span>${ZU.escapeHtml(row.originalName)} <span class="small-muted">v${row.version}</span></span>
          <span class="badge-soft ${row.status === 2 ? 'badge-ok' : row.status === 1 ? 'badge-warn' : 'badge-muted'}">
            ${ZU.escapeHtml(row.status === 2 ? ZU.t('impactApproved') : row.status === 1 ? ZU.t('impactReturned') : ZU.t('impactPending'))}
          </span>
        </div>
        ${row.reviewNoteAr ? `<div class="note-red mb-2">${ZU.escapeHtml(row.reviewNoteAr)}</div>` : ''}`).join('');
      host.html(`
        <h3 class="h6">${ZU.escapeHtml(ZU.t('impact'))}</h3>
        ${list || ''}
        <form id="impact-form" class="d-flex gap-2 flex-wrap align-items-center">
          <input type="file" class="form-control" style="max-width:320px" accept=".pdf,.docx" required>
          <button class="btn btn-outline-primary" type="submit" data-i18n="uploadImpact">رفع ملف الأثر</button>
        </form>`);
      ZU.applyLanguage();
    }).fail(function () { host.empty(); });
  }

  $('#impact-box').on('submit', '#impact-form', function (event) {
    event.preventDefault();
    const file = $(this).find('input[type=file]')[0].files[0];
    if (!file) return;
    const data = new FormData();
    data.append('file', file);
    ZU.apiForm(`/api/impact/courses/${current.course.id}`, 'POST', data)
      .done(function () { ZU.toast(ZU.t('saved'), true); openCourse(current.course.id); })
      .fail(function () { ZU.toast(ZU.t('failed'), false); });
  });

  function renderExam(detail) {
    const host = $('#exam-box');
    if (!detail.course.hasExam) { host.empty(); return; }
    host.html(`
      <h3 class="h6">${ZU.escapeHtml(ZU.t('exam'))}</h3>
      <div class="small-muted">
        ${ZU.escapeHtml(detail.examNameAr || ZU.t('notRecorded'))} ·
        ${ZU.escapeHtml(ZU.t('status'))}:
        <strong>${ZU.escapeHtml(detail.myExamOutcome === 1 ? ZU.t('passed') : detail.myExamOutcome === 2 ? ZU.t('failedExam') : ZU.t('notRecorded'))}</strong>
      </div>`);
  }
})();
