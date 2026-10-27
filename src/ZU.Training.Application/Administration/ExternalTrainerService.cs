using System.Security.Cryptography;
using ZU.Training.Application.Communication;
using ZU.Training.Application.Security;
using ZU.Training.Domain.Communication;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Settings;
using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Administration;

public sealed record ExternalTrainerRequestInput(string TrainerNameAr, string? TrainerNameEn, string Email,
    string? OrganizationAr, string? NoteAr, Guid? CourseId);

public sealed record ExternalTrainerRequestView(Guid Id, string TrainerNameAr, string? TrainerNameEn,
    string Email, string? OrganizationAr, string? NoteAr, ExternalTrainerRequestStatus Status,
    Guid RequestedByUserId, string? RequestedByName, DateTimeOffset CreatedAtUtc,
    Guid? HandledByUserId, DateTimeOffset? HandledAtUtc, string? CreatedLoginName, Guid? CourseId);

public sealed record CreateExternalAccountInput(string LoginName, string Password);

public interface IExternalTrainerStore
{
    Task<UserAccount?> UserAsync(Guid id, CancellationToken ct);
    Task<Course?> CourseAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ExternalTrainerRequest>> RequestsAsync(CancellationToken ct);
    Task<ExternalTrainerRequest?> RequestAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<UserAccount>> AdminsAsync(CancellationToken ct);
    Task<PasswordPolicy?> PolicyAsync(CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    void AddAudit(AuditEvent audit);
    Task SaveAsync(CancellationToken ct);
}

/// <summary>
/// حلقة إدارة حسابات المدربين الخارجيين (القسم 8): الجودة تطلب، والأدمن ينشئ حساباً برقم دخول
/// من أربعة أرقام وكلمة مرور، ثم تُرسل البيانات للبريد المذكور في الطلب.
/// </summary>
public sealed class ExternalTrainerService(IExternalTrainerStore store, UserAdminService users,
    IEmailSender email, PortalOptions portal, IPermissionAccess permissions, NotificationService notifications)
{
    public async Task<IReadOnlyList<ExternalTrainerRequestView>> ListAsync(Guid actorId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.RequestExternalTrainer, ct);
        var rows = await store.RequestsAsync(ct);
        var names = await store.NamesAsync(rows.Select(x => x.RequestedByUserId).Distinct().ToList(), ct);
        return rows.OrderByDescending(x => x.CreatedAtUtc).Select(x => new ExternalTrainerRequestView(
            x.Id, x.TrainerNameAr, x.TrainerNameEn, x.Email, x.OrganizationAr, x.NoteAr, x.Status,
            x.RequestedByUserId, names.GetValueOrDefault(x.RequestedByUserId), x.CreatedAtUtc,
            x.HandledByUserId, x.HandledAtUtc, x.CreatedLoginName, x.CourseId)).ToList();
    }

