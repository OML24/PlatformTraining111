/* كونسول دائرة الجودة: المقترحات، الاحتياجات، الإلغاءات، التقييم، التوثيق، المدربون الخارجيون. */
(function () {
  'use strict';

  const S = ZU.state;
  let faculties = [];
  const loaded = {};

  ZU.boot({
    page: 'quality',
    onReady: function () {
      if (!ZU.requirePermission('page.quality')) return;
      ZU.api('/api/courses/faculties').done(rows => {
        faculties = rows || [];
        $('#n-faculty').html('<option value="">— الكلية —</option>' + faculties.map(f =>
          `<option value="${f.id}">${ZU.escapeHtml(ZU.pick(f.nameAr, f.nameEn))}</option>`).join(''));
      });
      loadProposals();
    }
  });

  $('#quality-tabs').on('click', 'button[data-tab]', function () {
    const tab = $(this).data('tab');
    $('#quality-tabs button').removeClass('active');
    $(this).addClass('active');
    $('main.app-main > section.panel').addClass('d-none');
    $('#tab-' + tab).removeClass('d-none');
    if (loaded[tab]) return;
    loaded[tab] = true;
    if (tab === 'needs') loadNeeds();
    if (tab === 'cancellations') loadCancellations();
    if (tab === 'evaluation') loadEvaluation();
    if (tab === 'historical') renderHistorical();
    if (tab === 'external') loadExternal();
    if (tab === 'vault') loadVault();
  });

  // QualityRequestStatus: 0 Submitted, 1 RevisionRequested, 2 Approved, 3 Rejected
  function statusBadge(status) {
    const map = { 0: 'badge-warn', 1: 'badge-navy', 2: 'badge-ok', 3: 'badge-danger' };
    const names = { 0: 'pending', 1: 'revisionRequested', 2: 'approved', 3: 'rejected' };
    return `<span class="badge-soft ${map[status] || 'badge-muted'}">${ZU.escapeHtml(ZU.t(names[status] || 'status'))}</span>`;
  }

  /* ---------------------------------------------------------------- proposals */
  let proposalIndex = {};
  function loadProposals() {
    ZU.api('/api/courses').done(function (rows) {
      proposalIndex = {};
      (rows || []).forEach(course => { proposalIndex[course.id] = course; });
      const pending = (rows || []).filter(c => c.status === 1 || c.status === 2 || c.status === 4);
      const host = $('#tab-proposals');
      if (!pending.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
      host.html(`<div class="panel-heading"><div><h2>${ZU.escapeHtml(ZU.t('courseProposals'))}</h2>
        <p>${ZU.escapeHtml(ZU.t('reviewHint'))}</p></div></div>
        <div class="table-responsive"><table class="table align-middle">
          <thead><tr>
            <th>${ZU.escapeHtml(ZU.t('title'))}</th>
            <th>${ZU.escapeHtml(ZU.t('instructor'))}</th>
            <th>${ZU.escapeHtml(ZU.t('status'))}</th>
            <th>${ZU.escapeHtml(ZU.t('actions'))}</th>
          </tr></thead>
          <tbody>
            ${pending.map(course => `<tr>
              <td><strong>${ZU.escapeHtml(ZU.pick(course.titleAr, course.titleEn))}</strong>
                <div class="small-muted">${ZU.escapeHtml(course.actualInstructorNameAr || '—')}</div></td>
              <td>${ZU.escapeHtml(ZU.t(course.hasExam ? 'examRequired' : 'examNotRequired'))}</td>
              <td>${statusBadge(course.status)}</td>
              <td class="d-flex gap-2 flex-wrap">
                <button class="btn btn-sm btn-outline-danger proposal-review" data-id="${course.id}" data-decision="3" data-i18n="reject">رفض</button>
                <button class="btn btn-sm btn-outline-warning proposal-review" data-id="${course.id}" data-decision="2" data-i18n="return">إعادة للمحاضر</button>
                <button class="btn btn-sm btn-primary approve-course" data-id="${course.id}" data-i18n="approve">اعتماد</button>
              </td>
            </tr>`).join('')}
          </tbody></table></div>`);
      ZU.applyLanguage();
    }).fail(function () {
      $('#tab-proposals').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
    });
  }

  $('#tab-proposals').on('click', '.proposal-review', function () {
    const id = $(this).data('id');
    const decision = Number($(this).data('decision'));
    const note = prompt('ملاحظة المراجعة (إلزامية):') || '';
    if (!note) return;
    ZU.api(`/api/courses/${id}/review`, 'POST', { decision: decision, noteAr: note })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadProposals(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  let approvingTarget = null;
  $('#tab-proposals').on('click', '.approve-course', function () {
    const id = $(this).data('id');
    approvingTarget = proposalIndex[id] || null;
    const deadlineText = prompt('آخر موعد للتقييم (YYYY-MM-DD):', new Date(Date.now() + 14 * 864e5).toISOString().slice(0, 10));
    if (!deadlineText) return;
    const deadline = ZU.toIso(deadlineText + ' 23:59');
    if (!deadline) { ZU.toast('تاريخ غير صالح.', false); return; }
    ZU.api(`/api/courses/${id}/approve`, 'POST', {
      evaluationDeadlineUtc: deadline, qualityOperated: true, instructorUserId: null
    }).done(function () { ZU.toast(ZU.t('saved'), true); loadProposals(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ---------------------------------------------------------------- needs */
  function loadNeeds() {
    ZU.api('/api/training-needs').done(function (rows) {
      const host = $('#needs-list');
      if (!rows || !rows.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
      host.html(`<div class="table-responsive"><table class="table align-middle">
        <thead><tr>
          <th>${ZU.escapeHtml(ZU.t('title'))}</th>
          <th>${ZU.escapeHtml(ZU.t('description'))}</th>
          <th>${ZU.escapeHtml(ZU.t('status'))}</th>
          <th>${ZU.escapeHtml(ZU.t('actions'))}</th>
        </tr></thead>
        <tbody>${rows.map(row => `<tr>
          <td>${ZU.escapeHtml(ZU.pick(row.titleAr, row.titleEn))}</td>
          <td class="small-muted">${ZU.escapeHtml(ZU.pick(row.descriptionAr, row.descriptionEn))}</td>
          <td>${statusBadge(row.status)}
            ${row.reviewNoteAr ? `<div class="note-red small">${ZU.escapeHtml(row.reviewNoteAr)}</div>` : ''}</td>
          <td class="d-flex gap-2 flex-wrap">
            ${(row.documentIds || []).map(id => `<a class="btn btn-sm btn-outline-secondary" target="_blank"
                href="/api/official-documents/${id}">${ZU.escapeHtml(ZU.t('file'))}</a>`).join('')}
            <button class="btn btn-sm btn-outline-danger need-review" data-id="${row.id}" data-status="3" data-i18n="reject">رفض</button>
            <button class="btn btn-sm btn-primary need-review" data-id="${row.id}" data-status="2" data-i18n="approve">اعتماد</button>
          </td>
        </tr>`).join('')}</tbody></table></div>`);
      ZU.applyLanguage();
    });
  }

  $('#need-form').on('submit', function (event) {
    event.preventDefault();
    const file = $('#n-file')[0].files[0];
    if (!file) { ZU.alertBox('#needs-message', 'أرفق الكتاب الرسمي.', false); return; }
    const data = new FormData();
    data.append('titleAr', $('#n-title').val().trim());
    data.append('descriptionAr', $('#n-desc').val().trim());
    if ($('#n-faculty').val()) data.append('facultyId', $('#n-faculty').val());
    data.append('file', file);
    ZU.apiForm('/api/training-needs', 'POST', data)
      .done(function () {
        ZU.clearAlert('#needs-message');
        ZU.toast(ZU.t('saved'), true);
        $('#need-form')[0].reset();
        loadNeeds();
      })
      .fail(function (xhr) { ZU.alertBox('#needs-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#needs-list').on('click', '.need-review', function () {
    const status = Number($(this).data('status'));
    const note = status === 2 ? null : (prompt('سبب الرفض:') || '');
    if (status !== 2 && !note) return;
    ZU.api(`/api/training-needs/${$(this).data('id')}/review`, 'PUT', { status: status, reviewNoteAr: note })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadNeeds(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ---------------------------------------------------------------- cancellations */
  function loadCancellations() {
    ZU.api('/api/course-cancellations').done(function (rows) {
      const host = $('#cancel-list');
      if (!rows || !rows.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
      host.html(`<h2>${ZU.escapeHtml(ZU.t('cancellations'))}</h2>
        <div class="table-responsive"><table class="table align-middle">
        <thead><tr>
          <th>${ZU.escapeHtml(ZU.t('courses'))}</th>
          <th>${ZU.escapeHtml(ZU.t('reason'))}</th>
          <th>${ZU.escapeHtml(ZU.t('status'))}</th>
          <th>${ZU.escapeHtml(ZU.t('actions'))}</th>
        </tr></thead>
        <tbody>${rows.map(row => `<tr>
          <td>${ZU.escapeHtml(row.courseTitleAr)}</td>
          <td class="small-muted">${ZU.escapeHtml(row.reasonAr)}</td>
          <td>${statusBadge(row.status)}</td>
          <td class="d-flex gap-2 flex-wrap">
            ${(row.documentIds || []).map(id => `<a class="btn btn-sm btn-outline-secondary" target="_blank"
               href="/api/official-documents/${id}">${ZU.escapeHtml(ZU.t('file'))}</a>`).join('')}
            ${row.status === 0 || row.status === 1 ? `
              <button class="btn btn-sm btn-outline-danger cancel-review" data-id="${row.id}" data-status="3" data-i18n="reject">رفض</button>
              <button class="btn btn-sm btn-primary cancel-review" data-id="${row.id}" data-status="2" data-i18n="approve">اعتماد</button>` : ''}
          </td>
        </tr>`).join('')}</tbody></table></div>`);
      ZU.applyLanguage();
    }).fail(function () { $('#cancel-list').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#cancel-list').on('click', '.cancel-review', function () {
    const status = Number($(this).data('status'));
    const note = status === 2 ? null : (prompt('سبب الرفض:') || '');
    if (status !== 2 && !note) return;
    ZU.api(`/api/course-cancellations/${$(this).data('id')}/review`, 'PUT', { status: status, reviewNoteAr: note })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadCancellations(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ---------------------------------------------------------------- evaluation form */
  function loadEvaluation() {
    const host = $('#evaluation-host');
    ZU.api('/api/evaluation/form').done(function (form) {
      if (!form) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noEvaluationForm'))}</div>`); return; }
      host.html(`
        <div class="panel-heading"><div><h2>${ZU.escapeHtml(ZU.t('evaluation'))}</h2>
          <p>الإصدار ${form.version} · ${form.questions.length} ${ZU.escapeHtml(ZU.t('questions'))}</p></div></div>
        <div class="mb-4">
          ${form.questions.map(question => `<div class="border rounded-3 p-3 mb-2">
            <strong>${ZU.escapeHtml(ZU.pick(question.textAr, question.textEn))}</strong>
            <div class="small-muted">${question.options.map(option => ZU.escapeHtml(ZU.pick(option.textAr, option.textEn))).join(' · ')}</div>
          </div>`).join('')}
        </div>
        <h3 class="h6">${ZU.escapeHtml(ZU.t('newVersion'))}</h3>
        <p class="small-muted">كل سطر: نص السؤال | خيار١ ; خيار٢ ; خيار٣</p>
        <textarea id="form-source" class="form-control" rows="8" dir="rtl"></textarea>
        <button class="btn btn-primary mt-2" id="publish-form" type="button" data-i18n="publish">نشر الإصدار</button>
        <a class="btn btn-outline-secondary mt-2" href="/statistics.html" data-i18n="statistics">الإحصاءات</a>`);
      ZU.applyLanguage();
      $('#form-source').val(form.questions.map(question =>
        ZU.pick(question.textAr, question.textEn) + ' | ' +
        question.options.map(option => ZU.pick(option.textAr, option.textEn)).join(' ; ')).join('\n'));
    }).fail(function () {
      host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noEvaluationForm'))}</div>`);
    });
  }

  $('#evaluation-host').on('click', '#publish-form', function () {
    const lines = ($('#form-source').val() || '').split('\n').map(line => line.trim()).filter(Boolean);
    if (!lines.length) { ZU.toast('أدخل سؤالاً واحداً على الأقل.', false); return; }
    const questions = lines.map(line => {
      const [text, options] = line.split('|');
      const optionTexts = (options || '').split(';').map(x => x.trim()).filter(Boolean);
      return {
        textAr: (text || '').trim(), textEn: null,
        options: optionTexts.map(text => ({ textAr: text, textEn: null }))
      };
    }).filter(question => question.textAr && question.options.length >= 2);
    if (!questions.length) { ZU.toast('تأكد من صيغة الأسئلة والخيارات.', false); return; }
    ZU.api('/api/evaluation/form/publish', 'POST', { questions: questions })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadEvaluation(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ---------------------------------------------------------------- historical */
  function renderHistorical() {
    const host = $('#historical-host');
    host.html(`
      <div class="panel-heading"><div><h2>${ZU.escapeHtml(ZU.t('historical'))}</h2>
        <p>توثيق دورة عُقدت قبل إطلاق المنصة وإصدار شهادات المستحقين.</p></div></div>
      <div id="historical-message" class="alert d-none"></div>
      <form id="historical-form" class="row g-2">
        <div class="col-md-4"><input id="h-title" class="form-control" placeholder="عنوان الدورة" required></div>
        <div class="col-md-3"><input id="h-hours" class="form-control" type="number" min="1" step="0.5" value="6" required></div>
        <div class="col-md-5"><select id="h-track" class="form-select" required></select></div>
        <div class="col-md-3"><input id="h-start" class="form-control" data-date required></div>
        <div class="col-md-3"><input id="h-end" class="form-control" data-date required></div>
        <div class="col-md-3"><input id="h-lectures" class="form-control" type="number" min="1" value="6" required></div>
        <div class="col-md-3"><input id="h-instructor" class="form-control" placeholder="اسم المحاضر"></div>
        <div class="col-12"><textarea id="h-obj" class="form-control" rows="2" placeholder="أهداف الدورة" required></textarea></div>
        <div class="col-12"><textarea id="h-audience" class="form-control" rows="1" placeholder="الفئة المستهدفة" required></textarea></div>
        <div class="col-md-6"><input id="h-search" class="form-control" placeholder="بحث عن مشارك بالاسم أو الرقم"></div>
        <div class="col-md-2"><button class="btn btn-outline-primary w-100" id="h-search-btn" type="button" data-i18n="search">بحث</button></div>
        <div class="col-12"><div id="h-results"></div></div>
        <div class="col-12"><div id="h-participants" class="small-muted">لم يُضف مشاركون بعد.</div></div>
        <div class="col-12">
          <button class="btn btn-primary" type="submit" data-i18n="save">تسجيل الدورة وإصدار الشهادات</button>
        </div>
      </form>`);
    ZU.initDatePickers(host[0]);
    const participants = [];
    host.data('participants', participants);
    ZU.api('/api/courses/tracks').done(rows => {
      $('#h-track').html((rows || []).map(track =>
        `<option value="${track.id}">${ZU.escapeHtml(ZU.pick(track.nameAr, track.nameEn))}</option>`).join(''));
    });
    $('#h-search-btn', host).on('click', function () {
      const query = $('#h-search').val().trim();
      if (!query) return;
      ZU.api('/api/courses/directory-users?query=' + encodeURIComponent(query)).done(function (rows) {
        $('#h-results').html((rows || []).slice(0, 8).map(user => `<button type="button"
          class="btn btn-sm btn-outline-secondary me-1 mb-1 h-add" data-id="${user.id}"
          data-name="${ZU.escapeHtml(ZU.pick(user.nameAr, user.nameEn))}">
          ${ZU.escapeHtml(ZU.pick(user.nameAr, user.nameEn))} · ${ZU.escapeHtml(user.loginName)}</button>`).join(''));
      });
    });
    $('#h-results', host).on('click', '.h-add', function () {
      const list = host.data('participants');
      if (list.some(x => x.userId === $(this).data('id'))) return;
      list.push({ userId: $(this).data('id'), name: $(this).data('name'), attendedLectures: 0, examOutcome: 0 });
      renderParticipants(host, list, $('#h-lectures').val());
    });
    $('#h-lectures', host).on('change', () => renderParticipants(host, host.data('participants'), $('#h-lectures').val()));
    $('#h-participants', host).on('change', 'input[data-idx]', function () {
      const list = host.data('participants');
      const idx = Number($(this).data('idx'));
      list[idx][$(this).data('field')] = $(this).data('field') === 'attendedLectures'
        ? Number($(this).val()) : Number($(this).val());
    });
    $('#h-participants', host).on('change', 'input.h-confirm', function () {
      const list = host.data('participants');
      const idx = Number($(this).data('idx'));
      list[idx][$(this).data('field')] = $(this).is(':checked');
    });
    $('#historical-form', host).on('submit', function (event) {
      event.preventDefault();
      const list = host.data('participants');
      if (!list.length) { ZU.alertBox('#historical-message', 'أضف مشاركاً واحداً على الأقل.', false); return; }
      const payload = {
        titleAr: $('#h-title').val().trim(),
        titleEn: null,
        trainingTrackId: $('#h-track').val(),
        objectivesAr: $('#h-obj').val().trim(),
        objectivesEn: null,
        targetAudienceAr: $('#h-audience').val().trim(),
        targetAudienceEn: null,
        deliveryMode: 0,
        plannedHours: Number($('#h-hours').val()),
        startUtc: ZU.toIso($('#h-start').val() + ' 09:00'),
        endUtc: ZU.toIso($('#h-end').val() + ' 15:00'),
        facultyId: null,
        instructorUserId: null,
        actualInstructorNameAr: $('#h-instructor').val().trim() || null,
        actualInstructorNameEn: null,
        hasExam: false,
        examNameAr: null,
        examNameEn: null,
        lectureCount: Number($('#h-lectures').val()),
        enrollmentModeSelfRegistration: false,
        participants: list.map(p => ({
          userId: p.userId, attendedLectures: p.attendedLectures || 0,
          examOutcome: p.examOutcome || 0, evaluationConfirmed: !!p.evaluationConfirmed,
          impactConfirmed: !!p.impactConfirmed
        }))
      };
      ZU.api('/api/courses/historical', 'POST', payload)
        .done(function () {
          ZU.clearAlert('#historical-message');
          ZU.toast(ZU.t('saved'), true);
          host.data('participants', []);
          $('#h-participants', host).text('لم يُضف مشاركون بعد.');
          $('#historical-form', host)[0].reset();
        })
        .fail(function (xhr) {
          ZU.alertBox('#historical-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false);
        });
    });
  }

  function renderParticipants(host, list, lectureCount) {
    const total = Number(lectureCount) || 1;
    $('#h-participants', host).html(list.length ? `<table class="table table-sm">
      <thead><tr><th>${ZU.escapeHtml(ZU.t('member'))}</th><th>${ZU.escapeHtml(ZU.t('attendance'))}</th>
        <th>${ZU.escapeHtml(ZU.t('evaluation'))}</th><th>${ZU.escapeHtml(ZU.t('impact'))}</th></tr></thead>
      <tbody>${list.map((p, index) => `<tr>
        <td>${ZU.escapeHtml(p.name)}</td>
        <td><input type="number" class="form-control form-control-sm" style="width:90px"
          data-idx="${index}" data-field="attendedLectures" min="0" max="${total}" value="${p.attendedLectures || 0}"></td>
        <td><input type="checkbox" class="form-check-input h-confirm" data-idx="${index}" data-field="evaluationConfirmed" ${p.evaluationConfirmed ? 'checked' : ''}></td>
        <td><input type="checkbox" class="form-check-input h-confirm" data-idx="${index}" data-field="impactConfirmed" ${p.impactConfirmed ? 'checked' : ''}></td>
      </tr>`).join('')}</tbody></table>` : 'لم يُضف مشاركون بعد.');
  }

  /* ---------------------------------------------------------------- external trainers */
  function loadExternal() {
    const host = $('#external-host');
    ZU.api('/api/external-trainers').done(function (rows) {
      host.html(`
        <div class="panel-heading"><div><h2>${ZU.escapeHtml(ZU.t('externalTrainers'))}</h2>
          <p>الحساب الخارجي يُنشأ برقم دخول من أربعة أرقام ويُرسل إلى البريد المذكور.</p></div></div>
        <div id="external-message" class="alert d-none"></div>
        <form id="external-form" class="row g-2 mb-4">
          <div class="col-md-3"><input id="e-name" class="form-control" placeholder="اسم المدرب" required></div>
          <div class="col-md-3"><input id="e-email" type="email" class="form-control" placeholder="البريد الإلكتروني" required></div>
          <div class="col-md-3"><input id="e-org" class="form-control" placeholder="الجهة"></div>
          <div class="col-md-3"><input id="e-note" class="form-control" placeholder="ملاحظة"></div>
          <div class="col-md-3"><button class="btn btn-primary w-100" type="submit" data-i18n="save">إرسال الطلب</button></div>
        </form>
        ${rows && rows.length ? `<div class="table-responsive"><table class="table align-middle">
          <thead><tr><th>${ZU.escapeHtml(ZU.t('name'))}</th><th>${ZU.escapeHtml(ZU.t('email'))}</th>
            <th>${ZU.escapeHtml(ZU.t('status'))}</th><th>${ZU.escapeHtml(ZU.t('actions'))}</th></tr></thead>
          <tbody>${rows.map(row => `<tr>
            <td>${ZU.escapeHtml(ZU.pick(row.trainerNameAr, row.trainerNameEn))}
              <div class="small-muted">${ZU.escapeHtml(row.organizationAr || '—')}</div></td>
            <td dir="ltr">${ZU.escapeHtml(row.email)}</td>
            <td>${row.status === 1 ? `<span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('accountCreated'))}</span>`
              : row.status === 2 ? `<span class="badge-soft badge-danger">${ZU.escapeHtml(ZU.t('rejected'))}</span>`
              : `<span class="badge-soft badge-warn">${ZU.escapeHtml(ZU.t('pending'))}</span>`}
              ${row.createdLoginName ? `<div class="small-muted">${ZU.escapeHtml(row.createdLoginName)}</div>` : ''}</td>
            <td class="d-flex gap-2">${row.status === 0 && ZU.can('admin.users.manage') ? `<button class="btn btn-sm btn-primary ext-fulfil"
                data-id="${row.id}" data-i18n="account">إنشاء حساب</button>
              <button class="btn btn-sm btn-outline-danger ext-reject" data-id="${row.id}" data-i18n="reject">رفض</button>` : ''}</td>
          </tr>`).join('')}</tbody></table></div>` : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`}`);
      ZU.applyLanguage();
    });
  }

  $('#external-host').on('submit', '#external-form', function (event) {
    event.preventDefault();
    ZU.api('/api/external-trainers', 'POST', {
      trainerNameAr: $('#e-name').val().trim(), trainerNameEn: null,
      email: $('#e-email').val().trim(), organizationAr: $('#e-org').val().trim() || null,
      noteAr: $('#e-note').val().trim() || null, courseId: null
    }).done(function () { ZU.toast(ZU.t('saved'), true); loaded.external = false; loadExternal(); })
      .fail(function (xhr) { ZU.alertBox('#external-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#external-host').on('click', '.ext-fulfil', function () {
    const id = $(this).data('id');
    ZU.api('/api/external-trainers/suggest-login').done(function (suggestion) {
      const loginName = prompt('رقم الدخول (4 أرقام):', suggestion.login || '');
      if (!loginName) return;
      const password = prompt('كلمة المرور الأولية (8–64 حرفاً):',
        'Zu#' + Math.random().toString(36).slice(2, 10) + '1');
      if (!password) return;
      ZU.api(`/api/external-trainers/${id}/account`, 'POST', { loginName: loginName, password: password })
        .done(function () { ZU.toast(ZU.t('saved'), true); loadExternal(); })
        .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
    });
  });

  $('#external-host').on('click', '.ext-reject', function () {
    const note = prompt('سبب الرفض:') || '';
    if (!note) return;
    ZU.api(`/api/external-trainers/${$(this).data('id')}/reject`, 'POST', { noteAr: note })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadExternal(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ---------------------------------------------------------------- vault */
  function loadVault() {
    const host = $('#vault-host');
    host.html(`<div class="panel-heading"><div><h2>${ZU.escapeHtml(ZU.t('documentVault'))}</h2>
      <p>كل الوثائق الرسمية المرفوعة مع بصمة SHA-256.</p></div></div>
      <div class="d-flex gap-2 mb-3">
        <input id="vault-query" class="form-control" placeholder="بحث باسم الملف">
        <button class="btn btn-outline-primary" id="vault-search" type="button" data-i18n="search">بحث</button>
      </div>
      <div id="vault-rows"><div class="loading">${ZU.escapeHtml(ZU.t('loading'))}</div></div>`);
    loadVaultRows('');
  }

  function loadVaultRows(query) {
    ZU.api('/api/official-documents?query=' + encodeURIComponent(query || ''))
      .done(function (page) {
        const rows = page.items || [];
        $('#vault-rows').html(rows.length ? `<div class="table-responsive"><table class="table align-middle">
          <thead><tr><th>${ZU.escapeHtml(ZU.t('file'))}</th><th>${ZU.escapeHtml(ZU.t('kind'))}</th>
            <th>SHA-256</th><th>${ZU.escapeHtml(ZU.t('uploadedAt'))}</th><th></th></tr></thead>
          <tbody>${rows.map(row => `<tr>
            <td>${ZU.escapeHtml(row.originalName)}<div class="small-muted">${ZU.escapeHtml(row.entityType)}</div></td>
            <td>${ZU.escapeHtml(row.kind)}</td>
            <td class="small-muted" dir="ltr">${ZU.escapeHtml((row.sha256 || '').slice(0, 16))}…</td>
            <td>${ZU.escapeHtml(ZU.fmtDateTime(row.uploadedAtUtc))}</td>
            <td><a class="btn btn-sm btn-outline-primary" target="_blank" href="/api/official-documents/${row.id}">${ZU.escapeHtml(ZU.t('download'))}</a></td>
          </tr>`).join('')}</tbody></table></div>
          <div class="small-muted">${ZU.escapeHtml(ZU.t('total'))}: ${page.total}</div>`
          : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
      })
      .fail(function () { $('#vault-rows').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#vault-host').on('click', '#vault-search', function () { loadVaultRows($('#vault-query').val().trim()); });
})();
