using ZU.Training.Application.Security;
using ZU.Training.Domain.Identity;

namespace ZU.Training.Application.Reporting;

public sealed record StatisticsOverview(int Courses, int ActiveCourses, int EndedCourses, int CancelledCourses,
    int ActiveMembers, int Enrollments, int Lectures, int ValidCertificates, int RevokedCertificates,
    int EvaluationResponses, int ExamPassed, int ExamFailed, double AverageAttendancePercent);

public sealed record FacultyRow(Guid? FacultyId, string NameAr, string? NameEn, int Courses, int Enrollments,
    int Certificates, double AverageAttendancePercent);

public sealed record TrackRow(Guid TrackId, string NameAr, string? NameEn, int Courses, int Enrollments,
    int Certificates);

public sealed record CourseRow(Guid CourseId, string TitleAr, string? TitleEn, int Status, bool HasExam,
    int Lectures, int Enrollments, int Certificates, int ExamPassed, int ExamFailed, int EvaluationResponses,
    double AverageAttendancePercent, DateTimeOffset? EndedAtUtc);

public sealed record MemberRow(Guid UserId, string NameAr, string? NameEn, string LoginName, int Enrollments,
    int Certificates, double AverageAttendancePercent);

public sealed record OptionRow(Guid OptionId, int Position, string TextAr, string? TextEn, int Count,
    double Percent);

public sealed record QuestionRow(Guid QuestionId, int FormVersion, string TextAr, string? TextEn, int Answers,
    IReadOnlyList<OptionRow> Options);

public sealed record EvaluationReport(Guid? VersionId, int Version, int Responses,
    IReadOnlyList<QuestionRow> Questions, double SatisfactionIndex);

public interface IStatisticsStore
{
    Task<StatisticsOverview> OverviewAsync(Guid? facultyId, CancellationToken ct);
    Task<IReadOnlyList<FacultyRow>> FacultyRowsAsync(CancellationToken ct);
    Task<IReadOnlyList<TrackRow>> TrackRowsAsync(CancellationToken ct);
    Task<IReadOnlyList<CourseRow>> CourseRowsAsync(Guid? facultyId, CancellationToken ct);
    Task<IReadOnlyList<MemberRow>> MemberRowsAsync(Guid? facultyId, int take, CancellationToken ct);
    Task<EvaluationReport> EvaluationAsync(Guid? courseId, Guid? versionId, CancellationToken ct);
}

/// <summary>
/// الإحصاءات الشاملة: الجامعة، الكلية، المحور، الدورة، والعضو. تُحسب من بيانات المنصة المحلية.
/// مؤشر الرضا = متوسط موقع الخيار المختار داخل سؤال مرتّب من الأقل إلى الأعلى، كنسبة مئوية.
/// </summary>
public sealed class StatisticsService(IStatisticsStore store, IPermissionAccess permissions)
{
    public async Task<StatisticsOverview> OverviewAsync(Guid actorId, Guid? facultyId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewAllStatistics, ct);
        return await store.OverviewAsync(facultyId, ct);
    }

    public async Task<IReadOnlyList<FacultyRow>> FacultiesAsync(Guid actorId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewAllStatistics, ct);
        return await store.FacultyRowsAsync(ct);
    }

    public async Task<IReadOnlyList<TrackRow>> TracksAsync(Guid actorId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewCourseStatistics, ct);
        return await store.TrackRowsAsync(ct);
    }

    public async Task<IReadOnlyList<CourseRow>> CoursesAsync(Guid actorId, Guid? facultyId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewCourseStatistics, ct);
        return await store.CourseRowsAsync(facultyId, ct);
    }

    public async Task<IReadOnlyList<MemberRow>> MembersAsync(Guid actorId, Guid? facultyId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewAllStatistics, ct);
        return await store.MemberRowsAsync(facultyId, 100, ct);
    }

    public async Task<EvaluationReport> EvaluationAsync(Guid actorId, Guid? courseId, Guid? versionId,
        CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewEvaluationStatistics, ct);
        return await store.EvaluationAsync(courseId, versionId, ct);
    }

    /// <summary>إحصاءات دورة واحدة متاحة للمحاضر المسؤول والجودة والأدمن.</summary>
    public async Task<CourseRow> CourseAsync(Guid actorId, Guid courseId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewCourses, ct);
        var rows = await store.CourseRowsAsync(null, ct);
        return rows.FirstOrDefault(x => x.CourseId == courseId)
            ?? throw new KeyNotFoundException("Course statistics not found.");
    }

    private async Task RequireAsync(Guid actorId, string permission, CancellationToken ct)
    {
        if (actorId == Guid.Empty || !await permissions.IsAllowedAsync(actorId, permission, ct))
            throw new UnauthorizedAccessException();
    }
}

public sealed record PasswordRecoveryRequest(string LoginName, string Email);

/// <summary>«نسيت كلمة المرور»: يتحقق من الرقم الوظيفي وتطابق البريد ثم يرسل الكلمة الحالية للبريد فقط.</summary>
public sealed class PasswordRecoveryService(IAuthStore store,
    Communication.IEmailSender sender, Security.ICredentialCipher cipher)
{
    public async Task RequestAsync(PasswordRecoveryRequest request, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.LoginName) || string.IsNullOrWhiteSpace(request.Email))
            return; // لا نكشف وجود الحساب في أي حال
        var user = await store.FindByLoginAsync(request.LoginName.Trim(), ct);
        if (user is null || user.IsExternalTrainer || string.IsNullOrWhiteSpace(user.Email)) return;
        if (!string.Equals(user.Email.Trim(), request.Email.Trim(), StringComparison.OrdinalIgnoreCase)) return;
        if (!AuthService.CanSignIn(user)) return;

        string password;
        try { password = cipher.Decrypt(user.EncryptedCurrentPassword); }
        catch (Exception) { return; }

        await sender.SendAsync(Communication.EmailTemplates.PasswordReminder(user.Email!, user.NameAr,
            user.LoginName, password), ct);
    }
}
