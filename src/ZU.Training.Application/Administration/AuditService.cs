using ZU.Training.Application.Security;

namespace ZU.Training.Application.Administration;

public sealed record AuditRow(Guid Id, DateTimeOffset CreatedAtUtc, Guid? ActorUserId, string? ActorName,
    string ActionKey, string EntityType, Guid? EntityId, string? Reason, string? IpAddress, string? UserAgent,
    string? Details);

public sealed record AuditPage(IReadOnlyList<AuditRow> Items, int Total, int Page, int PageSize);

public interface IAuditStore
{
    Task<IReadOnlyList<AuditRow>> QueryAsync(string? action, string? entityType, Guid? actorId,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int skip, int take, CancellationToken ct);
    Task<int> CountAsync(string? action, string? entityType, Guid? actorId, DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc, CancellationToken ct);
    Task<IReadOnlyList<AuditRow>> LatestAsync(int take, CancellationToken ct);
}

/// <summary>سجل التدقيق (القسم 12): استعلام مُقيد بالصلاحية وبث مباشر لأحدث الإجراءات.</summary>
public sealed class AuditService(IAuditStore store, IPermissionAccess permissions)
{
    public async Task<AuditPage> QueryAsync(Guid viewerId, string? action, string? entityType, Guid? actorId,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int page, int pageSize, CancellationToken ct)
    {
        await RequireAsync(viewerId, PermissionKeys.ViewAuditLog, ct);
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;
        var items = await store.QueryAsync(Clean(action), Clean(entityType), actorId, fromUtc, toUtc,
            (page - 1) * pageSize, pageSize, ct);
        var total = await store.CountAsync(Clean(action), Clean(entityType), actorId, fromUtc, toUtc, ct);
        return new AuditPage(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<AuditRow>> LiveFeedAsync(Guid viewerId, int take, CancellationToken ct)
    {
        await RequireAsync(viewerId, PermissionKeys.ViewAuditLog, ct);
        return await store.LatestAsync(take is < 1 or > 200 ? 40 : take, ct);
    }

    private async Task RequireAsync(Guid userId, string key, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new UnauthorizedAccessException();
        if (!await permissions.IsAllowedAsync(userId, key, ct)) throw new UnauthorizedAccessException();
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
