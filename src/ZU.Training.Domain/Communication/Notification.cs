using ZU.Training.Domain.Common;

namespace ZU.Training.Domain.Communication;

public static class NotificationKinds
{
    public const string LectureAdded = "lecture.added";
    public const string CourseApproved = "course.approved";
    public const string CourseCancelled = "course.cancelled";
    public const string CertificateIssued = "certificate.issued";
    public const string CertificateRevoked = "certificate.revoked";
    public const string ImpactReturned = "impact.returned";
    public const string ImpactApproved = "impact.approved";
    public const string ProposalReviewed = "proposal.reviewed";
    public const string EvaluationPublished = "evaluation.published";
    public const string AccountReady = "account.ready";
    public const string ExternalTrainerRequested = "external_trainer.requested";
    public const string ExternalTrainerCreated = "external_trainer.created";
}

/// <summary>
/// إشعار داخلي لمستخدم المنصة. النص العربي إلزامي والإنجليزي اختياري.
/// </summary>
public sealed class Notification : Entity
{
    public Guid UserId { get; set; }
    public required string Kind { get; set; }
    public required string TitleAr { get; set; }
    public string? TitleEn { get; set; }
    public string? BodyAr { get; set; }
    public string? BodyEn { get; set; }
    public string? LinkUrl { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
}
