using ZU.Training.Application.Communication;
using ZU.Training.Application.Security;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Settings;

namespace ZU.Training.Application.Administration;

public sealed record CredentialDeliveryRow(Guid UserId, string LoginName, string NameAr, string? Email,
    CredentialEmailStatus Status, DateTimeOffset? SentAtUtc, string? LastError);

public sealed record CredentialDeliveryResult(int Sent, int Failed, int Skipped,
    IReadOnlyList<CredentialDeliveryRow> Rows);

/// <summary>
/// إرسال بيانات الدخول الأولية إلى البريد الجامعي (القسم 10 من المواصفات).
/// الإرسال لا يغيّر كلمة المرور؛ ويُسجَّل نتيجة كل محاولة على الحساب لتفادي افتراض النجاح.
/// </summary>
public sealed class CredentialEmailService(IUserAdminStore users, ICredentialCipher cipher,
    IEmailSender sender, PortalOptions portal)
{
    public async Task<IReadOnlyList<CredentialDeliveryRow>> StatusAsync(CancellationToken ct)
    {
        var rows = await users.SearchUsersAsync(null, 0, 500, ct);
        return rows.OrderBy(x => x.LoginName, StringComparer.Ordinal)
            .Select(x => new CredentialDeliveryRow(x.Id, x.LoginName, x.NameAr, x.Email,
                x.InitialEmailStatus, x.InitialEmailSentAtUtc, x.InitialEmailLastError))
            .ToList();
    }

    public async Task<CredentialDeliveryResult> SendPendingAsync(Guid actorId, string? ip, string? userAgent,
        CancellationToken ct)
    {
        var candidates = (await users.SearchUsersAsync(null, 0, 500, ct))
            .Where(x => !x.IsExternalTrainer &&
                        x.InitialEmailStatus is CredentialEmailStatus.NotSent or CredentialEmailStatus.Failed)
            .OrderBy(x => x.LoginName, StringComparer.Ordinal)
            .Take(200)
            .ToList();

        var rows = new List<CredentialDeliveryRow>();
        var sent = 0; var failed = 0; var skipped = 0;
        foreach (var user in candidates)
        {
            var row = await SendOneAsync(user, ct);
            rows.Add(row);
            switch (row.Status)
            {
                case CredentialEmailStatus.Sent: sent++; break;
                case CredentialEmailStatus.Failed: failed++; break;
                default: skipped++; break;
            }
        }

        await users.SaveAsync(ct);
        users.AddAudit(new AuditEvent
        {
            ActorUserId = actorId, ActionKey = "credentials.bulk_email", EntityType = "UserAccount",
            IpAddress = ip, UserAgent = userAgent,
            Details = $"sent={sent}; failed={failed}; skipped={skipped}"
        });
        await users.SaveAsync(ct);
        return new CredentialDeliveryResult(sent, failed, skipped, rows);
    }

    public async Task<CredentialDeliveryRow> SendToUserAsync(Guid actorId, Guid userId, string? ip,
        string? userAgent, CancellationToken ct)
    {
        var user = await users.UserAsync(userId, ct) ?? throw new KeyNotFoundException("User not found.");
        var row = await SendOneAsync(user, ct);
        await users.SaveAsync(ct);
        users.AddAudit(new AuditEvent
        {
            ActorUserId = actorId, ActionKey = "credentials.email", EntityType = "UserAccount",
            EntityId = userId, IpAddress = ip, UserAgent = userAgent, Details = row.Status.ToString()
        });
        await users.SaveAsync(ct);
        return row;
    }

    private async Task<CredentialDeliveryRow> SendOneAsync(UserAccount user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return Row(user, CredentialEmailStatus.NotSent, "لا يوجد بريد جامعي مسجل لهذا الحساب.");

        var password = string.IsNullOrWhiteSpace(user.EncryptedInitialPassword)
            ? null
            : SafeDecrypt(user.EncryptedInitialPassword);
        if (password is null)
            return Row(user, CredentialEmailStatus.NotSent, "لا توجد كلمة مرور أولية محفوظة.");

        var message = EmailTemplates.InitialCredentials(user.Email!, user.NameAr, user.LoginName, password,
            portal.PortalBaseUrl);
        var outcome = await sender.SendAsync(message, ct);
        user.InitialEmailStatus = outcome switch
        {
            EmailOutcome.Sent => CredentialEmailStatus.Sent,
            EmailOutcome.Uncertain => CredentialEmailStatus.Uncertain,
            _ => CredentialEmailStatus.Failed
        };
        if (outcome == EmailOutcome.Sent) user.InitialEmailSentAtUtc = DateTimeOffset.UtcNow;
        user.InitialEmailLastError = outcome == EmailOutcome.Sent
            ? null
            : outcome == EmailOutcome.Uncertain ? "تعذر تأكيد الإرسال." : "فشل الإرسال عبر خادم البريد.";
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Row(user, user.InitialEmailStatus, user.InitialEmailLastError);
    }

    private string? SafeDecrypt(string value)
    {
        try { return cipher.Decrypt(value); }
        catch (Exception) { return null; }
    }

    private static CredentialDeliveryRow Row(UserAccount user, CredentialEmailStatus status, string? error)
        => new(user.Id, user.LoginName, user.NameAr, user.Email, status, user.InitialEmailSentAtUtc, error);
}
