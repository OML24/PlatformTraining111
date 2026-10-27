using Microsoft.EntityFrameworkCore;
using ZU.Training.Application.Members;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Members;
using ZU.Training.Domain.Training;
using ZU.Training.Infrastructure.Data;

namespace ZU.Training.Infrastructure.Repositories;

public sealed class EfMemberDirectoryStore(TrainingDbContext db) : IMemberDirectoryStore
{
    public async Task<IReadOnlyList<UserAccount>> SearchAsync(string? query, Guid? facultyId, Guid? departmentId,
        int skip, int take, CancellationToken ct)
        => await Filter(query, facultyId, departmentId)
            .OrderBy(x => x.NameAr).Skip(skip).Take(take).ToListAsync(ct);

    public Task<int> CountAsync(string? query, Guid? facultyId, Guid? departmentId, CancellationToken ct)
        => Filter(query, facultyId, departmentId).CountAsync(ct);

    public Task<UserAccount?> UserAsync(Guid id, CancellationToken ct)
        => db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<MemberProfile?> ProfileAsync(Guid userId, CancellationToken ct)
        => db.MemberProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);

    public Task<Faculty?> FacultyAsync(Guid id, CancellationToken ct)
        => db.Faculties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<Department?> DepartmentAsync(Guid id, CancellationToken ct)
        => db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Faculty>> FacultiesAsync(CancellationToken ct)
        => await db.Faculties.AsNoTracking().OrderBy(x => x.NameAr).ToListAsync(ct);

    public async Task<IReadOnlyList<Department>> DepartmentsAsync(Guid? facultyId, CancellationToken ct)
    {
        var query = db.Departments.AsNoTracking().AsQueryable();
        if (facultyId.HasValue) query = query.Where(x => x.FacultyId == facultyId);
        return await query.OrderBy(x => x.NameAr).ToListAsync(ct);
    }

    public Task<int> ValidCertificateCountAsync(Guid userId, CancellationToken ct)
        => (from certificate in db.Certificates.AsNoTracking()
            join enrollment in db.CourseEnrollments.AsNoTracking() on certificate.EnrollmentId equals enrollment.Id
            where enrollment.UserId == userId && certificate.Status == CertificateStatus.Issued && certificate.IsActive
            select certificate.Id).CountAsync(ct);

    public void AddProfile(MemberProfile profile) => db.MemberProfiles.Add(profile);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private IQueryable<UserAccount> Filter(string? query, Guid? facultyId, Guid? departmentId)
    {
        var source = db.UserAccounts.AsNoTracking().Where(x => x.IsActive && !x.IsArchived);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            source = source.Where(x => EF.Functions.ILike(x.NameAr, $"%{term}%")
                || (x.NameEn != null && EF.Functions.ILike(x.NameEn, $"%{term}%"))
                || EF.Functions.ILike(x.LoginName, $"%{term}%"));
        }
        if (facultyId.HasValue) source = source.Where(x => x.FacultyId == facultyId);
        if (departmentId.HasValue) source = source.Where(x => x.DepartmentId == departmentId);
        return source;
    }
}
