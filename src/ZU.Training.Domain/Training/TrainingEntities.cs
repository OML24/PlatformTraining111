using ZU.Training.Domain.Common;

namespace ZU.Training.Domain.Training;

public enum CourseStatus
{
    Draft = 0,
    Submitted = 1,
    RevisionRequested = 2,
    Rejected = 3,
    Approved = 4,
    InProgress = 5,
    Ended = 6,
    Cancelled = 7
}

public enum EnrollmentMode { SelfRegistration = 0, QualitySelection = 1 }
public enum DeliveryMode { InPerson = 0, Online = 1, Hybrid = 2 }

public sealed class TrainingTrack : Entity
{
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
    public required string ObjectivesAr { get; set; }
    public string? ObjectivesEn { get; set; }
}

public sealed class Course : Entity
{
    public required string TitleAr { get; set; }
    public string? TitleEn { get; set; }
    public Guid TrainingTrackId { get; set; }
    public required string ObjectivesAr { get; set; }
    public string? ObjectivesEn { get; set; }
    public required string TargetAudienceAr { get; set; }
    public string? TargetAudienceEn { get; set; }
    public DeliveryMode DeliveryMode { get; set; }
    public decimal PlannedHours { get; set; }
    public DateTimeOffset PlannedStartUtc { get; set; }
    public DateTimeOffset PlannedEndUtc { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? InstructorUserId { get; set; }
    public required string ActualInstructorNameAr { get; set; }
    public string? ActualInstructorNameEn { get; set; }
    public bool QualityOperated { get; set; }
    public bool HasExam { get; set; }
    public EnrollmentMode EnrollmentMode { get; set; }
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public DateTimeOffset? EvaluationDeadlineUtc { get; set; }
    public Guid? EvaluationFormVersionId { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public string? ReviewNoteAr { get; set; }
    public string? ExternalTrainerOrganizationAr { get; set; }
    public string? ExternalTrainerOrganizationEn { get; set; }
    public bool IsHistorical { get; set; }
}

public sealed class CourseReviewDecision : Entity
{
    public Guid CourseId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public CourseStatus Decision { get; set; }
    public required string NoteAr { get; set; }
}

public sealed class Lecture : Entity
{
    public Guid CourseId { get; set; }
    public required string TitleAr { get; set; }
    public string? TitleEn { get; set; }
    public int SequenceNumber { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DeliveryMode Mode { get; set; }
    public string? OnlineLink { get; set; }
    public required string AttendanceCode { get; set; }
    public DateTimeOffset? CodeActivatedAtUtc { get; set; }
}

public sealed class CourseEnrollment : Entity
{
    public Guid CourseId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset EnrolledAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>يُستخدم في إدخال الدورات التاريخية: توثيق الجودة لإتمام الاستبيان.</summary>
    public bool LegacyEvaluationConfirmed { get; set; }
    /// <summary>يُستخدم في إدخال الدورات التاريخية: توثيق الجودة لاعتماد الأثر.</summary>
    public bool LegacyImpactConfirmed { get; set; }
}

public sealed class AttendanceRecord : Entity
{
    public Guid LectureId { get; set; }
    public Guid EnrollmentId { get; set; }
    public bool IsPresent { get; set; }
    public bool SetByInstructor { get; set; }
    public Guid? LastChangedByUserId { get; set; }
}

public sealed class Exam : Entity
{
    public Guid CourseId { get; set; }
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
}

public sealed class ExamResult : Entity
{
    public Guid ExamId { get; set; }
    public Guid EnrollmentId { get; set; }
    public ExamOutcome Outcome { get; set; }
    public Guid RecordedByUserId { get; set; }
}

public enum CertificateStatus { Valid = 0, Revoked = 1, Hidden = 2 }

public sealed class CertificateTemplate : Entity
{
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
    public required string PdfStorageKey { get; set; }
    public required string Sha256 { get; set; }
    public int Version { get; set; }
}

public sealed class Certificate : Entity
{
    public Guid CourseId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid TemplateId { get; set; }
    public required string ReferenceNumber { get; set; }
    public required string VerificationCode { get; set; }
    public required string PdfStorageKey { get; set; }
    public CertificateStatus Status { get; set; } = CertificateStatus.Valid;
    public string? StatusReason { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StatusChangedAtUtc { get; set; }
}
