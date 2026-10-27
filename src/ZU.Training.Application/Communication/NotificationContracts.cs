using ZU.Training.Domain.Communication;

namespace ZU.Training.Application.Communication;

/// <summary>عناوين بوابة الويب المستخدمة في الرسائل والروابط الداخلية.</summary>
public sealed record PortalOptions(string PortalBaseUrl, string VerificationBaseUrl);

public sealed record NotificationView(Guid Id, string Kind, string TitleAr, string? TitleEn, string? BodyAr,
    string? BodyEn, string? LinkUrl, bool IsRead, DateTimeOffset CreatedAtUtc);

public interface INotificationStore
{
    Task<IReadOnlyList<Notification>> ForUserAsync(Guid userId, int take, CancellationToken ct);
    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct);
    Task<Notification?> FindAsync(Guid id, Guid userId, CancellationToken ct);
    void Add(Notification notification);
    Task SaveAsync(CancellationToken ct);
}

/// <summary>إشعارات داخلية للمستخدمين مع عدّاد غير المقروء (القسم 9 من المواصفات).</summary>
public sealed class NotificationService(INotificationStore store)
{
    public async Task NotifyAsync(Guid userId, string kind, string titleAr, string? titleEn, string? bodyAr,
        string? bodyEn, string? linkUrl, CancellationToken ct)
    {
        if (userId == Guid.Empty) return;
        store.Add(Build(userId, kind, titleAr, titleEn, bodyAr, bodyEn, linkUrl));
        await store.SaveAsync(ct);
    }

    public async Task NotifyManyAsync(IEnumerable<Guid> userIds, string kind, string titleAr, string? titleEn,
        string? bodyAr, string? bodyEn, string? linkUrl, CancellationToken ct)
    {
        var created = false;
        foreach (var userId in userIds.Where(x => x != Guid.Empty).Distinct())
        {
            store.Add(Build(userId, kind, titleAr, titleEn, bodyAr, bodyEn, linkUrl));
            created = true;
        }
        if (created) await store.SaveAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationView>> ListAsync(Guid userId, int take, CancellationToken ct)
    {
        take = take is < 1 or > 100 ? 30 : take;
        var rows = await store.ForUserAsync(userId, take, ct);
        return rows.Select(x => new NotificationView(x.Id, x.Kind, x.TitleAr, x.TitleEn, x.BodyAr, x.BodyEn,
            x.LinkUrl, x.ReadAtUtc.HasValue, x.CreatedAtUtc)).ToList();
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct) => store.UnreadCountAsync(userId, ct);

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var notification = await store.FindAsync(notificationId, userId, ct);
        if (notification is null) return false;
        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc = DateTimeOffset.UtcNow;
            notification.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await store.SaveAsync(ct);
        }
        return true;
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        var rows = await store.ForUserAsync(userId, 200, ct);
        var touched = false;
        foreach (var row in rows.Where(x => x.ReadAtUtc is null))
        {
            row.ReadAtUtc = DateTimeOffset.UtcNow;
            row.UpdatedAtUtc = DateTimeOffset.UtcNow;
            touched = true;
        }
        if (touched) await store.SaveAsync(ct);
    }

    private static Notification Build(Guid userId, string kind, string titleAr, string? titleEn, string? bodyAr,
        string? bodyEn, string? linkUrl) => new()
        {
            UserId = userId,
            Kind = Truncate(kind, 80) ?? "notification",
            TitleAr = Truncate(titleAr, 300) ?? kind,
            TitleEn = Truncate(titleEn, 300),
            BodyAr = Truncate(bodyAr, 2000),
            BodyEn = Truncate(bodyEn, 2000),
            LinkUrl = Truncate(linkUrl, 500)
        };

    private static string? Truncate(string? value, int length)
        => string.IsNullOrWhiteSpace(value) ? null
            : value.Trim().Length <= length ? value.Trim() : value.Trim()[..length];
}
