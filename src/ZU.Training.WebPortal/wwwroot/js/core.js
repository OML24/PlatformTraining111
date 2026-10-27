/* نواة الواجهة: الاتصال بالـAPI، الجلسة، اللغة، القوائم، التلميحات، ومنتقي التاريخ/الوقت. */
window.ZU = (function () {
  'use strict';

  const state = {
    apiBase: '',
    lang: localStorage.getItem('zu.lang') || 'ar',
    account: null,
    hints: {},
    labels: window.ZU_LABELS || { ar: {}, en: {} }
  };

  /* ------------------------------------------------------------------ utilities */
  function escapeHtml(value) {
    return String(value === null || value === undefined ? '' : value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function t(key) {
    const table = state.labels[state.lang] || {};
    const fallback = state.labels.ar || {};
    return table[key] || fallback[key] || key;
  }

  function pick(ar, en) {
    if (state.lang === 'en' && en) return en;
    return ar || en || '';
  }

  function fmtDate(value) {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    return date.toLocaleDateString(state.lang === 'ar' ? 'ar-JO' : 'en-GB',
      { year: 'numeric', month: '2-digit', day: '2-digit' });
  }

  function fmtDateTime(value) {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    return date.toLocaleString(state.lang === 'ar' ? 'ar-JO' : 'en-GB',
      { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
  }

  function toIso(localValue) {
    if (!localValue) return null;
    const normalized = localValue.replace(' ', 'T');
    const date = new Date(normalized.length === 16 ? normalized + ':00' : normalized);
    if (Number.isNaN(date.getTime())) return null;
    return date.toISOString();
  }

  function toLocalInput(value) {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    const pad = n => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  /* ------------------------------------------------------------------ API */
  function url(path) { return state.apiBase + path; }

  function handleFailure(xhr) {
    if (xhr.status === 401) {
      if (!location.pathname.endsWith('/index.html') && location.pathname !== '/') {
        location.href = '/index.html?next=' + encodeURIComponent(location.pathname);
      }
      return;
    }
    const message = (xhr.responseJSON && (xhr.responseJSON.message || xhr.responseJSON.title)) ||
      (xhr.status === 403 ? 'ليست لديك صلاحية لهذا الإجراء.' : t('failed'));
    toast(message, false);
  }

  function api(path, method, data) {
    return $.ajax({
      url: url(path), method: method || 'GET',
      contentType: 'application/json; charset=utf-8',
      data: data !== undefined ? JSON.stringify(data) : undefined,
      cache: false, timeout: 30000
    }).fail(function (xhr) {
      if (xhr.status !== 401) handleFailure(xhr);
    });
  }

  function apiForm(path, method, formData) {
    return $.ajax({
      url: url(path), method: method || 'POST', data: formData, processData: false, contentType: false,
      cache: false, timeout: 120000
    }).fail(function (xhr) { if (xhr.status !== 401) handleFailure(xhr); });
  }

  /* ------------------------------------------------------------------ auth */
  function can(permission) {
    return !!(state.account && (state.account.permissions || []).includes(permission));
  }

  function hasRole(role) {
    return !!(state.account && (state.account.roles || []).includes(role));
  }

  function homeFor(account) {
    if (!account) return '/index.html';
    if (account.mustChangePassword) return '/change-password.html';
    const roles = account.roles || [];
    if (roles.includes('ADMIN')) return '/admin.html';
    if (roles.includes('QA')) return '/quality.html';
    if (roles.includes('DEAN')) return '/dean.html';
    if (roles.includes('INST') || roles.includes('EXTERNAL_INSTRUCTOR')) return '/instructor.html';
    return '/member.html';
  }

  /* ------------------------------------------------------------------ notifications */
  function loadNotifications() {
    if (!state.account) return;
    api('/api/notifications?take=20').done(function (result) {
      const badge = $('#notification-count');
      if (!badge.length) return;
      badge.text(result.unread || '').toggleClass('d-none', !result.unread);
      const host = $('#notification-list');
      if (!host.length) return;
      if (!result.items || !result.items.length) {
        host.html(`<div class="empty-state">${escapeHtml(t('notificationsEmpty'))}</div>`);
        return;
      }
      host.html(result.items.map(item => `
        <div class="border rounded-3 p-3 mb-2 ${item.isRead ? '' : 'bg-light'}">
          <div class="d-flex justify-content-between gap-2">
            <strong>${escapeHtml(pick(item.titleAr, item.titleEn))}</strong>
            <span class="small-muted">${escapeHtml(fmtDateTime(item.createdAtUtc))}</span>
          </div>
          <div class="small-muted">${escapeHtml(pick(item.bodyAr, item.bodyEn))}</div>
          ${item.linkUrl ? `<a class="small" href="${escapeHtml(item.linkUrl)}">${escapeHtml(t('view'))}</a>` : ''}
        </div>`).join(''));
    });
  }

  /* ------------------------------------------------------------------ navigation */
  const navigation = [
    { section: 'workspace', items: [
      { page: 'member', href: '/member.html', label: 'myCourses', permission: 'page.member' },
      { page: 'instructor', href: '/instructor.html', label: 'myCoursesAsInstructor', permission: 'page.instructor' },
      { page: 'dean', href: '/dean.html', label: 'dean', permission: 'page.dean' },
      { page: 'quality', href: '/quality.html', label: 'quality', permission: 'page.quality' },
      { page: 'admin', href: '/admin.html', label: 'admin', permission: 'page.admin' }
    ] },
    { section: 'account', items: [
      { page: 'profile', href: '/member-profile.html', label: 'profile', permission: 'page.member' },
      { page: 'directory', href: '/directory.html', label: 'members', permission: 'members.directory.view' },
      { page: 'statistics', href: '/statistics.html', label: 'statistics', permission: 'courses.statistics.view' }
    ] }
  ];

  function renderNav(activePage) {
    const host = $('#app-nav');
    if (!host.length) return;
    const sections = [];
    navigation.forEach(group => {
      const items = group.items.filter(item => can(item.permission));
      if (!items.length) return;
      sections.push(`<div class="sidebar-section">${escapeHtml(t(group.section))}</div>` +
        items.map(item => `<a class="nav-link ${item.page === activePage ? 'active' : ''}"
          href="${item.href}">${escapeHtml(t(item.label))}</a>`).join(''));
    });
    host.html(sections.join(''));
  }

  /* ------------------------------------------------------------------ toasts */
  function toast(message, ok) {
    if (!message) return;
    let host = $('.toast-host');
    if (!host.length) host = $('<div class="toast-host"></div>').appendTo('body');
    const item = $(`<div class="zu-toast ${ok === true ? 'ok' : ok === false ? 'err' : ''}"></div>`).text(message);
    host.append(item);
    setTimeout(() => item.fadeOut(220, () => item.remove()), ok === false ? 6200 : 3200);
  }

  function alertBox(selector, message, success) {
    const host = $(selector);
    if (!host.length) return;
    host.removeClass('d-none alert-danger alert-success alert-warning')
      .addClass(success === false ? 'alert-danger' : success === 'warn' ? 'alert-warning' : 'alert-success')
      .text(message || '');
  }

  function clearAlert(selector) { $(selector).addClass('d-none').text(''); }

  /* ------------------------------------------------------------------ language */
  function applyLanguage() {
    const arabic = state.lang === 'ar';
    document.documentElement.lang = state.lang;
    document.documentElement.dir = arabic ? 'rtl' : 'ltr';
    $('#bootstrap-style').attr('href', arabic ? '/vendor/bootstrap.rtl.min.css' : '/vendor/bootstrap.min.css');
    $('[data-i18n]').each(function () { $(this).text(t($(this).data('i18n'))); });
    $('[data-i18n-placeholder]').each(function () {
      $(this).attr('placeholder', t($(this).data('i18n-placeholder')));
    });
    $('#language-toggle').text(t('language'));
    if (state.account) {
      $('#account-name').text(pick(state.account.nameAr, state.account.nameEn));
      $('#account-login').text(state.account.loginName || '');
    }
    $(document).trigger('zu:language');
  }

  function setLanguage(lang) {
    if (lang !== 'ar' && lang !== 'en') return;
    state.lang = lang;
    localStorage.setItem('zu.lang', lang);
    applyLanguage();
    if (state.account) api('/api/auth/language', 'PUT', { language: lang });
  }

  /* ------------------------------------------------------------------ hints */
  function loadHints() {
    if (!state.account) return $.Deferred().resolve().promise();
    return api('/api/settings/hints').done(function (rows) {
      state.hints = {};
      (rows || []).forEach(row => { state.hints[row.screenKey + '.' + row.fieldKey] = row; });
    });
  }

  function attachHints() {
    $(document).on('focusin', '[data-hint]', function () {
      const hint = state.hints[$(this).data('hint')];
      if (!hint) return;
      let node = $(this).next('.field-hint');
      if (!node.length) node = $('<div class="field-hint"></div>').insertAfter($(this));
      node.text(pick(hint.textAr, hint.textEn));
    });
  }

  function renderHints() {
    const hints = Object.values(state.hints || {});
    $('#hints-list').html(hints.length ? `<table class="table table-sm">
      <thead><tr><th>screen</th><th>field</th><th>${escapeHtml(t('hint'))}</th><th></th></tr></thead>
      <tbody>${hints.map(hint => `<tr>
        <td dir="ltr" class="small">${escapeHtml(hint.screenKey)}</td>
        <td dir="ltr" class="small">${escapeHtml(hint.fieldKey)}</td>
        <td>${escapeHtml(pick(hint.textAr, hint.textEn))}</td>
        <td><button class="btn btn-sm btn-outline-danger hint-delete" data-id="${hint.id}">×</button></td>
      </tr>`).join('')}</tbody></table>` : `<div class="small-muted">${escapeHtml(t('noRows'))}</div>`);
  }

  /* ------------------------------------------------------------------ date/time picker */
  const pad = value => String(value).padStart(2, '0');
  const monthNames = {
    ar: ['كانون الثاني', 'شباط', 'آذار', 'نيسان', 'أيار', 'حزيران', 'تموز', 'آب', 'أيلول', 'تشرين الأول', 'تشرين الثاني', 'كانون الأول'],
    en: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
  };
  const dayNames = { ar: ['ح', 'ن', 'ث', 'ر', 'خ', 'ج', 'س'], en: ['S', 'M', 'T', 'W', 'T', 'F', 'S'] };

  function datePicker(input, options) {
    const field = $(input);
    if (field.data('zu-dtp')) return;
    field.data('zu-dtp', true).attr('readonly', 'readonly').addClass('zu-dtp-input');
    const withTime = !options || options.time !== false;
    const wrap = $('<div class="zu-dtp"></div>').insertBefore(field);
    wrap.append(field);
    let selected = field.val() ? new Date(field.val().replace(' ', 'T')) : null;
    if (selected && Number.isNaN(selected.getTime())) selected = null;
    let cursor = selected ? new Date(selected) : new Date();

    function commit() {
      field.val(`${selected.getFullYear()}-${pad(selected.getMonth() + 1)}-${pad(selected.getDate())}` +
        (withTime ? ` ${pad(selected.getHours())}:${pad(selected.getMinutes())}` : ''));
      field.trigger('change');
      $('.zu-dtp-panel').remove();
      field.removeClass('invalid');
    }

    function render() {
      $('.zu-dtp-panel').remove();
      const year = cursor.getFullYear();
      const month = cursor.getMonth();
      const startDay = new Date(year, month, 1).getDay();
      const daysInMonth = new Date(year, month + 1, 0).getDate();
      const today = new Date();
      let cells = '';
      for (let i = 0; i < startDay; i++) cells += '<span class="empty"></span>';
      for (let day = 1; day <= daysInMonth; day++) {
        const isSelected = selected && selected.getFullYear() === year &&
          selected.getMonth() === month && selected.getDate() === day;
        const isToday = today.getFullYear() === year && today.getMonth() === month && today.getDate() === day;
        cells += `<button type="button" class="day ${isSelected ? 'selected' : ''} ${isToday ? 'today' : ''}"
          data-day="${day}">${day}</button>`;
      }
      const panel = $(`<div class="zu-dtp-panel">
        <div class="zu-dtp-head">
          <button type="button" data-nav="-1" aria-label="previous">‹</button>
          <span>${escapeHtml(monthNames[state.lang === 'ar' ? 'ar' : 'en'][month])} ${year}</span>
          <button type="button" data-nav="1" aria-label="next">›</button>
        </div>
        <div class="zu-dtp-grid">
          ${dayNames[state.lang === 'ar' ? 'ar' : 'en'].map(d => `<span class="dow">${d}</span>`).join('')}
          ${cells}
        </div>
        ${withTime ? `<div class="zu-dtp-time">
            <span class="small-muted">${escapeHtml(t('hour'))}</span>
            <select class="zu-hour">${Array.from({ length: 24 }, (_, i) => `<option value="${i}">${pad(i)}</option>`).join('')}</select>
            <span class="small-muted">${escapeHtml(t('minute'))}</span>
            <select class="zu-minute">${[0, 15, 30, 45].map(m => `<option value="${m}">${pad(m)}</option>`).join('')}</select>
            <button type="button" class="btn btn-sm btn-primary ms-auto zu-apply">${escapeHtml(t('apply'))}</button>
          </div>` : ''}
      </div>`);
      wrap.append(panel);
      if (selected) {
        panel.find('.zu-hour').val(selected.getHours());
        panel.find('.zu-minute').val(selected.getMinutes() - (selected.getMinutes() % 15));
      }
      panel.on('click', event => event.stopPropagation());
      panel.on('click', '[data-nav]', function () {
        cursor = new Date(year, month + Number($(this).data('nav')), 1);
        render();
      });
      panel.on('click', '[data-day]', function () {
        const day = Number($(this).data('day'));
        selected = new Date(year, month, day, selected ? selected.getHours() : 9,
          selected ? selected.getMinutes() : 0);
        cursor = new Date(selected);
        withTime ? render() : commit();
      });
      if (withTime) panel.on('click', '.zu-apply', function () {
        if (!selected) selected = new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate(), 9, 0);
        selected.setHours(Number(panel.find('.zu-hour').val() || 9));
        selected.setMinutes(Number(panel.find('.zu-minute').val() || 0));
        commit();
      });
    }

    field.on('click focus', function (event) {
      event.stopPropagation();
      cursor = selected ? new Date(selected) : new Date();
      render();
    });
    $(document).on('click.zuDtp', () => $('.zu-dtp-panel').remove());
  }

  function initDatePickers(scope) {
    (scope || document).querySelectorAll('[data-datetime]').forEach(input => datePicker(input, { time: true }));
    (scope || document).querySelectorAll('[data-date]').forEach(input => datePicker(input, { time: false }));
  }

  /* ------------------------------------------------------------------ header */
  function renderHeader() {
    const host = $('#app-header');
    if (!host.length) return;
    const account = state.account;
    const consoles = [
      { permission: 'page.member', href: '/member.html', label: 'myCourses' },
      { permission: 'page.instructor', href: '/instructor.html', label: 'instructorConsole' },
      { permission: 'page.dean', href: '/dean.html', label: 'deanConsole' },
      { permission: 'page.quality', href: '/quality.html', label: 'qualityConsole' },
      { permission: 'page.admin', href: '/admin.html', label: 'adminConsole' }
    ].filter(item => can(item.permission));

    host.html(`
      <div class="container-xl d-flex align-items-center justify-content-between gap-3 flex-wrap">
        <a class="brand text-decoration-none" href="${escapeHtml(homeFor(account))}">
          <img src="/logo.png" alt="" width="42" height="42">
          <span><strong>${escapeHtml(t('brand'))}</strong><span>${escapeHtml(t('platform'))}</span></span>
        </a>
        <div class="header-actions">
          ${consoles.length > 1 ? `<div class="dropdown">
            <button class="btn btn-sm btn-outline-primary dropdown-toggle" data-bs-toggle="dropdown" type="button">
              ${escapeHtml(t('switchConsole'))}</button>
            <ul class="dropdown-menu">${consoles.map(item =>
              `<li><a class="dropdown-item" href="${item.href}">${escapeHtml(t(item.label))}</a></li>`).join('')}</ul>
          </div>` : ''}
          ${account ? `<div class="dropdown">
            <button class="btn btn-sm btn-outline-primary position-relative" data-bs-toggle="dropdown" type="button">
              ${escapeHtml(t('notifications'))}
              <span id="notification-count" class="badge badge-danger d-none"></span>
            </button>
            <div class="dropdown-menu dropdown-menu-end p-3" style="min-width:340px;max-width:390px">
              <div class="d-flex justify-content-between align-items-center mb-2">
                <strong>${escapeHtml(t('notifications'))}</strong>
                <button type="button" class="btn btn-sm btn-link p-0" id="mark-all-read">${escapeHtml(t('markAllRead'))}</button>
              </div>
              <div id="notification-list" style="max-height:360px;overflow:auto"></div>
            </div>
          </div>` : ''}
          <button type="button" id="language-toggle" class="btn btn-sm btn-outline-primary">${escapeHtml(t('language'))}</button>
          ${account ? `<span class="small-muted d-none d-lg-inline">
              <strong id="account-name">${escapeHtml(pick(account.nameAr, account.nameEn))}</strong>
              <span id="account-login">${escapeHtml(account.loginName || '')}</span>
            </span>
            ${can('page.member') ? `<a class="btn btn-sm btn-outline-primary" href="/member-profile.html">${escapeHtml(t('profile'))}</a>` : ''}
            <button type="button" id="logout-button" class="btn btn-sm btn-outline-secondary">${escapeHtml(t('logout'))}</button>`
            : `<a class="btn btn-sm btn-outline-primary" href="/index.html">${escapeHtml(t('login'))}</a>`}
        </div>
      </div>`);

    $('#language-toggle').on('click', () => setLanguage(state.lang === 'ar' ? 'en' : 'ar'));
    $('#logout-button').on('click', function () {
      api('/api/auth/logout', 'POST').always(function () { location.href = '/index.html'; });
    });
    $('#mark-all-read').on('click', function () {
      api('/api/notifications/read-all', 'POST').done(function () {
        loadNotifications();
        $('#notification-count').addClass('d-none');
      });
    });
    loadNotifications();
  }

  /* ------------------------------------------------------------------ boot */
  function boot(options) {
    const settings = options || {};
    applyLanguage();
    attachHints();

    return $.getJSON('/config/api.json').then(function (config) {
      state.apiBase = (config.baseUrl || '').replace(/\/$/, '');
    }).fail(function () {
      state.apiBase = '';
    }).then(function () {
      if (settings.public) {
        state.account = null;
        return null;
      }
      return new Promise(function (resolve) {
        api('/api/auth/me').done(function (account) {
          state.account = account;
          if (account.preferredLanguage && account.preferredLanguage !== state.lang) {
            state.lang = account.preferredLanguage;
          }
          applyLanguage();
          resolve(account);
        }).fail(function (xhr) {
          if (xhr.status === 401) {
            location.href = '/index.html?next=' + encodeURIComponent(location.pathname);
            return;
          }
          resolve(null);
        });
      });
    }).then(function (account) {
      initDatePickers();
      if (account && account.mustChangePassword && settings.allowPasswordChange !== true) {
        location.href = '/change-password.html';
        return null;
      }
      renderHeader();
      if (settings.page) renderNav(settings.page);
      $('[data-require-permission]').each(function () {
        if (!can($(this).data('require-permission'))) $(this).addClass('d-none');
      });
      loadHints().done(() => { if ($('#hints-list').length) renderHints(); });
      if (typeof settings.onReady === 'function') settings.onReady(account);
      return account;
    });
  }

  function requirePermission(permission) {
    if (can(permission)) return true;
    toast('ليست لديك صلاحية لعرض هذه الصفحة.', false);
    setTimeout(() => { location.href = homeFor(state.account); }, 900);
    return false;
  }

  return {
    state, t, pick, api, apiForm, can, hasRole, homeFor, boot, renderNav, toast, alertBox, clearAlert,
    setLanguage, applyLanguage, loadHints, renderHints, datePicker, initDatePickers, fmtDate, fmtDateTime,
    toIso, toLocalInput, escapeHtml, requirePermission, loadNotifications, renderHeader
  };
})();
