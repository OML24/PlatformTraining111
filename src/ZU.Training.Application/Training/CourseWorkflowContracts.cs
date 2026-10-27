using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Training;

public sealed record CreateCourseInput(string TitleAr, string? TitleEn, Guid TrainingTrackId,
    string ObjectivesAr, string? ObjectivesEn, string TargetAudienceAr, string? TargetAudienceEn,
    DeliveryMode DeliveryMode, decimal PlannedHours, DateTimeOffset PlannedStartUtc,
    DateTimeOffset PlannedEndUtc, Guid? FacultyId,
    Guid? InstructorUserId, string? ActualInstructorNameAr, string? ActualInstructorNameEn,
    bool QualityOperated, bool HasExam, EnrollmentMode EnrollmentMode, DateTimeOffset? EvaluationDeadlineUtc);

public sealed record CourseSummary(Guid Id, string TitleAr, string? TitleEn, CourseStatus Status,
    bool HasExam, EnrollmentMode EnrollmentMode, bool QualityOperated, string ActualInstructorNameAr,
    Guid TrainingTrackId, Guid? FacultyId, Guid CreatedByUserId, Guid? InstructorUserId,
    bool IsEnrolled, bool CanSelfEnroll, string? ReviewNoteAr, Guid? EvaluationFormVersionId);

public sealed record LectureSummary(Guid Id, string TitleAr, string? TitleEn, int SequenceNumber,
    DateTimeOffset? StartsAtUtc, bool CodeIsActive, DateTimeOffset? CodeExpiresAtUtc, bool? IsPresent,
    DeliveryMode Mode, string? OnlineLink);

public sealed record ParticipantSummary(Guid EnrollmentId, Guid UserId, string NameAr, string? NameEn,
    int AttendedLectures, int TotalLectures, ExamOutcome ExamOutcome);
public sealed record ParticipantAttendance(Guid LectureId, Guid EnrollmentId, bool IsPresent);
public sealed record DirectoryUser(Guid Id, string LoginName, string NameAr, string? NameEn, Guid? FacultyId);

public sealed record CourseDetails(CourseSummary Course, IReadOnlyList<LectureSummary> Lectures,
    IReadOnlyList<ParticipantSummary> Participants, IReadOnlyList<ParticipantAttendance> Attendance,
    string? ExamNameAr, string? ExamNameEn,
    ExamOutcome MyExamOutcome, string ObjectivesAr, string? ObjectivesEn,
    string TargetAudienceAr, string? TargetAudienceEn, DeliveryMode DeliveryMode,
    decimal PlannedHours, DateTimeOffset PlannedStartUtc, DateTimeOffset PlannedEndUtc,
    DateTimeOffset? EvaluationDeadlineUtc, DateTimeOffset? EndedAtUtc);

public interface ICourseStore
{
    Task<UserAccount?> UserAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Faculty>> FacultiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAccount>> SearchUsersAsync(string query, Guid? facultyId, CancellationToken cancellationToken);
    Task<bool> TrackExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingTrack>> TracksAsync(CancellationToken cancellationToken);
    Task<EvaluationFormVersion?> LatestEvaluationVersionAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Course>> CoursesAsync(CancellationToken cancellationToken);
    Task<Course?> CourseAsync(Guid id, CancellationToken cancellationToken);
    Task<Lecture?> LectureAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Lecture>> LecturesAsync(Guid courseId, CancellationToken cancellationToken);
    Task<CourseEnrollment?> EnrollmentAsync(Guid courseId, Guid userId, CancellationToken cancellationToken);
    Task<CourseEnrollment?> EnrollmentByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CourseEnrollment>> EnrollmentsAsync(Guid courseId, CancellationToken cancellationToken);
    Task<AttendanceRecord?> AttendanceAsync(Guid lectureId, Guid enrollmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttendanceRecord>> AttendancesAsync(Guid courseId, CancellationToken cancellationToken);
    Task<Exam?> ExamAsync(Guid courseId, CancellationToken cancellationToken);
    Task<ExamResult?> ExamResultAsync(Guid examId, Guid enrollmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExamResult>> ExamResultsAsync(Guid examId, CancellationToken cancellationToken);
    void AddCourse(Course course);
    void AddReviewDecision(CourseReviewDecision decision);
    void AddLecture(Lecture lecture);
    void AddEnrollment(CourseEnrollment enrollment);
    void AddAttendance(AttendanceRecord attendance);
    void AddExam(Exam exam);
    void AddExamResult(ExamResult result);
    Task<IReadOnlyList<LectureMaterial>> MaterialsAsync(Guid lectureId, CancellationToken cancellationToken);
    Task<LectureMaterial?> MaterialAsync(Guid id, CancellationToken cancellationToken);
    void AddMaterial(LectureMaterial material);
    Task<IReadOnlyList<CourseEnrollment>> EnrollmentsForUserAsync(Guid userId, CancellationToken cancellationToken);
    void AddAudit(Domain.Settings.AuditEvent auditEvent);
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>مشارك في دورة تاريخية مع توثيق الجودة لمتطلبات الشهادة.</summary>
public sealed record HistoricalParticipantInput(Guid UserId, int AttendedLectures, ExamOutcome ExamOutcome,
    bool EvaluationConfirmed, bool ImpactConfirmed);

/// <summary>إدخال دورة تاريخية (عُقدت قبل إطلاق المنصة) مع إصدار شهادات المستحقين.</summary>
public sealed record HistoricalCourseInput(string TitleAr, string? TitleEn, Guid TrainingTrackId,
    string ObjectivesAr, string? ObjectivesEn, string TargetAudienceAr, string? TargetAudienceEn,
    DeliveryMode DeliveryMode, decimal PlannedHours, DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    Guid? FacultyId, Guid? InstructorUserId, string? ActualInstructorNameAr, string? ActualInstructorNameEn,
    bool HasExam, string? ExamNameAr, string? ExamNameEn, int LectureCount, bool EnrollmentModeSelfRegistration,
    IReadOnlyList<HistoricalParticipantInput> Participants);
