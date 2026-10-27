using System.Security.Cryptography;
using ZU.Training.Application.Communication;
using ZU.Training.Application.Security;
using ZU.Training.Domain.Communication;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Training;

public sealed record CertificateOptions(string VerificationBaseUrl)
{
    public string UrlFor(string referenceNumber)
    {
        var separator = VerificationBaseUrl.Contains('?') ? "&" : "?";
        return VerificationBaseUrl.TrimEnd('/') + separator + "cert=" + Uri.EscapeDataString(referenceNumber);
    }
}

/// <summary>بيانات تُطبع على الشهادة فوق مواضع القالب الثابت.</summary>
public sealed record CertificateDocumentData(
    string ReferenceNumber,
    string VerificationCode,
    string VerificationUrl,
    string RecipientNameAr,
    string? RecipientNameEn,
    string CourseTitleAr,
    string? CourseTitleEn,
    string InstructorNameAr,
    string? InstructorNameEn,
    string? FacultyNameAr,
    string? FacultyNameEn,
    decimal PlannedHours,
    DateTimeOffset CourseStartUtc,
    DateTimeOffset CourseEndUtc,
    DateTimeOffset IssuedAtUtc);

public interface ICertificatePdfGenerator
{
    Task<byte[]> RenderAsync(CertificateDocumentData data, CancellationToken ct);
}

public interface IQrCodeRenderer
{
    byte[] RenderPng(string content);
}

public interface ICertificateFileStorage
{
    Task<string> SavePdfAsync(Stream content, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}

public sealed record CertificateCard(Guid Id, string ReferenceNumber, string VerificationCode,
    CertificateStatus Status, string? StatusReason, DateTimeOffset IssuedAtUtc,
    string CourseTitleAr, string? CourseTitleEn, string InstructorNameAr, string? InstructorNameEn,
    string VerificationUrl, bool CanDownload);

/// <summary>حالة مشاركة عضو في دورة انتهت: هل يستوفي متطلبات الشهادة أم لا.</summary>
public sealed record ParticipationStatus(Guid CourseId, string CourseTitleAr, string? CourseTitleEn,
    CourseStatus CourseStatus, int TotalLectures, int AttendedLectures, ExamOutcome ExamOutcome,
    bool HasExam, bool EvaluationCompleted, bool ImpactApproved, bool IsEligible,
    IReadOnlyList<CertificateIneligibilityReason> Reasons, DateTimeOffset? EndedAtUtc);

public sealed record MemberCertificateProfile(Guid UserId, string NameAr, string? NameEn,
    IReadOnlyList<CertificateCard> Certificates, IReadOnlyList<ParticipationStatus> Participations);

public interface ICertificateIssuanceStore
{
    Task<IReadOnlyList<Course>> CoursesAsync(CancellationToken ct);
    Task<Course?> CourseAsync(Guid id, CancellationToken ct);
    Task<UserAccount?> UserAsync(Guid id, CancellationToken ct);
    Task<Faculty?> FacultyAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CourseEnrollment>> EnrollmentsAsync(Guid courseId, CancellationToken ct);
    Task<CourseEnrollment?> EnrollmentAsync(Guid courseId, Guid userId, CancellationToken ct);
    Task<CourseEnrollment?> EnrollmentByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CourseEnrollment>> EnrollmentsForUserAsync(Guid userId, CancellationToken ct);
    Task<int> LectureCountAsync(Guid courseId, CancellationToken ct);
    Task<int> AttendanceCountAsync(Guid courseId, Guid enrollmentId, CancellationToken ct);
    Task<ExamOutcome> ExamOutcomeAsync(Guid courseId, Guid enrollmentId, CancellationToken ct);
    Task<bool> HasEvaluationAsync(Guid? versionId, Guid enrollmentId, CancellationToken ct);
    Task<bool> HasApprovedImpactAsync(Guid enrollmentId, CancellationToken ct);
    Task<Certificate?> CertificateAsync(Guid enrollmentId, CancellationToken ct);
    Task<Certificate?> CertificateByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Certificate>> CertificatesForCourseAsync(Guid courseId, CancellationToken ct);
    Task<IReadOnlyList<Certificate>> CertificatesForUserAsync(Guid userId, CancellationToken ct);
    Task<CertificateTemplate?> CurrentTemplateAsync(CancellationToken ct);
    Task<CertificateTemplate?> TemplateAsync(Guid id, CancellationToken ct);
    Task<int> NextCertificateNumberAsync(string prefix, CancellationToken ct);
    void AddCertificate(Certificate certificate);
    void AddAudit(Domain.Settings.AuditEvent audit);
    Task SaveAsync(CancellationToken ct);
}

/// <summary>
/// بوابة إصدار الشهادات: تحسب الاستحقاق، تُصدر الشهادة وملف PDF، تُلغي الشهادات عند فقدان
/// الاستحقاق، وتُبلّغ صاحب الشهادة بالإشعارات.
/// </summary>
public sealed class CertificateIssuanceService(ICertificateIssuanceStore store,
    ICertificatePdfGenerator generator, ICertificateFileStorage files,
    NotificationService notifications, CertificateOptions options,
    IPermissionAccess permissions)
{
    public const string RevocationReasonIneligible = "RequirementLost";
    public const string RevocationReasonCourseCancelled = "CourseCancelled";
    public const string RevocationReasonCourseDisabled = "AdminCourseDisabled";

    // ---------------------------------------------------------------- إصدار

    public async Task<int> IssueForCourseAsync(Guid actorId, Guid courseId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.IssueCertificates, ct);
        return await IssueForCourseCoreAsync(courseId, actorId, ct);
    }

