using ZU.Training.Domain.Common;

namespace ZU.Training.Domain.Identity;

public sealed class Faculty : Entity
{
    public required string ExternalCode { get; set; }
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
}

public sealed class Department : Entity
{
    public required string ExternalCode { get; set; }
    public required string FacultyExternalCode { get; set; }
    public Guid? FacultyId { get; set; }
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
}

public sealed class UserAccount : Entity
{
    public required string LoginName { get; set; }
    public int? EmployeeId { get; set; }
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
    public string? Email { get; set; }
    public bool IsExternalTrainer { get; set; }
    public bool FirstLoginCompleted { get; set; }
    public string? EncryptedInitialPassword { get; set; }
    public required string EncryptedCurrentPassword { get; set; }
    public DateTimeOffset? InitialEmailSentAtUtc { get; set; }
    public CredentialEmailStatus InitialEmailStatus { get; set; } = CredentialEmailStatus.NotSent;
    public string? InitialEmailLastError { get; set; }
    public int? SourceEmploymentStatusCode { get; set; }
    public int? SourceEmploymentCheck { get; set; }
    public int? SourceMainJobCode { get; set; }
    public int? SourceSideCode { get; set; }
    public string? SourceDepartmentId { get; set; }
    public string? SourceCenterId { get; set; }
    public DateTimeOffset? SourceLastSeenAtUtc { get; set; }
    public DateTimeOffset? SourceMissingSinceUtc { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool AccessOverrideAllow { get; set; }
    public int AuthVersion { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
}

public enum CredentialEmailStatus
{
    NotSent = 0,
    Sent = 1,
    Failed = 2,
    Uncertain = 3
}

public sealed class SharedInitialPassword : Entity
{
    public required string EncryptedPassword { get; set; }
}

public sealed class Role : Entity
{
    public required string Key { get; set; }
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
}

public sealed class Permission : Entity
{
    public required string Key { get; set; }
    public required string NameAr { get; set; }
    public string? NameEn { get; set; }
}

public sealed class UserRole : Entity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

public sealed class RolePermission : Entity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}

public enum PermissionOverrideMode { Inherit = 0, Allow = 1, Deny = 2 }

public sealed class UserPermissionOverride : Entity
{
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }
    public PermissionOverrideMode Mode { get; set; }
}
