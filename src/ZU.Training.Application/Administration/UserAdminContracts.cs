using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Settings;

namespace ZU.Training.Application.Administration;

public sealed record CreateUserInput(int? EmployeeId, string? ExternalLoginName, string NameAr,
    string? NameEn, string? Email, bool IsExternalTrainer, string? ExternalPassword,
    Guid? FacultyId, Guid? DepartmentId, IReadOnlyList<string> RoleKeys);

public sealed record UserAdminSummary(Guid Id, string LoginName, string NameAr, string? NameEn,
    string? Email, bool IsActive, bool IsExternalTrainer, bool FirstLoginCompleted,
    int? SourceEmploymentStatusCode, bool AccessOverrideAllow, Guid? FacultyId,
    IReadOnlyCollection<string> Roles);

public sealed record UserCredentialView(string? InitialPassword, string CurrentPassword);
public sealed record PermissionState(string Key, string NameAr, string? NameEn,
    bool Inherited, PermissionOverrideMode OverrideMode, bool Effective);
public sealed record RolePermissionState(string Key, string NameAr, string? NameEn, bool Granted);

public interface IUserAdminStore
{
    Task<UserAccount?> UserAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> LoginExistsAsync(string loginName, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAccount>> SearchUsersAsync(string query, int skip, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<Role>> RolesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Permission>> PermissionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<UserRole>> UserRolesAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RolePermission>> RolePermissionsAsync(CancellationToken cancellationToken);
    Task<RolePermission?> RolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserPermissionOverride>> OverridesAsync(Guid userId, CancellationToken cancellationToken);
    Task<Permission?> PermissionAsync(string key, CancellationToken cancellationToken);
    Task<PasswordPolicy?> CurrentPolicyAsync(CancellationToken cancellationToken);
    Task<SharedInitialPassword?> SharedPasswordAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAccount>> UsersForCredentialEmailAsync(CancellationToken cancellationToken);
    void AddUser(UserAccount user);
    void AddUserRole(UserRole role);
    void AddRolePermission(RolePermission value);
    void AddOverride(UserPermissionOverride value);
    void AddSharedPassword(SharedInitialPassword value);
    void AddAudit(AuditEvent value);
    Task SaveAsync(CancellationToken cancellationToken);
}