    public async Task<ExternalTrainerRequest> RequestAsync(Guid actorId, ExternalTrainerRequestInput input,
        string? ip, string? userAgent, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.RequestExternalTrainer, ct);
        if (string.IsNullOrWhiteSpace(input.TrainerNameAr) || input.TrainerNameAr.Length > 300 ||
            input.TrainerNameEn?.Length > 300)
            throw new ArgumentException("Trainer name is required.");
        if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 320 || !input.Email.Contains('@'))
            throw new ArgumentException("A valid trainer email is required.");
        if (input.NoteAr?.Length > 2000 || input.OrganizationAr?.Length > 300)
            throw new ArgumentException("One of the fields is too long.");
        if (input.CourseId.HasValue && await store.CourseAsync(input.CourseId.Value, ct) is null)
            throw new ArgumentException("Course not found.");

        var request = new ExternalTrainerRequest
        {
            RequestedByUserId = actorId,
            TrainerNameAr = input.TrainerNameAr.Trim(),
            TrainerNameEn = Clean(input.TrainerNameEn),
            Email = input.Email.Trim(),
            OrganizationAr = Clean(input.OrganizationAr),
            NoteAr = Clean(input.NoteAr),
            CourseId = input.CourseId
        };
        store.AddRequest(request);
        store.AddAudit(new AuditEvent
        {
            ActorUserId = actorId, ActionKey = "external_trainer.requested", EntityType = "ExternalTrainerRequest",
            EntityId = request.Id, IpAddress = ip, UserAgent = userAgent, Reason = request.Email
        });
        await store.SaveAsync(ct);
        await notifications.NotifyManyAsync((await store.AdminsAsync(ct)).Select(x => x.Id),
            NotificationKinds.ExternalTrainerRequested, "طلب حساب محاضر خارجي",
            "External trainer account request",
            "طلبت الجودة إنشاء حساب للمحاضر: " + request.TrainerNameAr,
            "Quality requested an account for: " + request.TrainerNameAr, "/admin.html#external-trainers", ct);
        return request;
    }

    /// <summary>ينشئ الأدمن الحساب ويعيده للجودة، ويرسل البيانات للبريد المذكور في الطلب.</summary>
    public async Task<ExternalTrainerRequestView> CreateAccountAsync(Guid actorId, Guid requestId,
        CreateExternalAccountInput input, string? ip, string? userAgent, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ManageUsers, ct);
        var request = await store.RequestAsync(requestId, ct)
            ?? throw new KeyNotFoundException("Request not found.");
        if (request.Status != ExternalTrainerRequestStatus.Pending)
            throw new InvalidOperationException("This request has already been handled.");

        var login = (input.LoginName ?? string.Empty).Trim();
        if (login.Length != 4 || !login.All(char.IsAsciiDigit))
            throw new ArgumentException("External trainer login must contain four digits.");

        var created = await users.CreateAsync(actorId, new CreateUserInput(null, login, request.TrainerNameAr,
            request.TrainerNameEn, request.Email, true, input.Password, null, null,
            new[] { "EXTERNAL_INSTRUCTOR" }), ip, ct);

        request.Status = ExternalTrainerRequestStatus.AccountCreated;
        request.HandledByUserId = actorId;
        request.HandledAtUtc = DateTimeOffset.UtcNow;
        request.CreatedLoginName = created.LoginName;
        store.AddAudit(new AuditEvent
        {
            ActorUserId = actorId, ActionKey = "external_trainer.account_created", EntityType = "UserAccount",
            EntityId = created.Id, IpAddress = ip, UserAgent = userAgent, Reason = request.Email
        });
        await store.SaveAsync(ct);

        await email.SendAsync(EmailTemplates.ExternalTrainerAccount(request.Email, request.TrainerNameAr,
            created.LoginName, input.Password, portal.PortalBaseUrl), ct);
        await notifications.NotifyAsync(request.RequestedByUserId, NotificationKinds.ExternalTrainerCreated,
            "تم إنشاء حساب المحاضر الخارجي", "External trainer account created",
            "اسم الدخول: " + created.LoginName, "Login: " + created.LoginName, "/quality.html#proposals", ct);

        return (await ListAsync(actorId, ct)).First(x => x.Id == request.Id);
    }

    public async Task<ExternalTrainerRequestView> RejectAsync(Guid actorId, Guid requestId, string? noteAr,
        string? ip, string? userAgent, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ManageUsers, ct);
        var request = await store.RequestAsync(requestId, ct) ?? throw new KeyNotFoundException("Request not found.");
        if (request.Status != ExternalTrainerRequestStatus.Pending)
            throw new InvalidOperationException("This request has already been handled.");
        request.Status = ExternalTrainerRequestStatus.Rejected;
        request.HandledByUserId = actorId;
        request.HandledAtUtc = DateTimeOffset.UtcNow;
        request.NoteAr = Clean(noteAr) ?? request.NoteAr;
        store.AddAudit(new AuditEvent
        {
            ActorUserId = actorId, ActionKey = "external_trainer.rejected", EntityType = "ExternalTrainerRequest",
            EntityId = request.Id, IpAddress = ip, UserAgent = userAgent, Reason = request.NoteAr
        });
        await store.SaveAsync(ct);
        await notifications.NotifyAsync(request.RequestedByUserId, NotificationKinds.ExternalTrainerRequested,
            "رفض طلب حساب محاضر خارجي", "External trainer request rejected",
            request.NoteAr, request.NoteAr, "/quality.html#proposals", ct);
        return (await ListAsync(actorId, ct)).First(x => x.Id == request.Id);
    }

    /// <summary>الرقم التالي المتاح من أربعة أرقام (لاقتراحه على الأدمن).</summary>
    public static string SuggestLogin()
        => RandomNumberGenerator.GetInt32(1000, 10000).ToString();

    private async Task RequireAsync(Guid actorId, string permission, CancellationToken ct)
    {
        if (actorId == Guid.Empty || !await permissions.IsAllowedAsync(actorId, permission, ct))
            throw new UnauthorizedAccessException();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
