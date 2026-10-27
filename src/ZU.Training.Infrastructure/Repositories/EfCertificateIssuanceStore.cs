using Microsoft.EntityFrameworkCore;
using ZU.Training.Application.Training;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Settings;
using ZU.Training.Domain.Training;
using ZU.Training.Infrastructure.Data;

namespace ZU.Training.Infrastructure.Repositories;

public sealed class EfCertificateIssuanceStore(TrainingDbContext db) : ICertificateIssuanceStore
{
    public async Task<IReadOnlyList<Course>> CoursesAsync(CancellationToken ct)
        => await db.Courses.AsNoTracking().Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    public Task<Course?> CourseAsync(Guid id, CancellationToken ct)
        => db.Courses.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<UserAccount?> UserAsync(Guid id, CancellationToken ct)
        => db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<Faculty?> FacultyAsync(Guid id, CancellationToken ct)
        => db.Faculties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CourseEnrollment>> EnrollmentsAsync(Guid courseId, CancellationToken ct)
        => await db.CourseEnrollments.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive).ToListAsync(ct);

    public Task<CourseEnrollment?> EnrollmentAsync(Guid courseId, Guid userId, CancellationToken ct)
        => db.CourseEnrollments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CourseId == courseId && x.UserId == userId && x.IsActive, ct);

    public Task<CourseEnrollment?> EnrollmentByIdAsync(Guid id, CancellationToken ct)
        => db.CourseEnrollments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CourseEnrollment>> EnrollmentsForUserAsync(Guid userId, CancellationToken ct)
        => await db.CourseEnrollments.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive).ToListAsync(ct);

    public Task<int> LectureCountAsync(Guid courseId, CancellationToken ct)
        => db.Lectures.CountAsync(x => x.CourseId == courseId && x.IsActive, ct);

    public Task<int> AttendanceCountAsync(Guid courseId, Guid enrollmentId, CancellationToken ct)
        => (from record in db.AttendanceRecords
            join lecture in db.Lectures on record.LectureId equals lecture.Id
            where lecture.CourseId == courseId && lecture.IsActive && record.IsActive &&
                  record.EnrollmentId == enrollmentId && record.IsPresent
            select record.Id).CountAsync(ct);

    public async Task<ExamOutcome> ExamOutcomeAsync(Guid courseId, Guid enrollmentId, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive).Select(x => x.Id).FirstOrDefaultAsync(ct);
        if (exam == Guid.Empty) return ExamOutcome.NotRecorded;
        var outcome = await db.ExamResults.AsNoTracking()
            .Where(x => x.ExamId == exam && x.EnrollmentId == enrollmentId && x.IsActive)
            .Select(x => (ExamOutcome?)x.Outcome).FirstOrDefaultAsync(ct);
        return outcome ?? ExamOutcome.NotRecorded;
    }

    public Task<bool> HasEvaluationAsync(Guid? versionId, Guid enrollmentId, CancellationToken ct)
        => versionId.HasValue
            ? db.EvaluationResponses.AnyAsync(x => x.FormVersionId == versionId.Value &&
                x.EnrollmentId == enrollmentId && x.IsActive, ct)
            : db.EvaluationResponses.AnyAsync(x => x.EnrollmentId == enrollmentId && x.IsActive, ct);

    public Task<bool> HasApprovedImpactAsync(Guid enrollmentId, CancellationToken ct)
        => db.ImpactSubmissions.AnyAsync(x => x.EnrollmentId == enrollmentId && x.IsActive &&
            x.Status == ImpactStatus.Approved, ct);

    public Task<Certificate?> CertificateAsync(Guid enrollmentId, CancellationToken ct)
        => db.Certificates.Where(x => x.EnrollmentId == enrollmentId && x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);

    public Task<Certificate?> CertificateByIdAsync(Guid id, CancellationToken ct)
        => db.Certificates.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

    public async Task<IReadOnlyList<Certificate>> CertificatesForCourseAsync(Guid courseId, CancellationToken ct)
        => await db.Certificates.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive).ToListAsync(ct);

    public async Task<IReadOnlyList<Certificate>> CertificatesForUserAsync(Guid userId, CancellationToken ct)
        => await (from certificate in db.Certificates.AsNoTracking()
                  join enrollment in db.CourseEnrollments.AsNoTracking() on certificate.EnrollmentId equals enrollment.Id
                  where enrollment.UserId == userId && certificate.IsActive
                  orderby certificate.IssuedAtUtc descending
                  select certificate).ToListAsync(ct);

    public Task<CertificateTemplate?> CurrentTemplateAsync(CancellationToken ct)
        => db.CertificateTemplates.AsNoTracking().Where(x => x.IsActive)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);

    public Task<CertificateTemplate?> TemplateAsync(Guid id, CancellationToken ct)
        => db.CertificateTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<int> NextCertificateNumberAsync(string prefix, CancellationToken ct)
    {
        var numbers = await db.Certificates.AsNoTracking()
            .Where(x => x.ReferenceNumber.StartsWith(prefix))
            .Select(x => x.ReferenceNumber).ToListAsync(ct);
        var max = 0;
        foreach (var value in numbers)
        {
            var suffix = value[prefix.Length..];
            if (int.TryParse(suffix, out var parsed) && parsed > max) max = parsed;
        }
        return max;
    }

    public void AddCertificate(Certificate certificate) => db.Certificates.Add(certificate);
    public void AddAudit(AuditEvent audit) => db.AuditEvents.Add(audit);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
