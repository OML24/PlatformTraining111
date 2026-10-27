/* لوحة الإدارة: المستخدمون، الصلاحيات، الدورات، المسارات، الإعدادات، التزامن، سجل التدقيق. */
(function () {
  'use strict';

  const S = ZU.state;
  let roles = [];
  let faculties = [];
  let users = [];
  let selectedUserId = null;
  let auditTimer = null;
  const loaded = {};

  ZU.boot({
    page: 'admin',
    onReady: function () {
      if (!ZU.requirePermission('page.admin')) return;
      ZU.api('/api/admin/roles').done(function (rows) {
        roles = rows || [];
        $('#role-select').html(roles.map(role =>
          `<option value="${role.key}">${ZU.escapeHtml(ZU.pick(role.nameAr, role.nameEn))}</option>`).join(''));
        $('#nu-roles').html(roles.map(role => `<div class="form-check">
          <input class="form-check-input nu-role" type="checkbox" value="${role.key}" id="nr_${role.key}">
          <label class="form-check-label" for="nr_${role.key}">${ZU.escapeHtml(ZU.pick(role.nameAr, role.nameEn))}</label>
        </div>`).join(''));
        loadRoleMatrix();
      });
      ZU.api('/api/courses/faculties').done(function (rows) {
        faculties = rows || [];
        $('#nu-faculty').append(faculties.map(faculty =>
          `<option value="${faculty.id}">${ZU.escapeHtml(ZU.pick(faculty.nameAr, faculty.nameEn))}</option>`).join(''));
      });
      loadUsers();
      loadSharedPassword();
    }
  });

  $('#admin-tabs').on('click', 'button[data-tab]', function () {
    const tab = $(this).data('tab');
    $('#admin-tabs button').removeClass('active');
    $(this).addClass('active');
    $('main.app-main > section.panel').addClass('d-none');
    $('#tab-' + tab).removeClass('d-none');
    stopLive();
    if (loaded[tab]) return;
    loaded[tab] = true;
    if (tab === 'courses') loadAdminCourses();
    if (tab === 'tracks') loadTracks();
    if (tab === 'permissions') loadRoleMatrix();
    if (tab === 'settings') loadSettings();
    if (tab === 'audit') loadAudit();
    if (tab === 'vault') loadVault(null);
  });

  /* ------------------------------------------------------------------ users */
  function loadUsers(query) {
    ZU.api('/api/admin/users?query=' + encodeURIComponent(query || ''))
      .done(function (rows) {
        users = rows || [];
        const total = users.length;
        const host = $('#users-list');
        if (!users.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
        host.html(`<div class="small-muted mb-2">${ZU.escapeHtml(ZU.t('total'))}: ${total}</div>
          <div class="table-responsive"><table class="table table-sm align-middle">
          <thead><tr>
            <th>${ZU.escapeHtml(ZU.t('name'))}</th>
            <th>${ZU.escapeHtml(ZU.t('employeeNumber'))}</th>
            <th>${ZU.escapeHtml(ZU.t('roles'))}</th>
            <th>${ZU.escapeHtml(ZU.t('status'))}</th>
            <th>${ZU.escapeHtml(ZU.t('actions'))}</th>
          </tr></thead>
          <tbody>${users.map(user => `<tr data-id="${user.id}">
            <td><strong>${ZU.escapeHtml(ZU.pick(user.nameAr, user.nameEn))}</strong>
              <div class="small-muted" dir="ltr">${ZU.escapeHtml(user.email || '—')}</div></td>
            <td dir="ltr">${ZU.escapeHtml(user.loginName)}</td>
            <td class="small">${(user.roles || []).map(r => `<span class="badge-soft badge-muted me-1">${ZU.escapeHtml(r)}</span>`).join('')}</td>
            <td>${user.isActive ? `<span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('active'))}</span>`
              : `<span class="badge-soft badge-danger">${ZU.escapeHtml(ZU.t('inactive'))}</span>`}
              ${user.isExternalTrainer ? `<span class="badge-soft badge-navy">${ZU.escapeHtml(ZU.t('external'))}</span>` : ''}</td>
            <td class="d-flex gap-1 flex-wrap">
              <button class="btn btn-sm btn-outline-primary user-perms" data-id="${user.id}" data-i18n="permissions">صلاحيات</button>
              <button class="btn btn-sm btn-outline-primary user-roles" data-id="${user.id}" data-i18n="roles">أدوار</button>
              <button class="btn btn-sm btn-outline-secondary user-password" data-id="${user.id}" data-i18n="password">كلمة المرور</button>
              <button class="btn btn-sm btn-outline-secondary user-credentials" data-id="${user.id}">بيانات الدخول</button>
              <button class="btn btn-sm btn-outline-success user-email" data-id="${user.id}" data-i18n="sendCredentials">إرسال بالبريد</button>
              <button class="btn btn-sm ${user.isActive ? 'btn-outline-danger' : 'btn-outline-success'} user-active"
                data-id="${user.id}" data-active="${user.isActive}">${ZU.escapeHtml(user.isActive ? ZU.t('deactivate') : ZU.t('activate'))}</button>
            </td>
          </tr>`).join('')}</tbody></table></div>
          <div class="panel border rounded-3 p-3 mt-3 d-none" id="user-panel"><div id="user-panel-body"></div></div>`);
        ZU.applyLanguage();
      })
      .fail(function () { $('#users-list').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#user-search').on('click', () => loadUsers($('#user-query').val().trim()));
  $('#user-query').on('keypress', event => { if (event.which === 13) loadUsers($('#user-query').val().trim()); });
  $('#new-user-toggle').on('click', () => $('#new-user-box').toggleClass('d-none'));

  $('#new-user-form').on('submit', function (event) {
    event.preventDefault();
    const external = $('#nu-external').is(':checked');
    const loginText = $('#nu-login').val().trim();
    const payload = {
      employeeId: external ? null : Number(loginText || 0),
      externalLoginName: external ? loginText : null,
      nameAr: $('#nu-name').val().trim(),
      nameEn: $('#nu-name-en').val().trim() || null,
      email: $('#nu-email').val().trim() || null,
      isExternalTrainer: external,
      externalPassword: external ? ($('#nu-password').val() || null) : null,
      facultyId: $('#nu-faculty').val() || null,
      departmentId: null,
      roleKeys: $('.nu-role:checked').map(function () { return $(this).val(); }).get()
    };
    ZU.api('/api/admin/users', 'POST', payload)
      .done(function () {
        ZU.clearAlert('#new-user-message');
        ZU.toast(ZU.t('saved'), true);
        $('#new-user-form')[0].reset();
        $('#new-user-box').addClass('d-none');
        loadUsers();
      })
      .fail(function (xhr) {
        ZU.alertBox('#new-user-message', (xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false);
      });
  });

  $('#users-list').on('click', '.user-password', function () {
    const id = $(this).data('id');
    const password = prompt('كلمة المرور الجديدة (8–64 حرفاً):');
    if (!password) return;
    ZU.api(`/api/admin/users/${id}/password`, 'PUT', { password: password })
      .done(function () { ZU.toast(ZU.t('saved'), true); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#users-list').on('click', '.user-credentials', function () {
    const id = $(this).data('id');
    ZU.api(`/api/admin/users/${id}/credentials`).done(function (result) {
      $('#user-panel').removeClass('d-none');
      $('#user-panel-body').html(`
        <div class="d-flex justify-content-between align-items-center">
          <strong>بيانات الدخول</strong>
          <button class="btn btn-sm btn-outline-secondary" id="close-user-panel">${ZU.escapeHtml(ZU.t('close'))}</button>
        </div>
        <div class="row g-2 mt-2">
          <div class="col-md-6"><div class="small-muted">كلمة المرور الأولية</div>
            <code dir="ltr">${ZU.escapeHtml(result.initialPassword || '—')}</code></div>
          <div class="col-md-6"><div class="small-muted">كلمة المرور الحالية</div>
            <code dir="ltr">${ZU.escapeHtml(result.currentPassword || '—')}</code></div>
        </div>
        <div class="note-red mt-2">تُعرض هذه البيانات مرة واحدة وتُسجل عملية الاطلاع في سجل التدقيق.</div>`);
    }).fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#users-list').on('click', '#close-user-panel', () => $('#user-panel').addClass('d-none'));

  $('#users-list').on('click', '.user-email', function () {
    const id = $(this).data('id');
    ZU.api(`/api/admin/users/${id}/send-credentials`, 'POST')
      .done(function () { ZU.toast(ZU.t('saved'), true); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || 'تعذر الإرسال، تحقق من إعدادات البريد.', false); });
  });

  $('#users-list').on('click', '.user-active', function () {
    const id = $(this).data('id');
    const isActive = String($(this).data('active')) === 'true';
    ZU.api(`/api/admin/users/${id}/active`, 'PUT', { isActive: !isActive })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadUsers($('#user-query').val().trim()); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#users-list').on('click', '.user-roles', function () {
    const id = $(this).data('id');
    const user = users.find(x => x.id === id);
    const input = prompt('أدوار مفصولة بفاصلة (ADMIN, QA, DEAN, INST, MEMBER, EXTERNAL_INSTRUCTOR):',
      (user.roles || []).join(', '));
    if (input === null) return;
    const keys = input.split(',').map(x => x.trim()).filter(Boolean);
    ZU.api(`/api/admin/users/${id}/roles`, 'PUT', { roleKeys: keys })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadUsers($('#user-query').val().trim()); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#users-list').on('click', '.user-perms', function () {
    const id = $(this).data('id');
    selectedUserId = id;
    ZU.api(`/api/admin/users/${id}/permissions`).done(function (rows) {
      $('#user-panel').removeClass('d-none');
      $('#user-panel-body').html(`
        <div class="d-flex justify-content-between align-items-center mb-2">
          <strong>${ZU.escapeHtml(ZU.t('permissions'))}</strong>
          <button class="btn btn-sm btn-outline-secondary" id="close-user-panel">${ZU.escapeHtml(ZU.t('close'))}</button>
        </div>
        <div class="table-responsive" style="max-height:420px;overflow:auto"><table class="table table-sm">
          <thead><tr><th>${ZU.escapeHtml(ZU.t('permission'))}</th><th>${ZU.escapeHtml(ZU.t('inherited'))}</th>
            <th>${ZU.escapeHtml(ZU.t('override'))}</th><th>${ZU.escapeHtml(ZU.t('effective'))}</th></tr></thead>
          <tbody>${(rows || []).map(row => `<tr>
            <td>${ZU.escapeHtml(row.nameAr || row.key)}<div class="small-muted" dir="ltr">${ZU.escapeHtml(row.key)}</div></td>
            <td>${row.inherited ? '✓' : '—'}</td>
            <td>
              <select class="form-select form-select-sm user-perm" data-key="${row.key}" style="min-width:120px">
                <option value="0" ${row.overrideMode === 0 ? 'selected' : ''}>${ZU.escapeHtml(ZU.t('inherit'))}</option>
                <option value="1" ${row.overrideMode === 1 ? 'selected' : ''}>${ZU.escapeHtml(ZU.t('allow'))}</option>
                <option value="2" ${row.overrideMode === 2 ? 'selected' : ''}>${ZU.escapeHtml(ZU.t('deny'))}</option>
              </select>
            </td>
            <td>${row.effective ? `<span class="badge-soft badge-ok">✓</span>` : `<span class="badge-soft badge-muted">—</span>`}</td>
          </tr>`).join('')}</tbody></table></div>`);
    });
  });

  $('#users-list').on('change', '.user-perm', function () {
    const key = $(this).data('key');
    const mode = Number($(this).val());
    ZU.api(`/api/admin/users/${selectedUserId}/permissions/${encodeURIComponent(key)}`, 'PUT', { mode: mode })
      .done(function () {
        ZU.toast(ZU.t('saved'), true);
        $(`.user-perms[data-id="${selectedUserId}"]`).trigger('click');
      })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------------ role matrix */
  function loadRoleMatrix() {
    const roleKey = $('#role-select').val();
    if (!roleKey) return;
    ZU.api(`/api/admin/roles/${roleKey}/permissions`).done(function (rows) {
      $('#role-matrix').html(`<div class="table-responsive" style="max-height:520px;overflow:auto">
        <table class="table table-sm align-middle">
          <thead><tr><th>${ZU.escapeHtml(ZU.t('permission'))}</th><th style="width:130px">${ZU.escapeHtml(ZU.t('granted'))}</th></tr></thead>
          <tbody>${(rows || []).map(row => `<tr>
            <td>${ZU.escapeHtml(row.nameAr || row.key)}<div class="small-muted" dir="ltr">${ZU.escapeHtml(row.key)}</div></td>
            <td><div class="form-check form-switch">
              <input class="form-check-input role-perm" type="checkbox" data-key="${row.key}" ${row.granted ? 'checked' : ''}>
            </div></td>
          </tr>`).join('')}</tbody></table></div>`);
    });
  }

  $('#role-select').on('change', loadRoleMatrix);

  $('#role-matrix').on('change', '.role-perm', function () {
    const roleKey = $('#role-select').val();
    const key = $(this).data('key');
    ZU.api(`/api/admin/roles/${roleKey}/permissions/${encodeURIComponent(key)}`, 'PUT',
      { granted: $(this).is(':checked') })
      .done(function () { ZU.toast(ZU.t('saved'), true); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  function loadSharedPassword() {
    ZU.api('/api/admin/shared-initial-password').done(function (result) {
      $('#shared-password').html(`كلمة المرور الأولية المشتركة: <code dir="ltr">${ZU.escapeHtml(result.password || '—')}</code>
        <button class="btn btn-sm btn-outline-secondary ms-2" id="set-shared">تغيير</button>`);
    }).fail(function () { $('#shared-password').empty(); });
  }

  $('#tab-permissions').on('click', '#set-shared', function () {
    const password = prompt('كلمة المرور الأولية المشتركة الجديدة:');
    if (!password) return;
    ZU.api('/api/admin/shared-initial-password', 'PUT', { password: password })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadSharedPassword(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------------ courses */
  function loadAdminCourses() {
    ZU.api('/api/admin/courses').done(function (rows) {
      const host = $('#admin-courses');
      if (!rows || !rows.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
      host.html(`<div class="table-responsive"><table class="table align-middle">
        <thead><tr><th>${ZU.escapeHtml(ZU.t('title'))}</th><th>${ZU.escapeHtml(ZU.t('status'))}</th>
          <th>${ZU.escapeHtml(ZU.t('certificates'))}</th><th>${ZU.escapeHtml(ZU.t('state'))}</th>
          <th>${ZU.escapeHtml(ZU.t('actions'))}</th></tr></thead>
        <tbody>${rows.map(course => `<tr>
          <td>${ZU.escapeHtml(ZU.pick(course.titleAr, course.titleEn))}</td>
          <td>${course.status}</td>
          <td>${course.certificateCount}</td>
          <td>${course.isArchived ? `<span class="badge-soft badge-muted">${ZU.escapeHtml(ZU.t('archived'))}</span>` : ''}
              ${course.isActive ? `<span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('active'))}</span>`
                : `<span class="badge-soft badge-danger">${ZU.escapeHtml(ZU.t('disabled'))}</span>`}</td>
          <td>${course.isActive ? `<button class="btn btn-sm btn-outline-danger deactivate-course" data-id="${course.id}"
                data-certs="${course.certificateCount}" data-i18n="deactivate">تعطيل</button>`
              : `<button class="btn btn-sm btn-outline-success restore-course" data-id="${course.id}" data-i18n="restore">استعادة</button>`}</td>
        </tr>`).join('')}</tbody></table></div>`);
      ZU.applyLanguage();
    }).fail(function () { $('#admin-courses').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#reload-admin-courses').on('click', loadAdminCourses);

  $('#admin-courses').on('click', '.deactivate-course', function () {
    const id = $(this).data('id');
    const certificates = Number($(this).data('certs')) || 0;
    const hide = certificates > 0
      ? confirm(`هذه الدورة مرتبطة بـ ${certificates} شهادة. هل تريد إخفاء الشهادات أيضاً؟`)
      : false;
    if (!confirm('تحذير: التعطيل يخفي الدورة من المتدربين دون حذف السجلات. المتابعة؟')) return;
    ZU.api(`/api/admin/courses/${id}`, 'DELETE', { hideCertificates: hide, warningAcknowledged: true })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadAdminCourses(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#admin-courses').on('click', '.restore-course', function () {
    ZU.api(`/api/admin/courses/${$(this).data('id')}/restore`, 'POST')
      .done(function () { ZU.toast(ZU.t('saved'), true); loadAdminCourses(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------------ tracks */
  function loadTracks() {
    ZU.api('/api/admin/courses/tracks').done(function (rows) {
      const host = $('#tracks-list');
      if (!rows || !rows.length) { host.html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return; }
      host.html(`<div class="table-responsive"><table class="table align-middle">
        <thead><tr><th>${ZU.escapeHtml(ZU.t('name'))}</th><th>${ZU.escapeHtml(ZU.t('objectivesAr'))}</th>
          <th>${ZU.escapeHtml(ZU.t('status'))}</th><th>${ZU.escapeHtml(ZU.t('actions'))}</th></tr></thead>
        <tbody>${rows.map(track => `<tr>
          <td>${ZU.escapeHtml(ZU.pick(track.nameAr, track.nameEn))}</td>
          <td class="small-muted">${ZU.escapeHtml(track.objectivesAr || '—')}</td>
          <td>${track.isActive === false ? `<span class="badge-soft badge-danger">${ZU.escapeHtml(ZU.t('disabled'))}</span>`
            : `<span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('active'))}</span>`}</td>
          <td class="d-flex gap-1">
            <button class="btn btn-sm btn-outline-primary track-edit" data-id="${track.id}" data-i18n="edit">تعديل</button>
            <button class="btn btn-sm ${track.isActive === false ? 'btn-outline-success' : 'btn-outline-danger'} track-active"
              data-id="${track.id}" data-active="${track.isActive !== false}">
              ${ZU.escapeHtml(track.isActive === false ? ZU.t('restore') : ZU.t('deactivate'))}</button>
          </td>
        </tr>`).join('')}</tbody></table></div>`);
      ZU.applyLanguage();
    });
  }

  $('#track-form').on('submit', function (event) {
    event.preventDefault();
    const id = $('#track-id').val();
    const payload = {
      nameAr: $('#track-name-ar').val().trim(),
      nameEn: $('#track-name-en').val().trim() || null,
      objectivesAr: $('#track-obj-ar').val().trim() || null,
      objectivesEn: null
    };
    const call = id ? ZU.api(`/api/admin/courses/tracks/${id}`, 'PUT', payload)
      : ZU.api('/api/admin/courses/tracks', 'POST', payload);
    call.done(function () {
      ZU.toast(ZU.t('saved'), true);
      $('#track-form')[0].reset();
      $('#track-id').val('');
      loadTracks();
    }).fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#tracks-list').on('click', '.track-edit', function () {
    const id = $(this).data('id');
    ZU.api('/api/admin/courses/tracks').done(function (rows) {
      const track = (rows || []).find(x => x.id === id);
      if (!track) return;
      $('#track-id').val(track.id);
      $('#track-name-ar').val(track.nameAr);
      $('#track-name-en').val(track.nameEn || '');
      $('#track-obj-ar').val(track.objectivesAr || '');
      $('#track-name-ar').focus();
    });
  });

  $('#tracks-list').on('click', '.track-active', function () {
    const id = $(this).data('id');
    const isActive = String($(this).data('active')) === 'true';
    ZU.api(`/api/admin/courses/tracks/${id}/active`, 'PUT', { isActive: !isActive })
      .done(function () { ZU.toast(ZU.t('saved'), true); loadTracks(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------------ settings */
  function loadSettings() {
    const host = $('#settings-host');
    host.html(`<div class="panel-heading"><div><h2>${ZU.escapeHtml(ZU.t('settings'))}</h2>
      <p>التلميحات، سياسة كلمة المرور، وقوالب الشهادات.</p></div></div>
      <div class="row g-3">
        <div class="col-lg-6">
          <div class="border rounded-3 p-3 h-100">
            <h3 class="h6">${ZU.escapeHtml(ZU.t('passwordPolicy'))}</h3>
            <form id="policy-form" class="row g-2 mt-1">
              <div class="col-6"><label class="form-label small">الحد الأدنى</label>
                <input id="pol-min" type="number" min="8" max="16" class="form-control"></div>
              <div class="col-6"><label class="form-label small">الحد الأقصى</label>
                <input id="pol-max" type="number" min="16" max="128" class="form-control"></div>
              <div class="col-12 form-check ms-1"><input id="pol-spaces" type="checkbox" class="form-check-input">
                <label class="form-check-label" for="pol-spaces">السماح بالمسافات</label></div>
              <div class="col-12 form-check ms-1"><input id="pol-mixed" type="checkbox" class="form-check-input">
                <label class="form-check-label" for="pol-mixed">اشتراط تنوع المحارف</label></div>
              <div class="col-12 form-check ms-1"><input id="pol-common" type="checkbox" class="form-check-input">
                <label class="form-check-label" for="pol-common">منع الكلمات الشائعة</label></div>
              <div class="col-12"><button class="btn btn-primary" type="submit">${ZU.escapeHtml(ZU.t('save'))}</button></div>
            </form>
          </div>
        </div>
        <div class="col-lg-6">
          <div class="border rounded-3 p-3 h-100">
            <h3 class="h6">${ZU.escapeHtml(ZU.t('certificateTemplates'))}</h3>
            <form id="template-form" class="row g-2 mt-1">
              <div class="col-6"><input id="tpl-name-ar" class="form-control" placeholder="اسم القالب" required></div>
              <div class="col-6"><input id="tpl-file" type="file" class="form-control" accept="application/pdf" required></div>
              <div class="col-12"><button class="btn btn-primary" type="submit">${ZU.escapeHtml(ZU.t('upload'))}</button></div>
            </form>
            <div id="templates-list" class="mt-2 small"></div>
          </div>
        </div>
        <div class="col-12">
          <div class="border rounded-3 p-3">
            <h3 class="h6">${ZU.escapeHtml(ZU.t('hints'))}</h3>
            <form id="hint-form" class="row g-2 mb-2">
              <div class="col-md-2"><input id="hint-screen" class="form-control" placeholder="screenKey" dir="ltr" required></div>
              <div class="col-md-2"><input id="hint-field" class="form-control" placeholder="fieldKey" dir="ltr" required></div>
              <div class="col-md-4"><input id="hint-ar" class="form-control" placeholder="التلميح بالعربية" required></div>
              <div class="col-md-3"><input id="hint-en" class="form-control" dir="ltr" placeholder="Hint (English)"></div>
              <div class="col-md-1"><button class="btn btn-primary w-100" type="submit">+</button></div>
            </form>
            <div id="hints-list" class="table-responsive" style="max-height:320px;overflow:auto"></div>
          </div>
        </div>
      </div>`);

    ZU.api('/api/settings/password-policy').done(function (policy) {
      $('#pol-min').val(policy.minimumLength);
      $('#pol-max').val(policy.maximumLength);
      $('#pol-spaces').prop('checked', policy.allowSpaces);
      $('#pol-mixed').prop('checked', policy.requireMixedCharacterClasses);
      $('#pol-common').prop('checked', policy.blockCommonPasswords);
    });

    ZU.api('/api/settings/certificate-templates').done(function (rows) {
      $('#templates-list').html((rows || []).map(row => `<div class="d-flex justify-content-between align-items-center border-bottom py-1">
        <span>${ZU.escapeHtml(row.nameAr)} <span class="small-muted">v${row.version}</span></span>
        <a class="btn btn-sm btn-outline-secondary" target="_blank" href="/api/settings/certificate-templates/${row.id}/file">PDF</a>
      </div>`).join('') || ZU.escapeHtml(ZU.t('noRows')));
    });

    ZU.loadHints().done(function () { renderHints(); });
  }

  function renderHints() {
    const hints = Object.values(ZU.state.hints || {});
    $('#hints-list').html(hints.length ? `<table class="table table-sm">
      <thead><tr><th>screen</th><th>field</th><th>${ZU.escapeHtml(ZU.t('hint'))}</th><th></th></tr></thead>
      <tbody>${hints.map(hint => `<tr>
        <td dir="ltr" class="small">${ZU.escapeHtml(hint.screenKey)}</td>
        <td dir="ltr" class="small">${ZU.escapeHtml(hint.fieldKey)}</td>
        <td>${ZU.escapeHtml(ZU.pick(hint.textAr, hint.textEn))}</td>
        <td><button class="btn btn-sm btn-outline-danger hint-delete" data-id="${hint.id}">×</button></td>
      </tr>`).join('')}</tbody></table>` : `<div class="small-muted">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
  }

  $('#settings-host').on('submit', '#policy-form', function (event) {
    event.preventDefault();
    ZU.api('/api/settings/password-policy', 'PUT', {
      minimumLength: Number($('#pol-min').val()),
      maximumLength: Number($('#pol-max').val()),
      allowSpaces: $('#pol-spaces').is(':checked'),
      requireMixedCharacterClasses: $('#pol-mixed').is(':checked'),
      blockCommonPasswords: $('#pol-common').is(':checked')
    }).done(function () { ZU.toast(ZU.t('saved'), true); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#settings-host').on('submit', '#template-form', function (event) {
    event.preventDefault();
    const file = $('#tpl-file')[0].files[0];
    if (!file) return;
    const data = new FormData();
    data.append('nameAr', $('#tpl-name-ar').val().trim());
    data.append('file', file);
    ZU.apiForm('/api/settings/certificate-templates', 'POST', data)
      .done(function () { ZU.toast(ZU.t('saved'), true); loadSettings(); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#settings-host').on('submit', '#hint-form', function (event) {
    event.preventDefault();
    ZU.api('/api/settings/hints', 'POST', {
      screenKey: $('#hint-screen').val().trim(),
      fieldKey: $('#hint-field').val().trim(),
      textAr: $('#hint-ar').val().trim(),
      textEn: $('#hint-en').val().trim() || null
    }).done(function () {
      ZU.toast(ZU.t('saved'), true);
      $('#hint-form')[0].reset();
      ZU.loadHints().done(renderHints);
    }).fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  $('#settings-host').on('click', '.hint-delete', function () {
    ZU.api(`/api/settings/hints/${$(this).data('id')}`, 'DELETE')
      .done(function () { ZU.loadHints().done(renderHints); })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  /* ------------------------------------------------------------------ university sync */
  $('#run-sync').on('click', function () {
    ZU.api('/api/admin/university-sync', 'POST')
      .done(function (result) {
        ZU.toast(ZU.t('saved'), true);
        $('#sync-output').html(`<div class="alert alert-success">تم التزامن: ${ZU.escapeHtml(JSON.stringify(result))}</div>`);
        loadSyncRuns();
      })
      .fail(function (xhr) { ZU.toast((xhr.responseJSON && xhr.responseJSON.message) || ZU.t('failed'), false); });
  });

  function loadSyncRuns() {
    ZU.api('/api/admin/university-sync/runs').done(function (rows) {
      $('#sync-output').html((rows || []).length ? `<div class="table-responsive"><table class="table table-sm">
        <thead><tr><th>#</th><th>${ZU.escapeHtml(ZU.t('status'))}</th><th>${ZU.escapeHtml(ZU.t('startedAt'))}</th>
          <th>${ZU.escapeHtml(ZU.t('finishedAt'))}</th></tr></thead>
        <tbody>${rows.map(run => `<tr><td>${run.id}</td><td>${ZU.escapeHtml(String(run.status))}</td>
          <td>${ZU.escapeHtml(ZU.fmtDateTime(run.startedAtUtc))}</td>
          <td>${ZU.escapeHtml(ZU.fmtDateTime(run.completedAtUtc || run.finishedAtUtc))}</td></tr>`).join('')}
        </tbody></table></div>` : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
    });
  }

  $('#admin-tabs').on('click', 'button[data-tab=sync]', loadSyncRuns);

  /* ------------------------------------------------------------------ audit */
  function loadAudit() {
    const action = $('#audit-action').val().trim();
    const entity = $('#audit-entity').val().trim();
    ZU.api(`/api/audit?action=${encodeURIComponent(action)}&entityType=${encodeURIComponent(entity)}&pageSize=50`)
      .done(function (page) {
        const rows = page.items || [];
        $('#audit-list').html(rows.length ? `<div class="table-responsive"><table class="table table-sm align-middle">
          <thead><tr><th>${ZU.escapeHtml(ZU.t('time'))}</th><th>${ZU.escapeHtml(ZU.t('user'))}</th>
            <th>${ZU.escapeHtml(ZU.t('action'))}</th><th>${ZU.escapeHtml(ZU.t('entity'))}</th>
            <th>${ZU.escapeHtml(ZU.t('details'))}</th></tr></thead>
          <tbody>${rows.map(row => `<tr>
            <td class="small">${ZU.escapeHtml(ZU.fmtDateTime(row.createdAtUtc))}</td>
            <td class="small">${ZU.escapeHtml(row.actorName || '—')}</td>
            <td class="small" dir="ltr">${ZU.escapeHtml(row.actionKey)}</td>
            <td class="small" dir="ltr">${ZU.escapeHtml(row.entityType)}</td>
            <td class="small">${ZU.escapeHtml(row.reason || row.details || '')}
              <div class="small-muted" dir="ltr">${ZU.escapeHtml(row.ipAddress || '')}</div></td>
          </tr>`).join('')}</tbody></table></div>
          <div class="small-muted">${ZU.escapeHtml(ZU.t('total'))}: ${page.total}</div>`
          : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
      })
      .fail(function () { $('#audit-list').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#audit-search').on('click', loadAudit);

  function stopLive() {
    if (auditTimer) { clearInterval(auditTimer); auditTimer = null; }
    $('#live-toggle').prop('checked', false);
  }

  $('#live-toggle').on('change', function () {
    if (!$(this).is(':checked')) { stopLive(); return; }
    const tick = function () {
      ZU.api('/api/audit/live?take=25').done(function (rows) {
        $('#audit-list').html(`<div class="live-feed">${(rows || []).map(row => `
          <div class="live-row"><span class="live-time">${ZU.escapeHtml(ZU.fmtDateTime(row.createdAtUtc))}</span>
            <span>${ZU.escapeHtml(row.actorName || '—')}</span>
            <code dir="ltr">${ZU.escapeHtml(row.actionKey)}</code>
            <span class="small-muted">${ZU.escapeHtml(row.entityType)}</span></div>`).join('') || ''}</div>`);
      });
    };
    tick();
    auditTimer = setInterval(tick, 8000);
  });

  /* ------------------------------------------------------------------ vault */
  function loadVault() {
    const host = $('#vault-admin');
    host.html(`<div class="panel-heading"><div><h2>${ZU.escapeHtml(ZU.t('documentVault'))}</h2>
      <p>وثائق الكتب الرسمية والسير الذاتية وطلبات الإلغاء.</p></div></div>
      <div class="d-flex gap-2 mb-3">
        <input id="av-query" class="form-control" placeholder="بحث">
        <button class="btn btn-outline-primary" id="av-search" type="button">${ZU.escapeHtml(ZU.t('search'))}</button>
      </div><div id="av-rows"></div>`);
    loadVaultRows('');
  }

  function loadVaultRows(query) {
    ZU.api('/api/official-documents?query=' + encodeURIComponent(query || ''))
      .done(function (page) {
        const rows = page.items || [];
        $('#av-rows').html(rows.length ? `<div class="table-responsive"><table class="table table-sm">
          <thead><tr><th>${ZU.escapeHtml(ZU.t('file'))}</th><th>${ZU.escapeHtml(ZU.t('kind'))}</th>
            <th>${ZU.escapeHtml(ZU.t('uploadedAt'))}</th><th></th></tr></thead>
          <tbody>${rows.map(row => `<tr>
            <td>${ZU.escapeHtml(row.originalName)}<div class="small-muted" dir="ltr">${ZU.escapeHtml(row.entityType)}</div></td>
            <td class="small">${ZU.escapeHtml(row.kind)}</td>
            <td class="small">${ZU.escapeHtml(ZU.fmtDateTime(row.uploadedAtUtc))}</td>
            <td><a class="btn btn-sm btn-outline-primary" target="_blank" href="/api/official-documents/${row.id}">${ZU.escapeHtml(ZU.t('download'))}</a></td>
          </tr>`).join('')}</tbody></table></div>` : `<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`);
      })
      .fail(function () { $('#av-rows').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); });
  }

  $('#vault-admin').on('click', '#av-search', function () { loadVaultRows($('#av-query').val().trim()); });
})();