    public async Task<int> IssueForCourseCoreAsync(Guid courseId, Guid? actorId, CancellationToken ct)
    {
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        if (course.Status != CourseStatus.Ended)
            throw new InvalidOperationException("The course must be ended before issuing certificates.");
        var issued = 0;
        foreach (var enrollment in await store.EnrollmentsAsync(courseId, ct))
        {
            var certificate = await TryIssueAsync(course, enrollment, actorId, ct);
            if (certificate is not null) issued++;
        }
        return issued;
    }

    /// <summary>يُعاد تقييم الاستحقاق بعد أي تغيير في الحضور أو الامتحان أو التقييم أو الأثر.</summary>
    public async Task ReconcileAsync(Guid courseId, Guid? actorId, CancellationToken ct)
    {
        var course = await store.CourseAsync(courseId, ct);
        if (course is null) return;
        foreach (var enrollment in await store.EnrollmentsAsync(courseId, ct))
            await ReconcileEnrollmentAsync(course, enrollment, actorId, ct);
    }

    public async Task ReconcileEnrollmentAsync(Guid courseId, Guid enrollmentId, Guid? actorId,
        CancellationToken ct)
    {
        var course = await store.CourseAsync(courseId, ct);
        if (course is null) return;
        var enrollment = await store.EnrollmentByIdAsync(enrollmentId, ct);
        if (enrollment is null) return;
        await ReconcileEnrollmentAsync(course, enrollment, actorId, ct);
    }

    public async Task<Certificate?> TryIssueAsync(Course course, CourseEnrollment enrollment, Guid? actorId,
        CancellationToken ct)
    {
        var existing = await store.CertificateAsync(enrollment.Id, ct);
        if (existing is { Status: CertificateStatus.Valid }) return existing;
        if (existing is { Status: CertificateStatus.Hidden }) return null;

        var eligibility = await EvaluateAsync(course, enrollment, ct);
        if (!eligibility.IsEligible) return null;

        var template = await store.CurrentTemplateAsync(ct);
        var certificate = existing;
        if (certificate is null)
        {
            certificate = new Certificate
            {
                CourseId = course.Id,
                EnrollmentId = enrollment.Id,
                TemplateId = template?.Id ?? Guid.Empty,
                ReferenceNumber = await NextReferenceAsync(course, ct),
                VerificationCode = VerificationCode(),
                PdfStorageKey = string.Empty,
                Status = CertificateStatus.Valid
            };
            store.AddCertificate(certificate);
        }
        else
        {
            certificate.Status = CertificateStatus.Valid;
            certificate.StatusReason = null;
            certificate.StatusChangedAtUtc = DateTimeOffset.UtcNow;
            if (template is not null) certificate.TemplateId = template.Id;
        }

        var pdf = await RenderAsync(course, enrollment, certificate, ct);
        certificate.PdfStorageKey = await files.SavePdfAsync(new MemoryStream(pdf), ct);
        await store.SaveAsync(ct);

        store.AddAudit(new Domain.Settings.AuditEvent
        {
            ActorUserId = actorId, ActionKey = "certificate.issued", EntityType = "Certificate",
            EntityId = certificate.Id, Reason = certificate.ReferenceNumber
        });
        await store.SaveAsync(ct);

        if (enrollment.UserId != Guid.Empty)
            await notifications.NotifyAsync(enrollment.UserId, NotificationKinds.CertificateIssued,
                "صدرت شهادتك", "Your certificate is ready",
                "تم إصدار شهادة الدورة: " + course.TitleAr, "Certificate issued for: " + course.TitleAr,
                "/member-profile.html?tab=certificates", ct);
        return certificate;
    }

