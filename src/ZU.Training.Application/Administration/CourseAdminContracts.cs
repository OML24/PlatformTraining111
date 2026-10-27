using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Administration;

public sealed record AdminCourseSummary(Guid Id, string TitleAr, string? TitleEn,
    CourseStatus Status, bool IsActive, bool IsArchived, int CertificateCount);

public interface ICourseAdminStore
{
    Task<IReadOnlyList<Course>> CoursesAsync(CancellationToken ct);
    Task<Course?> CourseAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Certificate>> CertificatesAsync(Guid courseId, CancellationToken ct);
    Task<CourseEnrollment?> EnrollmentAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TrainingTrack>> TracksAsync(CancellationToken ct);
    Task<TrainingTrack?> TrackAsync(Guid id, CancellationToken ct);
    Task<bool> TrackNameExistsAsync(Guid? id, string nameAr, CancellationToken ct);
    void AddTrack(TrainingTrack track);
    void AddAudit(global::ZU.Training.Domain.Settings.AuditEvent audit);
    Task SaveAsync(CancellationToken ct);
}

public sealed record TrackInput(string NameAr, string? NameEn, string? ObjectivesAr, string? ObjectivesEn);
