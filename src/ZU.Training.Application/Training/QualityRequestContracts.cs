using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Training;

public sealed record TrainingNeedInput(string TitleAr, string? TitleEn, string DescriptionAr,
    string? DescriptionEn, Guid? FacultyId);
public sealed record CancellationInput(string ReasonAr);
public sealed record ReviewRequestInput(QualityRequestStatus Status, string? ReviewNoteAr);
public sealed record OfficialDocumentDownload(Stream Content, string FileName);
public sealed record TrainingNeedSummary(Guid Id, string TitleAr, string? TitleEn, string DescriptionAr,
    string? DescriptionEn, Guid FacultyId, Guid SubmittedByUserId, QualityRequestStatus Status,
    string? ReviewNoteAr, IReadOnlyList<Guid> DocumentIds);
public sealed record CancellationSummary(Guid Id, Guid CourseId, string CourseTitleAr,
    Guid RequestedByUserId, string ReasonAr, QualityRequestStatus Status,
    string? ReviewNoteAr, IReadOnlyList<Guid> DocumentIds);

public interface IQualityRequestStore
{
    Task<UserAccount?> UserAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> FacultyExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<Course?> CourseAsync(Guid id, CancellationToken cancellationToken);
    Task<TrainingNeed?> NeedAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrainingNeed>> NeedsAsync(CancellationToken cancellationToken);
    Task<CourseCancellationRequest?> CancellationAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CourseCancellationRequest>> CancellationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Certificate>> CertificatesAsync(Guid courseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CourseEnrollment>> EnrollmentsAsync(Guid courseId, CancellationToken cancellationToken);
    Task<OfficialDocument?> DocumentAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OfficialDocument>> DocumentsAsync(string entityType, Guid entityId,
        CancellationToken cancellationToken);
    void AddNeed(TrainingNeed need);
    void AddCancellation(CourseCancellationRequest request);
    void AddDocument(OfficialDocument document);
    Task<IReadOnlyList<OfficialDocument>> VaultAsync(string? query, string? kind, Guid? entityId,
        int skip, int take, CancellationToken cancellationToken);
    Task<int> VaultCountAsync(string? query, string? kind, Guid? entityId, CancellationToken cancellationToken);
    void AddAudit(ZU.Training.Domain.Settings.AuditEvent audit);
    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed record OfficialDocumentRow(Guid Id, string EntityType, Guid EntityId, string Kind,
    string OriginalName, string Sha256, Guid UploadedByUserId, DateTimeOffset UploadedAtUtc);

public sealed record OfficialDocumentVault(IReadOnlyList<OfficialDocumentRow> Items, int Total, int Page);
