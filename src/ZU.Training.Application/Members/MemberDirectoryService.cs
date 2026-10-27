using ZU.Training.Application.Security;
using ZU.Training.Application.Training;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Members;

namespace ZU.Training.Application.Members;

public sealed record MemberSummary(Guid Id, string NameAr, string? NameEn, string LoginName,
    Guid? FacultyId, string? FacultyNameAr, string? FacultyNameEn, Guid? DepartmentId,
    string? DepartmentNameAr, string? DepartmentNameEn, string? ExpertiseAr, string? ExpertiseEn,
    bool HasPhoto, int ValidCertificates);

public sealed record MemberDetail(MemberSummary Member, string? BioAr, string? BioEn, string? ContactEmail,
    string? Phone, string? OfficeAr, IReadOnlyList<CertificateCard> Certificates);

public sealed record MemberProfileInput(string? BioAr, string? BioEn, string? ExpertiseAr, string? ExpertiseEn,
    string? ContactEmail, string? Phone, string? OfficeAr);

public sealed record MemberSearchResult(IReadOnlyList<MemberSummary> Items, int Total);

public interface IMemberDirectoryStore
{
    Task<IReadOnlyList<UserAccount>> SearchAsync(string? query, Guid? facultyId, Guid? departmentId,
        int skip, int take, CancellationToken ct);
    Task<int> CountAsync(string? query, Guid? facultyId, Guid? departmentId, CancellationToken ct);
    Task<UserAccount?> UserAsync(Guid id, CancellationToken ct);
    Task<MemberProfile?> ProfileAsync(Guid userId, CancellationToken ct);
    Task<Faculty?> FacultyAsync(Guid id, CancellationToken ct);
    Task<Department?> DepartmentAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Faculty>> FacultiesAsync(CancellationToken ct);
    Task<IReadOnlyList<Department>> DepartmentsAsync(Guid? facultyId, CancellationToken ct);
    Task<int> ValidCertificateCountAsync(Guid userId, CancellationToken ct);
    void AddProfile(MemberProfile profile);
    Task SaveAsync(CancellationToken ct);
}

/// <summary>دليل الأعضاء والملف الشخصي المشترك (القسم الخاص بالملفات الشخصية في المواصفات).</summary>
public sealed class MemberDirectoryService(IMemberDirectoryStore store,
    CertificateIssuanceService certificates, IPermissionAccess permissions)
{
    public async Task<MemberSearchResult> SearchAsync(Guid actorId, string? query, Guid? facultyId,
        Guid? departmentId, int page, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewMemberDirectory, ct);
        page = page is < 1 or > 10000 ? 1 : page;
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        if (query is { Length: < 2 }) query = null;
        if (query is { Length: > 100 }) query = query[..100];
        var rows = await store.SearchAsync(query, facultyId, departmentId, (page - 1) * 20, 20, ct);
        var items = new List<MemberSummary>();
        foreach (var user in rows) items.Add(await SummaryAsync(user, ct));
        return new MemberSearchResult(items,
            await store.CountAsync(query, facultyId, departmentId, ct));
    }

    public async Task<MemberDetail> DetailAsync(Guid actorId, Guid userId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewMemberDirectory, ct);
        var user = await store.UserAsync(userId, ct) ?? throw new KeyNotFoundException("User not found.");
        if (!user.IsActive) throw new KeyNotFoundException("User not found.");
        var profile = await store.ProfileAsync(userId, ct);
        var issued = await certificates.ProfileAsync(actorId, userId, ct);
        return new MemberDetail(await SummaryAsync(user, ct), profile?.BioAr, profile?.BioEn,
            profile?.ContactEmail, profile?.Phone, profile?.OfficeAr, issued.Certificates);
    }

    public async Task<MemberDetail> MyProfileAsync(Guid actorId, CancellationToken ct)
    {
        var user = await store.UserAsync(actorId, ct) ?? throw new UnauthorizedAccessException();
        var profile = await store.ProfileAsync(actorId, ct);
        var mine = await certificates.MineAsync(actorId, ct);
        return new MemberDetail(await SummaryAsync(user, ct), profile?.BioAr, profile?.BioEn,
            profile?.ContactEmail, profile?.Phone, profile?.OfficeAr, mine);
    }

    public async Task<MemberCertificateProfile> MyCertificatesAsync(Guid actorId, CancellationToken ct)
        => await certificates.ProfileAsync(actorId, actorId, ct);

    public async Task SaveMyProfileAsync(Guid actorId, MemberProfileInput input, CancellationToken ct)
    {
        EnsureLength(input.BioAr, 4000, "النبذة");
        EnsureLength(input.BioEn, 4000, "Bio");
        EnsureLength(input.ExpertiseAr, 1000, "مجالات الخبرة");
        EnsureLength(input.ExpertiseEn, 1000, "Expertise");
        EnsureLength(input.ContactEmail, 320, "Email");
        EnsureLength(input.Phone, 40, "Phone");
        EnsureLength(input.OfficeAr, 200, "Office");
        if (!string.IsNullOrWhiteSpace(input.ContactEmail) && !input.ContactEmail.Contains('@'))
            throw new ArgumentException("Invalid contact email.");

        var profile = await store.ProfileAsync(actorId, ct);
        if (profile is null)
        {
            profile = new MemberProfile { UserId = actorId };
            store.AddProfile(profile);
        }
        profile.BioAr = Clean(input.BioAr);
        profile.BioEn = Clean(input.BioEn);
        profile.ExpertiseAr = Clean(input.ExpertiseAr);
        profile.ExpertiseEn = Clean(input.ExpertiseEn);
        profile.ContactEmail = Clean(input.ContactEmail);
        profile.Phone = Clean(input.Phone);
        profile.OfficeAr = Clean(input.OfficeAr);
        await store.SaveAsync(ct);
    }

    public async Task<IReadOnlyList<Faculty>> FacultiesAsync(Guid actorId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewMemberDirectory, ct);
        return await store.FacultiesAsync(ct);
    }

    public async Task<IReadOnlyList<Department>> DepartmentsAsync(Guid actorId, Guid? facultyId,
        CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewMemberDirectory, ct);
        return await store.DepartmentsAsync(facultyId, ct);
    }

    private async Task<MemberSummary> SummaryAsync(UserAccount user, CancellationToken ct)
    {
        var faculty = user.FacultyId.HasValue ? await store.FacultyAsync(user.FacultyId.Value, ct) : null;
        var department = user.DepartmentId.HasValue ? await store.DepartmentAsync(user.DepartmentId.Value, ct) : null;
        var profile = await store.ProfileAsync(user.Id, ct);
        return new MemberSummary(user.Id, user.NameAr, user.NameEn, user.LoginName, user.FacultyId,
            faculty?.NameAr, faculty?.NameEn, user.DepartmentId, department?.NameAr, department?.NameEn,
            profile?.ExpertiseAr, profile?.ExpertiseEn, !string.IsNullOrWhiteSpace(profile?.PhotoStorageKey),
            await store.ValidCertificateCountAsync(user.Id, ct));
    }

    private async Task RequireAsync(Guid actorId, string permission, CancellationToken ct)
    {
        if (actorId == Guid.Empty || !await permissions.IsAllowedAsync(actorId, permission, ct))
            throw new UnauthorizedAccessException();
    }

    private static void EnsureLength(string? value, int max, string label)
    {
        if (value is not null && value.Length > max)
            throw new ArgumentException($"الحقل «{label}» يتجاوز الطول المسموح.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
