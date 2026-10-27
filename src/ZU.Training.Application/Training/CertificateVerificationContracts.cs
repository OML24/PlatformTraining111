using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Training;

public sealed record CertificateVerification(string ReferenceNumber, CertificateStatus Status,
    string? StatusReason, string RecipientNameAr, string? RecipientNameEn,
    string CourseTitleAr, string? CourseTitleEn, string InstructorNameAr,
    string? InstructorNameEn, DateTimeOffset IssuedAtUtc);

public interface ICertificateVerificationStore
{
    Task<Certificate?> ByReferenceAsync(string reference, CancellationToken ct);
    Task<CourseEnrollment?> EnrollmentAsync(Guid id, CancellationToken ct);
    Task<Course?> CourseAsync(Guid id, CancellationToken ct);
    Task<global::ZU.Training.Domain.Identity.UserAccount?> UserAsync(Guid id, CancellationToken ct);
}
