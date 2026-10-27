using ZU.Training.Application.Security;
using ZU.Training.Application.Training;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Administration;

public sealed class CourseAdminService(ICourseAdminStore store, CertificateGateService gate,
    IAuthStore auth, IPermissionAccess permissions)
{
    public async Task<IReadOnlyList<AdminCourseSummary>> ListAsync(Guid actorId, CancellationToken ct)
    {
        await RequireAdminAsync(actorId, ct);
        var rows = new List<AdminCourseSummary>();
        foreach (var course in await store.CoursesAsync(ct))
            rows.Add(new AdminCourseSummary(course.Id, course.TitleAr, course.TitleEn,
                course.Status, course.IsActive, course.IsArchived,
                (await store.CertificatesAsync(course.Id, ct)).Count));
        return rows;
    }

    public async Task<AdminCourseSummary> DeactivateAsync(Guid actorId, Guid courseId,
        bool hideCertificates, bool acknowledged, CancellationToken ct)
    {
        await RequireAdminAsync(actorId, ct);
        if (!acknowledged) throw new ArgumentException("The deletion warning must be acknowledged.");
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        if (!course.IsActive) throw new InvalidOperationException("The course is already disabled.");
        var certificates = await store.CertificatesAsync(courseId, ct);
        course.IsActive = false;
        if (hideCertificates)
        {
            foreach (var certificate in certificates)
            {
                if (certificate.Status != CertificateStatus.Valid) continue;
                certificate.Status = CertificateStatus.Hidden;
                certificate.StatusReason = "AdminCourseDisabled";
            }
        }
        await store.SaveAsync(ct);
        return new AdminCourseSummary(course.Id, course.TitleAr, course.TitleEn,
            course.Status, false, course.IsArchived, certificates.Count);
    }

    public async Task<AdminCourseSummary> RestoreAsync(Guid actorId, Guid courseId, CancellationToken ct)
    {
        await RequireAdminAsync(actorId, ct);
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        if (course.IsActive) throw new InvalidOperationException("The course is already active.");
        course.IsActive = true;
        var certificates = await store.CertificatesAsync(courseId, ct);
        foreach (var certificate in certificates.Where(x => x.Status == CertificateStatus.Hidden &&
            x.StatusReason == "AdminCourseDisabled"))
        {
            var enrollment = await store.EnrollmentAsync(certificate.EnrollmentId, ct);
            if (enrollment?.IsActive != true) continue;
            var status = await gate.StatusForEnrollmentAsync(course, enrollment, ct);
            if (!status.IsEligible) continue;
            certificate.Status = CertificateStatus.Valid;
            certificate.StatusReason = null;
        }
        await store.SaveAsync(ct);
        return new AdminCourseSummary(course.Id, course.TitleAr, course.TitleEn,
            course.Status, true, course.IsArchived, certificates.Count);
    }

    // ------------------------------------------------------------------ محاور التدريب

    public async Task<IReadOnlyList<TrainingTrack>> TracksAsync(Guid actorId, CancellationToken ct)
    {
        if (!await permissions.IsAllowedAsync(actorId, PermissionKeys.ViewCourses, ct))
            throw new UnauthorizedAccessException();
        return await store.TracksAsync(ct);
    }

    public async Task<TrainingTrack> SaveTrackAsync(Guid actorId, Guid? id, TrackInput input, string? ip,
        CancellationToken ct)
    {
        await RequireTrackManagerAsync(actorId, ct);
        if (string.IsNullOrWhiteSpace(input.NameAr) || input.NameAr.Length > 300 || input.NameEn?.Length > 300 ||
            input.ObjectivesAr?.Length > 2000 || input.ObjectivesEn?.Length > 2000)
            throw new ArgumentException("Invalid training track details.");
        var nameAr = input.NameAr.Trim();
        if (await store.TrackNameExistsAsync(id, nameAr, ct))
            throw new InvalidOperationException("A training track with this Arabic name already exists.");

        TrainingTrack track;
        if (id.HasValue)
        {
            track = await store.TrackAsync(id.Value, ct) ?? throw new KeyNotFoundException("Track not found.");
        }
        else
        {
            track = new TrainingTrack
            {
                NameAr = nameAr, ObjectivesAr = input.ObjectivesAr?.Trim() ?? nameAr
            };
            store.AddTrack(track);
        }
        track.NameAr = nameAr;
        track.NameEn = Clean(input.NameEn);
        track.ObjectivesAr = Clean(input.ObjectivesAr) ?? track.ObjectivesAr;
        track.ObjectivesEn = Clean(input.ObjectivesEn);
        track.IsActive = true;
        store.AddAudit(new global::ZU.Training.Domain.Settings.AuditEvent
        {
            ActorUserId = actorId, ActionKey = id.HasValue ? "track.updated" : "track.created",
            EntityType = "TrainingTrack", EntityId = track.Id, IpAddress = ip, Reason = track.NameAr
        });
        await store.SaveAsync(ct);
        return track;
    }

    public async Task<TrainingTrack> SetTrackActiveAsync(Guid actorId, Guid id, bool active, string? ip,
        CancellationToken ct)
    {
        await RequireTrackManagerAsync(actorId, ct);
        var track = await store.TrackAsync(id, ct) ?? throw new KeyNotFoundException("Track not found.");
        track.IsActive = active;
        store.AddAudit(new global::ZU.Training.Domain.Settings.AuditEvent
        {
            ActorUserId = actorId, ActionKey = active ? "track.restored" : "track.disabled",
            EntityType = "TrainingTrack", EntityId = track.Id, IpAddress = ip, Reason = track.NameAr
        });
        await store.SaveAsync(ct);
        return track;
    }

    private async Task RequireTrackManagerAsync(Guid actorId, CancellationToken ct)
    {
        if (!await permissions.IsAllowedAsync(actorId, PermissionKeys.ManageTracks, ct))
            throw new UnauthorizedAccessException();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task RequireAdminAsync(Guid actorId, CancellationToken ct)
    {
        if (!await permissions.IsAllowedAsync(actorId, PermissionKeys.ManageAllCourses, ct) ||
            !(await auth.GetRoleKeysAsync(actorId, ct)).Contains("ADMIN", StringComparer.Ordinal))
            throw new UnauthorizedAccessException();
    }
}
