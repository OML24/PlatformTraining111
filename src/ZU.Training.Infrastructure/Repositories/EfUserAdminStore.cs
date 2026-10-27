using Microsoft.EntityFrameworkCore;
using ZU.Training.Application.Administration;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Settings;
using ZU.Training.Infrastructure.Data;

namespace ZU.Training.Infrastructure.Repositories;

public sealed class EfUserAdminStore(TrainingDbContext db) : IUserAdminStore
{
    public Task<UserAccount?> UserAsync(Guid id, CancellationToken ct)
        => db.UserAccounts.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> LoginExistsAsync(string loginName, CancellationToken ct)
        => db.UserAccounts.AnyAsync(x => x.LoginName == loginName, ct);

    public async Task<IReadOnlyList<UserAccount>> SearchUsersAsync(string query, int skip, int take, CancellationToken ct)
        => await db.UserAccounts.AsNoTracking()
            .Where(x => query == "" || EF.Functions.ILike(x.NameAr, $"%{query}%") ||
                EF.Functions.ILike(x.LoginName, $"%{query}%"))
            .OrderBy(x => x.LoginName).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<IReadOnlyList<Role>> RolesAsync(CancellationToken ct)
        => await db.Roles.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Key).ToListAsync(ct);

    public async Task<IReadOnlyList<Permission>> PermissionsAsync(CancellationToken ct)
        => await db.Permissions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Key).ToListAsync(ct);

    public async Task<IReadOnlyList<UserRole>> UserRolesAsync(Guid userId, CancellationToken ct)
        => await db.UserRoles.Where(x => x.UserId == userId).ToListAsync(ct);

    public async Task<IReadOnlyList<RolePermission>> RolePermissionsAsync(CancellationToken ct)
        => await db.RolePermissions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);

    public Task<RolePermission?> RolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct)
        => db.RolePermissions.SingleOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct);

    public async Task<IReadOnlyList<UserPermissionOverride>> OverridesAsync(Guid userId, CancellationToken ct)
        => await db.UserPermissionOverrides.Where(x => x.UserId == userId).ToListAsync(ct);

    public Task<Permission?> PermissionAsync(string key, CancellationToken ct)
        => db.Permissions.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key && x.IsActive, ct);

    public Task<PasswordPolicy?> CurrentPolicyAsync(CancellationToken ct)
        => db.PasswordPolicies.AsNoTracking().Where(x => x.IsActive)
            .OrderByDescending(x => x.EffectiveAtUtc).FirstOrDefaultAsync(ct);

    public Task<SharedInitialPassword?> SharedPasswordAsync(CancellationToken ct)
        => db.SharedInitialPasswords.Where(x => x.IsActive).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<UserAccount>> UsersForCredentialEmailAsync(CancellationToken ct)
        => await db.UserAccounts.AsNoTracking()
            .Where(x => !x.IsExternalTrainer && x.IsActive && x.EmployeeId != null)
            .OrderBy(x => x.LoginName).ToListAsync(ct);

    public void AddUser(UserAccount user) => db.UserAccounts.Add(user);
    public void AddUserRole(UserRole role) => db.UserRoles.Add(role);
    public void AddRolePermission(RolePermission value) => db.RolePermissions.Add(value);
    public void AddOverride(UserPermissionOverride value) => db.UserPermissionOverrides.Add(value);
    public void AddSharedPassword(SharedInitialPassword value) => db.SharedInitialPasswords.Add(value);
    public void AddAudit(AuditEvent value) => db.AuditEvents.Add(value);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
