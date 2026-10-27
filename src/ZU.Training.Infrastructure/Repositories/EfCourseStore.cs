using Microsoft.EntityFrameworkCore;
using ZU.Training.Application.Training;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;
using ZU.Training.Infrastructure.Data;

namespace ZU.Training.Infrastructure.Repositories;

public sealed class EfCourseStore(TrainingDbContext db) : ICourseStore
{
    public Task<UserAccount?> UserAsync(Guid id, CancellationToken ct)
        => db.UserAccounts.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<Faculty>> FacultiesAsync(CancellationToken ct)
        => await db.Faculties.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.NameAr).ToListAsync(ct);
    public async Task<IReadOnlyList<UserAccount>> SearchUsersAsync(string query, Guid? facultyId, CancellationToken ct)
        => await db.UserAccounts.AsNoTracking().Where(x => x.IsActive && !x.IsExternalTrainer &&
            (!facultyId.HasValue || x.FacultyId == facultyId) &&
            (x.NameAr.Contains(query) || x.LoginName.Contains(query)))
            .OrderBy(x => x.NameAr).Take(30).ToListAsync(ct);
    public Task<bool> TrackExistsAsync(Guid id, CancellationToken ct)
        => db.TrainingTracks.AnyAsync(x => x.Id == id && x.IsActive, ct);
    public async Task<IReadOnlyList<TrainingTrack>> TracksAsync(CancellationToken ct)
        => await db.TrainingTracks.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.NameAr).ToListAsync(ct);
    public Task<EvaluationFormVersion?> LatestEvaluationVersionAsync(CancellationToken ct)
        => db.EvaluationFormVersions.OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<Course>> CoursesAsync(CancellationToken ct)
        => await db.Courses.AsNoTracking().Where(x => x.IsActive).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task<Course?> CourseAsync(Guid id, CancellationToken ct)
        => db.Courses.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
    public Task<Lecture?> LectureAsync(Guid id, CancellationToken ct)
        => db.Lectures.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
    public async Task<IReadOnlyList<Lecture>> LecturesAsync(Guid courseId, CancellationToken ct)
        => await db.Lectures.Where(x => x.CourseId == courseId && x.IsActive).OrderBy(x => x.SequenceNumber).ToListAsync(ct);
    public Task<CourseEnrollment?> EnrollmentAsync(Guid courseId, Guid userId, CancellationToken ct)
        => db.CourseEnrollments.SingleOrDefaultAsync(x => x.CourseId == courseId && x.UserId == userId, ct);
    public Task<CourseEnrollment?> EnrollmentByIdAsync(Guid id, CancellationToken ct)
        => db.CourseEnrollments.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<CourseEnrollment>> EnrollmentsAsync(Guid courseId, CancellationToken ct)
        => await db.CourseEnrollments.Where(x => x.CourseId == courseId && x.IsActive).ToListAsync(ct);
    public Task<AttendanceRecord?> AttendanceAsync(Guid lectureId, Guid enrollmentId, CancellationToken ct)
        => db.AttendanceRecords.SingleOrDefaultAsync(x => x.LectureId == lectureId && x.EnrollmentId == enrollmentId, ct);
    public async Task<IReadOnlyList<AttendanceRecord>> AttendancesAsync(Guid courseId, CancellationToken ct)
        => await (from record in db.AttendanceRecords
                  join lecture in db.Lectures on record.LectureId equals lecture.Id
                  where lecture.CourseId == courseId && lecture.IsActive && record.IsActive
                  select record).ToListAsync(ct);
    public Task<Exam?> ExamAsync(Guid courseId, CancellationToken ct)
        => db.Exams.SingleOrDefaultAsync(x => x.CourseId == courseId && x.IsActive, ct);
    public Task<ExamResult?> ExamResultAsync(Guid examId, Guid enrollmentId, CancellationToken ct)
        => db.ExamResults.SingleOrDefaultAsync(x => x.ExamId == examId && x.EnrollmentId == enrollmentId && x.IsActive, ct);
    public async Task<IReadOnlyList<ExamResult>> ExamResultsAsync(Guid examId, CancellationToken ct)
        => await db.ExamResults.Where(x => x.ExamId == examId && x.IsActive).ToListAsync(ct);
    public void AddCourse(Course x) => db.Courses.Add(x);
    public void AddReviewDecision(CourseReviewDecision x) => db.CourseReviewDecisions.Add(x);
    public void AddLecture(Lecture x) => db.Lectures.Add(x);
    public void AddEnrollment(CourseEnrollment x) => db.CourseEnrollments.Add(x);
    public void AddAttendance(AttendanceRecord x) => db.AttendanceRecords.Add(x);
    public void AddExam(Exam x) => db.Exams.Add(x);
    public void AddExamResult(ExamResult x) => db.ExamResults.Add(x);
    public async Task<IReadOnlyList<LectureMaterial>> MaterialsAsync(Guid lectureId, CancellationToken ct)
        => await db.LectureMaterials.AsNoTracking()
            .Where(x => x.LectureId == lectureId && x.IsActive)
            .OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task<LectureMaterial?> MaterialAsync(Guid id, CancellationToken ct)
        => db.LectureMaterials.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
    public void AddMaterial(LectureMaterial material) => db.LectureMaterials.Add(material);
    public async Task<IReadOnlyList<CourseEnrollment>> EnrollmentsForUserAsync(Guid userId, CancellationToken ct)
        => await db.CourseEnrollments.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive).ToListAsync(ct);
    public void AddAudit(ZU.Training.Domain.Settings.AuditEvent auditEvent) => db.AuditEvents.Add(auditEvent);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
