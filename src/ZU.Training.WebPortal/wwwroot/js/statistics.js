/* لوحة الإحصاءات: مؤشرات عامة وتفصيلية. */
(function () {
  'use strict';

  ZU.boot({ onReady: function () {
    if (!ZU.requirePermission('courses.statistics.view') && !ZU.can('admin.statistics.view')) {
      ZU.requirePermission('page.dean');
      return;
    }
    ZU.api('/api/statistics/faculties').done(function (rows) {
      $('#st-faculty').append((rows || []).map(row =>
        `<option value="${row.facultyId || ''}">${ZU.escapeHtml(ZU.pick(row.nameAr, row.nameEn))}</option>`).join(''));
      renderFaculties(rows);
    });
    load();
  } });

  $('#st-refresh').on('click', load);
  $('#st-faculty').on('change', load);

  function load() {
    const facultyId = $('#st-faculty').val();
    const query = facultyId ? '?facultyId=' + encodeURIComponent(facultyId) : '';
    ZU.api('/api/statistics/overview' + query).done(function (overview) {
      const cards = [
        ['courses', overview.courses], ['activeCourses', overview.activeCourses],
        ['endedCourses', overview.endedCourses], ['cancelledCourses', overview.cancelledCourses],
        ['activeMembers', overview.activeMembers], ['enrollments', overview.enrollments],
        ['validCertificates', overview.validCertificates], ['revokedCertificates', overview.revokedCertificates],
        ['examPassed', overview.examPassed], ['examFailed', overview.examFailed],
        ['averageAttendance', Math.round(overview.averageAttendancePercent) + '%']
      ];
      $('#st-overview').html(cards.map(([key, value]) => `
        <div class="stat-card"><span class="stat-value">${ZU.escapeHtml(String(value))}</span>
          <span class="stat-label">${ZU.escapeHtml(ZU.t(key))}</span></div>`).join(''));
    });

    ZU.api('/api/statistics/tracks').done(renderTracks);
    ZU.api('/api/statistics/courses' + query).done(renderCourses);
    ZU.api('/api/statistics/evaluation').done(renderEvaluation);
  }

  function renderFaculties(rows) {
    $('#st-faculties').html(`<div class="table-responsive"><table class="table table-sm">
      <thead><tr><th>${ZU.escapeHtml(ZU.t('faculty'))}</th><th>${ZU.escapeHtml(ZU.t('courses'))}</th>
        <th>${ZU.escapeHtml(ZU.t('enrollments'))}</th><th>${ZU.escapeHtml(ZU.t('certificates'))}</th>
        <th>${ZU.escapeHtml(ZU.t('attendance'))}</th></tr></thead>
      <tbody>${(rows || []).map(row => `<tr>
        <td>${ZU.escapeHtml(ZU.pick(row.nameAr, row.nameEn))}</td>
        <td>${row.courses}</td><td>${row.enrollments}</td><td>${row.certificates}</td>
        <td>${Math.round(row.averageAttendancePercent)}%</td>
      </tr>`).join('')}</tbody></table></div>`);
  }

  function renderTracks(rows) {
    $('#st-tracks').html(`<div class="table-responsive"><table class="table table-sm">
      <thead><tr><th>${ZU.escapeHtml(ZU.t('track'))}</th><th>${ZU.escapeHtml(ZU.t('courses'))}</th>
        <th>${ZU.escapeHtml(ZU.t('enrollments'))}</th><th>${ZU.escapeHtml(ZU.t('certificates'))}</th></tr></thead>
      <tbody>${(rows || []).map(row => `<tr>
        <td>${ZU.escapeHtml(ZU.pick(row.nameAr, row.nameEn))}</td><td>${row.courses}</td>
        <td>${row.enrollments}</td><td>${row.certificates}</td></tr>`).join('')}</tbody></table></div>`);
  }

  function renderCourses(rows) {
    $('#st-courses').html(`<div class="table-responsive"><table class="table table-sm align-middle">
      <thead><tr><th>${ZU.escapeHtml(ZU.t('courses'))}</th><th>${ZU.escapeHtml(ZU.t('lectures'))}</th>
        <th>${ZU.escapeHtml(ZU.t('enrollments'))}</th><th>${ZU.escapeHtml(ZU.t('attendance'))}</th>
        <th>${ZU.escapeHtml(ZU.t('examPassed'))}</th><th>${ZU.escapeHtml(ZU.t('examFailed'))}</th>
        <th>${ZU.escapeHtml(ZU.t('certificates'))}</th><th>${ZU.escapeHtml(ZU.t('evaluation'))}</th></tr></thead>
      <tbody>${(rows || []).map(row => `<tr>
        <td>${ZU.escapeHtml(ZU.pick(row.titleAr, row.titleEn))}</td><td>${row.lectures}</td>
        <td>${row.enrollments}</td><td>${Math.round(row.averageAttendancePercent)}%</td>
        <td>${row.examPassed}</td><td>${row.examFailed}</td><td>${row.certificates}</td>
        <td>${row.evaluationResponses}</td></tr>`).join('')}</tbody></table></div>`);
  }

  function renderEvaluation(report) {
    if (!report || !report.questions || !report.questions.length) {
      $('#st-evaluation').html(`<div class="empty-state">${ZU.escapeHtml(ZU.t('noRows'))}</div>`); return;
    }
    $('#st-evaluation').html(`
      <div class="d-flex justify-content-between align-items-center mb-2">
        <span class="small-muted">${ZU.escapeHtml(ZU.t('responses'))}: ${report.responses}</span>
        <span class="badge-soft badge-ok">${ZU.escapeHtml(ZU.t('satisfactionIndex'))}: ${Math.round(report.satisfactionIndex)}%</span>
      </div>
      ${report.questions.map(question => `
        <div class="border rounded-3 p-3 mb-2">
          <strong>${ZU.escapeHtml(ZU.pick(question.textAr, question.textEn))}</strong>
          <div class="small-muted mb-1">${ZU.escapeHtml(ZU.t('responses'))}: ${question.answers}</div>
          ${question.options.map(option => `
            <div class="d-flex align-items-center gap-2 mb-1">
              <span style="min-width:150px">${ZU.escapeHtml(ZU.pick(option.textAr, option.textEn))}</span>
              <div class="progress-thin flex-grow-1" style="max-width:260px"><span style="width:${option.percent}%"></span></div>
              <span class="small-muted">${option.count} (${Math.round(option.percent)}%)</span>
            </div>`).join('')}
        </div>`).join('')}`);
  }
})();
