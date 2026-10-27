using ZU.Training.Domain.Common;

namespace ZU.Training.Domain.Settings;

public sealed class FieldHint : Entity
{
    public required string ScreenKey { get; set; }
    public required string FieldKey { get; set; }
    public required string TextAr { get; set; }
    public string? TextEn { get; set; }
}

public sealed class PasswordPolicy : Entity
{
    public int MinimumLength { get; set; } = 15;
    public int MaximumLength { get; set; } = 128;
    public bool AllowSpaces { get; set; } = true;
    public bool RequireMixedCharacterClasses { get; set; }
    public bool BlockCommonPasswords { get; set; } = true;
    public DateTimeOffset EffectiveAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class IntegrationSyncRun : Entity
{
    public required string SourceKey { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public bool WasComplete { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int MissingCount { get; set; }
    public string? ErrorSummary { get; set; }
}

public sealed class AuditEvent : Entity
{
    public Guid? ActorUserId { get; set; }
    public required string ActionKey { get; set; }
    public required string EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Details { get; set; }
}