    // ---------------------------------------------------------------- إلغاء

    public async Task RevokeForCourseAsync(Guid actorId, Guid courseId, string reason, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.RevokeCertificates, ct);
        await RevokeCourseCertificatesAsync(courseId, reason, actorId, ct);
    }

    public async Task RevokeCourseCertificatesAsync(Guid courseId, string reason, Guid? actorId,
        CancellationToken ct)
        => await RevokeCourseCertificatesAsync(courseId, reason, actorId, notify: true, ct);

    public async Task RevokeCourseCertificatesAsync(Guid courseId, string reason, Guid? actorId, bool notify,
        CancellationToken ct)
    {
        var course = await store.CourseAsync(courseId, ct);
        foreach (var certificate in await store.CertificatesForCourseAsync(courseId, ct))
        {
            if (certificate.Status == CertificateStatus.Revoked) continue;
            certificate.Status = CertificateStatus.Revoked;
            certificate.StatusReason = reason;
            certificate.StatusChangedAtUtc = DateTimeOffset.UtcNow;
            if (!notify) continue;
            var enrollment = await store.EnrollmentByIdAsync(certificate.EnrollmentId, ct);
            if (enrollment is null) continue;
            await notifications.NotifyAsync(enrollment.UserId, NotificationKinds.CertificateRevoked,
                "أُلغيت شهادة", "Certificate revoked",
                "أُلغيت شهادة الدورة: " + (course?.TitleAr ?? string.Empty),
                "Certificate revoked for: " + (course?.TitleAr ?? string.Empty),
                "/member-profile.html?tab=certificates", ct);
        }
        store.AddAudit(new Domain.Settings.AuditEvent
        {
            ActorUserId = actorId, ActionKey = "certificate.course_revoked", EntityType = "Course",
            EntityId = courseId, Reason = reason
        });
        await store.SaveAsync(ct);
    }

    public async Task RevokeAsync(Guid actorId, Guid certificateId, string reason, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.RevokeCertificates, ct);
        var certificate = await store.CertificateByIdAsync(certificateId, ct)
            ?? throw new KeyNotFoundException("Certificate not found.");
        certificate.Status = CertificateStatus.Revoked;
        certificate.StatusReason = string.IsNullOrWhiteSpace(reason) ? "Manual" : reason.Trim();
        certificate.StatusChangedAtUtc = DateTimeOffset.UtcNow;
        store.AddAudit(new Domain.Settings.AuditEvent
        {
            ActorUserId = actorId, ActionKey = "certificate.revoked", EntityType = "Certificate",
            EntityId = certificate.Id, Reason = certificate.StatusReason
        });
        await store.SaveAsync(ct);
        var enrollment = await store.EnrollmentByIdAsync(certificate.EnrollmentId, ct);
        if (enrollment is not null)
            await notifications.NotifyAsync(enrollment.UserId, NotificationKinds.CertificateRevoked,
                "أُلغيت شهادة", "Certificate revoked", certificate.StatusReason,
                certificate.StatusReason, "/member-profile.html?tab=certificates", ct);
    }

    private async Task ReconcileEnrollmentAsync(Course course, CourseEnrollment enrollment, Guid? actorId,
        CancellationToken ct)
    {
        var existing = await store.CertificateAsync(enrollment.Id, ct);
        var eligibility = await EvaluateAsync(course, enrollment, ct);
        if (eligibility.IsEligible)
        {
            if (existing is null || existing.Status == CertificateStatus.Revoked)
                await TryIssueAsync(course, enrollment, actorId, ct);
            return;
        }
        if (existing is not { Status: CertificateStatus.Valid }) return;
        if (course.Status != CourseStatus.Ended) return;
        existing.Status = CertificateStatus.Revoked;
        existing.StatusReason = RevocationReasonIneligible;
        existing.StatusChangedAtUtc = DateTimeOffset.UtcNow;
        store.AddAudit(new Domain.Settings.AuditEvent
        {
            ActorUserId = actorId, ActionKey = "certificate.revoked.ineligible", EntityType = "Certificate",
            EntityId = existing.Id, Reason = RevocationReasonIneligible
        });
        await store.SaveAsync(ct);
        await notifications.NotifyAsync(enrollment.UserId, NotificationKinds.CertificateRevoked,
            "أُلغيت شهادة لفقدان شرط", "Certificate revoked: requirement lost",
            "أُلغيت شهادة الدورة بعد إعادة تقييم المتطلبات: " + course.TitleAr,
            "Certificate revoked after re-evaluation: " + course.TitleAr,
            "/member-profile.html?tab=certificates", ct);
    }

    // ---------------------------------------------------------------- قراءة

    public async Task<MemberCertificateProfile> ProfileAsync(Guid actorId, Guid targetUserId,
        CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewCourses, ct);
        var user = await store.UserAsync(targetUserId, ct) ?? throw new KeyNotFoundException("User not found.");
        var certificates = await store.CertificatesForUserAsync(targetUserId, ct);
        var cards = new List<CertificateCard>();
        foreach (var certificate in certificates.Where(x => x.Status == CertificateStatus.Valid))
            cards.Add(await CardAsync(certificate, actorId == targetUserId, ct));

        var participations = new List<ParticipationStatus>();
        foreach (var enrollment in await store.EnrollmentsForUserAsync(targetUserId, ct))
        {
            var course = await store.CourseAsync(enrollment.CourseId, ct);
            if (course is null) continue;
            if (course.Status is not (CourseStatus.Ended or CourseStatus.Cancelled)) continue;
            var hasValid = certificates.Any(x => x.EnrollmentId == enrollment.Id &&
                x.Status == CertificateStatus.Valid);
            if (hasValid) continue;
            var eligibility = await EvaluateAsync(course, enrollment, ct);
            var total = await store.LectureCountAsync(course.Id, ct);
            var attended = await store.AttendanceCountAsync(course.Id, enrollment.Id, ct);
            participations.Add(new ParticipationStatus(course.Id, course.TitleAr, course.TitleEn,
                course.Status, total, attended, await store.ExamOutcomeAsync(course.Id, enrollment.Id, ct),
                course.HasExam,
                await store.HasEvaluationAsync(course.EvaluationFormVersionId, enrollment.Id, ct) ||
                    enrollment.LegacyEvaluationConfirmed,
                await store.HasApprovedImpactAsync(enrollment.Id, ct) || enrollment.LegacyImpactConfirmed,
                eligibility.IsEligible, eligibility.Reasons, course.EndedAtUtc));
        }
        return new MemberCertificateProfile(user.Id, user.NameAr, user.NameEn, cards, participations);
    }

    public async Task<IReadOnlyList<CertificateCard>> MineAsync(Guid actorId, CancellationToken ct)
    {
        var certificates = await store.CertificatesForUserAsync(actorId, ct);
        var cards = new List<CertificateCard>();
        foreach (var certificate in certificates.Where(x => x.Status == CertificateStatus.Valid))
            cards.Add(await CardAsync(certificate, true, ct));
        return cards;
    }

    public async Task<IReadOnlyList<CertificateCard>> ForCourseAsync(Guid actorId, Guid courseId,
        CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewCourseStatistics, ct);
        var cards = new List<CertificateCard>();
        foreach (var certificate in await store.CertificatesForCourseAsync(courseId, ct))
            cards.Add(await CardAsync(certificate, false, ct));
        return cards;
    }

    /// <summary>تنزيل ملف PDF محصور بصاحب الشهادة.</summary>
    public async Task<CertificateDownload> DownloadAsync(Guid actorId, Guid certificateId, CancellationToken ct)
    {
        var certificate = await store.CertificateByIdAsync(certificateId, ct)
            ?? throw new KeyNotFoundException("Certificate not found.");
        var enrollment = await store.EnrollmentByIdAsync(certificate.EnrollmentId, ct)
            ?? throw new KeyNotFoundException("Enrollment not found.");
        if (enrollment.UserId != actorId) throw new UnauthorizedAccessException();
        if (certificate.Status != CertificateStatus.Valid)
            throw new InvalidOperationException("Only a valid certificate can be downloaded.");
        var stream = await files.OpenReadAsync(certificate.PdfStorageKey, ct)
            ?? throw new KeyNotFoundException("Certificate file not found.");
        return new CertificateDownload(stream, certificate.ReferenceNumber + ".pdf");
    }

    private async Task<CertificateCard> CardAsync(Certificate certificate, bool canDownload,
        CancellationToken ct)
    {
        var course = await store.CourseAsync(certificate.CourseId, ct);
        return new CertificateCard(certificate.Id, certificate.ReferenceNumber, certificate.VerificationCode,
            certificate.Status, certificate.StatusReason, certificate.IssuedAtUtc,
            course?.TitleAr ?? string.Empty, course?.TitleEn, course?.ActualInstructorNameAr ?? string.Empty,
            course?.ActualInstructorNameEn,
            options.UrlFor(certificate.ReferenceNumber), canDownload);
    }

    private async Task<CertificateEligibilityResult> EvaluateAsync(Course course, CourseEnrollment enrollment,
        CancellationToken ct)
    {
        var total = await store.LectureCountAsync(course.Id, ct);
        var attended = await store.AttendanceCountAsync(course.Id, enrollment.Id, ct);
        var exam = await store.ExamOutcomeAsync(course.Id, enrollment.Id, ct);
        var evaluation = await store.HasEvaluationAsync(course.EvaluationFormVersionId, enrollment.Id, ct)
            || enrollment.LegacyEvaluationConfirmed;
        var impact = await store.HasApprovedImpactAsync(enrollment.Id, ct) || enrollment.LegacyImpactConfirmed;
        return CertificateEligibility.Evaluate(new CertificateEligibilityInput(
            course.Status == CourseStatus.Ended, total, Math.Min(attended, total), course.HasExam, exam,
            evaluation, impact));
    }

    private async Task<byte[]> RenderAsync(Course course, CourseEnrollment enrollment, Certificate certificate,
        CancellationToken ct)
    {
        var user = await store.UserAsync(enrollment.UserId, ct);
        var faculty = course.FacultyId.HasValue ? await store.FacultyAsync(course.FacultyId.Value, ct) : null;
        var data = new CertificateDocumentData(
            certificate.ReferenceNumber, certificate.VerificationCode,
            options.UrlFor(certificate.ReferenceNumber),
            user?.NameAr ?? string.Empty, user?.NameEn,
            course.TitleAr, course.TitleEn,
            course.ActualInstructorNameAr, course.ActualInstructorNameEn,
            faculty?.NameAr, faculty?.NameEn,
            course.PlannedHours, course.PlannedStartUtc, course.PlannedEndUtc,
            certificate.IssuedAtUtc);
        return await generator.RenderAsync(data, ct);
    }

    private async Task<string> NextReferenceAsync(Course course, CancellationToken ct)
    {
        var facultyCode = "GN";
        if (course.FacultyId.HasValue)
        {
            var faculty = await store.FacultyAsync(course.FacultyId.Value, ct);
            var sanitized = new string((faculty?.ExternalCode ?? string.Empty)
                .Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (sanitized.Length >= 2) facultyCode = sanitized.Length > 4 ? sanitized[..4] : sanitized;
        }
        var year = DateTimeOffset.UtcNow.Year;
        var prefix = $"ZU-{year}-{facultyCode}-";
        var next = await store.NextCertificateNumberAsync(prefix, ct) + 1;
        return prefix + next.ToString("D4");
    }

    private static string VerificationCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(10);
        var chars = new char[10];
        for (var i = 0; i < chars.Length; i++) chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    private async Task RequireAsync(Guid actorId, string permission, CancellationToken ct)
    {
        if (actorId == Guid.Empty || !await permissions.IsAllowedAsync(actorId, permission, ct))
            throw new UnauthorizedAccessException();
    }
}

public sealed record CertificateDownload(Stream Content, string FileName);
