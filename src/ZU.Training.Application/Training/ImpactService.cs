using ZU.Training.Application.Communication;
using ZU.Training.Application.Security;
using ZU.Training.Domain.Communication;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Training;

public sealed class ImpactService(IImpactStore store, IOfficialDocumentStorage storage,
    IAuthStore auth, IPermissionAccess permissions, CertificateIssuanceService certificates,
    NotificationService notifications)
{
    public async Task<IReadOnlyList<ImpactSummary>> MineAsync(Guid userId, Guid courseId, CancellationToken ct)
    {
        await RequireAsync(userId, PermissionKeys.ViewCourses, ct);
        var enrollment = await store.EnrollmentAsync(courseId, userId, ct);
        if (enrollment?.IsActive != true) throw new UnauthorizedAccessException();
        return (await store.SubmissionsAsync(enrollment.Id, ct)).Select(Summary).ToList();
    }

    public async Task<ImpactSubmission> UploadAsync(Guid userId, Guid courseId,
        Stream content, string originalName, CancellationToken ct)
    {
        await RequireAsync(userId, PermissionKeys.SubmitImpact, ct);
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        if (course.Status != CourseStatus.Ended) throw new InvalidOperationException("The course must be ended first.");
        var enrollment = await store.EnrollmentAsync(courseId, userId, ct);
        if (enrollment?.IsActive != true) throw new UnauthorizedAccessException();
        var history = await store.SubmissionsAsync(enrollment.Id, ct);
        if (history.LastOrDefault()?.Status is ImpactStatus.Pending or ImpactStatus.Approved)
            throw new InvalidOperationException("The current impact file is awaiting review or already approved.");
        var saved = await storage.SaveAsync(content, originalName, ct);
        if (history.Any(x => x.Sha256 == saved.Sha256))
        {
            await storage.DeleteAsync(saved.StorageKey, ct);
            throw new InvalidOperationException("Upload a revised file with different contents.");
        }
        var submission = new ImpactSubmission
        {
            EnrollmentId = enrollment.Id, Version = history.Count + 1,
            OriginalName = saved.OriginalName, StorageKey = saved.StorageKey, Sha256 = saved.Sha256
        };
        store.Add(submission);
        try { await store.SaveAsync(ct); }
        catch
        {
            await storage.DeleteAsync(saved.StorageKey, CancellationToken.None);
            throw;
        }
        return submission;
    }

    public async Task<IReadOnlyList<ImpactSummary>> RosterAsync(Guid actorId, Guid courseId, CancellationToken ct)
    {
        await RequireOperatorAsync(actorId, courseId, PermissionKeys.ReviewImpact, ct);
        var enrollments = await store.EnrollmentsAsync(courseId, ct);
        var result = new List<ImpactSummary>();
        foreach (var enrollment in enrollments)
            result.AddRange((await store.SubmissionsAsync(enrollment.Id, ct)).Select(Summary));
        return result;
    }

    public async Task<ImpactSubmission> ReviewAsync(Guid actorId, Guid submissionId,
        bool approve, string? noteAr, CancellationToken ct)
    {
        var submission = await store.SubmissionAsync(submissionId, ct)
            ?? throw new KeyNotFoundException("Impact file not found.");
        var enrollment = await store.EnrollmentByIdAsync(submission.EnrollmentId, ct)
            ?? throw new KeyNotFoundException("Enrollment not found.");
        await RequireOperatorAsync(actorId, enrollment.CourseId, PermissionKeys.ReviewImpact, ct);
        if (submission.Status != ImpactStatus.Pending)
            throw new InvalidOperationException("Only a pending impact file can be reviewed.");
        if (!approve && string.IsNullOrWhiteSpace(noteAr))
            throw new ArgumentException("A return reason is required.");
        if (noteAr?.Length > 4000) throw new ArgumentException("Review note is too long.");
        submission.Status = approve ? ImpactStatus.Approved : ImpactStatus.Returned;
        submission.ReviewNoteAr = string.IsNullOrWhiteSpace(noteAr) ? null : noteAr.Trim();
        submission.ReviewedByUserId = actorId;
        submission.ReviewedAtUtc = DateTimeOffset.UtcNow;
        await store.SaveAsync(ct);

        await notifications.NotifyAsync(enrollment.UserId,
            approve ? NotificationKinds.ImpactApproved : NotificationKinds.ImpactReturned,
            approve ? "اعتُمد ملف الأثر" : "أُعيد ملف الأثر للتعديل",
            approve ? "Impact file approved" : "Impact file returned",
            submission.ReviewNoteAr, submission.ReviewNoteAr,
            "/member.html?course=" + enrollment.CourseId, ct);
        await certificates.ReconcileEnrollmentAsync(enrollment.CourseId, enrollment.Id, actorId, ct);
        return submission;
    }

    public async Task<OfficialDocumentDownload> DownloadAsync(Guid actorId, Guid submissionId, CancellationToken ct)
    {
        var submission = await store.SubmissionAsync(submissionId, ct)
            ?? throw new KeyNotFoundException("Impact file not found.");
        var enrollment = await store.EnrollmentByIdAsync(submission.EnrollmentId, ct)
            ?? throw new KeyNotFoundException("Enrollment not found.");
        if (enrollment.UserId != actorId)
            await RequireOperatorAsync(actorId, enrollment.CourseId, PermissionKeys.ReviewImpact, ct);
        var stream = await storage.OpenReadAsync(submission.StorageKey, ct)
            ?? throw new KeyNotFoundException("Impact document not found on disk.");
        return new OfficialDocumentDownload(stream, submission.OriginalName);
    }

    private async Task RequireOperatorAsync(Guid actorId, Guid courseId, string permission, CancellationToken ct)
    {
        await RequireAsync(actorId, permission, ct);
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        var roles = (await auth.GetRoleKeysAsync(actorId, ct)).ToHashSet(StringComparer.Ordinal);
        if (!roles.Contains("ADMIN") && course.InstructorUserId != actorId &&
            !(roles.Contains("QA") && course.QualityOperated)) throw new UnauthorizedAccessException();
    }

    private async Task RequireAsync(Guid actorId, string permission, CancellationToken ct)
    {
        if (await store.UserAsync(actorId, ct) is not { IsActive: true } ||
            !await permissions.IsAllowedAsync(actorId, permission, ct)) throw new UnauthorizedAccessException();
    }

    private static ImpactSummary Summary(ImpactSubmission x)
        => new(x.Id, x.EnrollmentId, x.Version, x.OriginalName, x.Status, x.ReviewNoteAr, x.CreatedAtUtc);
}
