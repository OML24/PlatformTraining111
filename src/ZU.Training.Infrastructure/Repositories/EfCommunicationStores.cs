using Microsoft.EntityFrameworkCore;
using ZU.Training.Application.Administration;
using ZU.Training.Application.Communication;
using ZU.Training.Domain.Communication;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Settings;
using ZU.Training.Domain.Training;
using ZU.Training.Infrastructure.Data;

namespace ZU.Training.Infrastructure.Repositories;

public sealed class EfNotificationStore(TrainingDbContext db) : INotificationStore
{
    public async Task<IReadOnlyList<Notification>> ForUserAsync(Guid userId, int take, CancellationToken ct)
        => await db.Notifications.Where(x => x.UserId == userId && x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc).Take(take).ToListAsync(ct);

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct)
        => db.Notifications.CountAsync(x => x.UserId == userId && x.IsActive && x.ReadAtUtc == null, ct);

    public Task<Notification?> FindAsync(Guid id, Guid userId, CancellationToken ct)
        => db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);

    public void Add(Notification notification) => db.Notifications.Add(notification);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class EfAuditStore(TrainingDbContext db) : IAuditStore
{
    public async Task<IReadOnlyList<AuditRow>> QueryAsync(string? action, string? entityType, Guid? actorId,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int skip, int take, CancellationToken ct)
    {
        var rows = await Filter(action, entityType, actorId, fromUtc, toUtc)
            .OrderByDescending(x => x.CreatedAtUtc).Skip(skip).Take(take).ToListAsync(ct);
        return await MapAsync(rows, ct);
    }

    public Task<int> CountAsync(string? action, string? entityType, Guid? actorId, DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc, CancellationToken ct)
        => Filter(action, entityType, actorId, fromUtc, toUtc).CountAsync(ct);

    public async Task<IReadOnlyList<AuditRow>> LatestAsync(int take, CancellationToken ct)
    {
        var rows = await db.AuditEvents.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc)
            .Take(take).ToListAsync(ct);
        return await MapAsync(rows, ct);
    }

    private IQueryable<AuditEvent> Filter(string? action, string? entityType, Guid? actorId,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        var query = db.AuditEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(x => EF.Functions.ILike(x.ActionKey, $"%{action.Trim()}%"));
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(x => x.EntityType == entityType.Trim());
        if (actorId.HasValue) query = query.Where(x => x.ActorUserId == actorId);
        if (fromUtc.HasValue) query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);
        return query;
    }

    private async Task<IReadOnlyList<AuditRow>> MapAsync(IReadOnlyList<AuditEvent> rows, CancellationToken ct)
    {
        var ids = rows.Where(x => x.ActorUserId.HasValue).Select(x => x.ActorUserId!.Value).Distinct().ToList();
        var names = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.UserAccounts.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new { x.Id, x.NameAr }).ToDictionaryAsync(x => x.Id, x => x.NameAr, ct);
        return rows.Select(x => new AuditRow(x.Id, x.CreatedAtUtc, x.ActorUserId,
            x.ActorUserId.HasValue && names.TryGetValue(x.ActorUserId.Value, out var name) ? name : null,
            x.ActionKey, x.EntityType, x.EntityId, x.Reason, x.IpAddress, x.UserAgent, x.Details)).ToList();
    }
}

public sealed class EfExternalTrainerStore(TrainingDbContext db) : IExternalTrainerStore
{
    public Task<UserAccount?> UserAsync(Guid id, CancellationToken ct)
        => db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<Course?> CourseAsync(Guid id, CancellationToken ct)
        => db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

    public async Task<IReadOnlyList<ExternalTrainerRequest>> RequestsAsync(CancellationToken ct)
        => await db.ExternalTrainerRequests.Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    public Task<ExternalTrainerRequest?> RequestAsync(Guid id, CancellationToken ct)
        => db.ExternalTrainerRequests.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

    public async Task<IReadOnlyList<UserAccount>> AdminsAsync(CancellationToken ct)
        => await (from userRole in db.UserRoles
                  join role in db.Roles on userRole.RoleId equals role.Id
                  join user in db.UserAccounts on userRole.UserId equals user.Id
                  where role.Key == "ADMIN" && userRole.IsActive && role.IsActive && user.IsActive
                  select user).Distinct().ToListAsync(ct);

    public Task<PasswordPolicy?> PolicyAsync(CancellationToken ct)
        => db.PasswordPolicies.AsNoTracking().Where(x => x.IsActive)
            .OrderByDescending(x => x.EffectiveAtUtc).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var rows = await db.UserAccounts.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.NameAr }).ToListAsync(ct);
        return rows.ToDictionary(x => x.Id, x => x.NameAr);
    }

    public void AddAudit(AuditEvent audit) => db.AuditEvents.Add(audit);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
