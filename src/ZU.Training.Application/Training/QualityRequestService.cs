using ZU.Training.Application.Communication;
using ZU.Training.Application.Security;
using ZU.Training.Domain.Communication;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Training;

public sealed class QualityRequestService(IQualityRequestStore store, IOfficialDocumentStorage storage,
    IAuthStore auth, IPermissionAccess permissions, NotificationService notifications)
{
    private sealed record Actor(UserAccount User, HashSet<string> Roles)
    {
        public bool IsAdmin => Roles.Contains("ADMIN");
        public bool IsQuality => IsAdmin || Roles.Contains("QA");
        public bool IsDean => Roles.Contains("DEAN");
    }

    private async Task<Actor> ActorAsync(Guid id, CancellationToken ct)
    {
        var user = await store.UserAsync(id, ct) ?? throw new UnauthorizedAccessException();
        return new Actor(user, (await auth.GetRoleKeysAsync(id, ct)).ToHashSet(StringComparer.Ordinal));
    }

    private async Task RequireAsync(Actor actor, string key, CancellationToken ct)
    {
        if (!await permissions.IsAllowedAsync(actor.User.Id, key, ct)) throw new UnauthorizedAccessException();
    }

    public async Task<IReadOnlyList<TrainingNeedSummary>> NeedsAsync(Guid actorId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.ViewTrainingNeeds, ct);
        var rows = await store.NeedsAsync(ct);
        var visible = actor.IsQuality ? rows : rows.Where(x => x.SubmittedByUserId == actorId).ToList();
        var result = new List<TrainingNeedSummary>();
        foreach (var need in visible)
        {
            var documents = await store.DocumentsAsync("TrainingNeed", need.Id, ct);
            result.Add(new TrainingNeedSummary(need.Id, need.TitleAr, need.TitleEn,
                need.DescriptionAr, need.DescriptionEn, need.FacultyId, need.SubmittedByUserId,
                need.Status, need.ReviewNoteAr, documents.Select(x => x.Id).ToList()));
        }
        return result;
    }

    public async Task<TrainingNeed> SubmitNeedAsync(Guid actorId, TrainingNeedInput input,
        Stream officialDocument, string originalName, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.SubmitTrainingNeeds, ct);
        if (!actor.IsDean && !actor.IsAdmin) throw new UnauthorizedAccessException();
        var facultyId = actor.IsAdmin ? input.FacultyId : actor.User.FacultyId;
        if (!facultyId.HasValue || !await store.FacultyExistsAsync(facultyId.Value, ct))
            throw new ArgumentException("Dean faculty is not assigned.");
        ValidateNeed(input);
        var saved = await storage.SaveAsync(officialDocument, originalName, ct);
        var need = new TrainingNeed
        {
            FacultyId = facultyId.Value, SubmittedByUserId = actorId,
            TitleAr = input.TitleAr.Trim(), TitleEn = Clean(input.TitleEn),
            DescriptionAr = input.DescriptionAr.Trim(), DescriptionEn = Clean(input.DescriptionEn),
            OfficialDocumentStorageKey = saved.StorageKey, OfficialDocumentOriginalName = saved.OriginalName,
            OfficialDocumentSha256 = saved.Sha256
        };
        store.AddNeed(need);
        AddDocument("TrainingNeed", need.Id, "OfficialLetter", saved, actorId);
        await store.SaveAsync(ct); return need;
    }

    public async Task<TrainingNeed> ResubmitNeedAsync(Guid actorId, Guid needId, TrainingNeedInput input,
        Stream? officialDocument, string? originalName, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.SubmitTrainingNeeds, ct);
        var need = await store.NeedAsync(needId, ct) ?? throw new KeyNotFoundException("Training need not found.");
        if (!actor.IsAdmin && need.SubmittedByUserId != actorId) throw new UnauthorizedAccessException();
        if (need.Status != QualityRequestStatus.RevisionRequested)
            throw new InvalidOperationException("Only a returned need can be resubmitted.");
        ValidateNeed(input);
        if (officialDocument is not null)
        {
            var saved = await storage.SaveAsync(officialDocument, originalName ?? "", ct);
            need.OfficialDocumentStorageKey = saved.StorageKey;
            need.OfficialDocumentOriginalName = saved.OriginalName;
            need.OfficialDocumentSha256 = saved.Sha256;
            AddDocument("TrainingNeed", need.Id, "RevisedOfficialLetter", saved, actorId);
        }
        need.TitleAr = input.TitleAr.Trim(); need.TitleEn = Clean(input.TitleEn);
        need.DescriptionAr = input.DescriptionAr.Trim(); need.DescriptionEn = Clean(input.DescriptionEn);
        need.Status = QualityRequestStatus.Submitted;
        await store.SaveAsync(ct); return need;
    }

    public async Task<TrainingNeed> ReviewNeedAsync(Guid actorId, Guid needId, ReviewRequestInput input,
        CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.ReviewTrainingNeeds, ct);
        if (input.Status is not (QualityRequestStatus.Approved or QualityRequestStatus.Rejected or QualityRequestStatus.RevisionRequested))
            throw new ArgumentException("Invalid review decision.");
        if (input.Status != QualityRequestStatus.Approved && string.IsNullOrWhiteSpace(input.ReviewNoteAr))
            throw new ArgumentException("A review note is required.");
        var need = await store.NeedAsync(needId, ct) ?? throw new KeyNotFoundException("Training need not found.");
        if (need.Status != QualityRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted needs can be reviewed.");
        need.Status = input.Status; need.ReviewNoteAr = Clean(input.ReviewNoteAr);
        need.ReviewedByUserId = actorId; need.ReviewedAtUtc = DateTimeOffset.UtcNow;
        await store.SaveAsync(ct); return need;
    }

    public async Task<IReadOnlyList<CancellationSummary>> CancellationsAsync(Guid actorId,
        CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.ViewCancellations, ct);
        var rows = await store.CancellationsAsync(ct);
        var visible = actor.IsQuality ? rows : rows.Where(x => x.RequestedByUserId == actorId).ToList();
        var result = new List<CancellationSummary>();
        foreach (var request in visible)
        {
            var course = await store.CourseAsync(request.CourseId, ct);
            var documents = await store.DocumentsAsync("CourseCancellationRequest", request.Id, ct);
            result.Add(new CancellationSummary(request.Id, request.CourseId, course?.TitleAr ?? "",
                request.RequestedByUserId, request.ReasonAr, request.Status,
                request.ReviewNoteAr, documents.Select(x => x.Id).ToList()));
        }
        return result;
    }

    public async Task<CourseCancellationRequest> RequestCancellationAsync(Guid actorId, Guid courseId,
        CancellationInput input, Stream officialDocument, string originalName, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.RequestCancellation, ct);
        if (!actor.IsDean && !actor.IsAdmin) throw new UnauthorizedAccessException();
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        if (!actor.IsAdmin && (course.FacultyId != actor.User.FacultyId || course.CreatedByUserId != actorId))
            throw new UnauthorizedAccessException();
        if (course.Status is not (CourseStatus.Approved or CourseStatus.InProgress or CourseStatus.Ended))
            throw new InvalidOperationException("Only an approved course can be cancelled by request.");
        if (string.IsNullOrWhiteSpace(input.ReasonAr) || input.ReasonAr.Length > 4000)
            throw new ArgumentException("Cancellation reason is required.");
        if ((await store.CancellationsAsync(ct)).Any(x => x.CourseId == courseId && x.Status == QualityRequestStatus.Submitted))
            throw new InvalidOperationException("A cancellation request is already pending.");
        var saved = await storage.SaveAsync(officialDocument, originalName, ct);
        var request = new CourseCancellationRequest
        {
            CourseId = courseId, RequestedByUserId = actorId, ReasonAr = input.ReasonAr.Trim(),
            OfficialDocumentStorageKey = saved.StorageKey, OfficialDocumentOriginalName = saved.OriginalName,
            OfficialDocumentSha256 = saved.Sha256
        };
        store.AddCancellation(request);
        AddDocument("CourseCancellationRequest", request.Id, "OfficialLetter", saved, actorId);
        await store.SaveAsync(ct); return request;
    }

    public async Task<CourseCancellationRequest> ReviewCancellationAsync(Guid actorId, Guid requestId,
        ReviewRequestInput input, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.ReviewCancellation, ct);
        if (input.Status is not (QualityRequestStatus.Approved or QualityRequestStatus.Rejected))
            throw new ArgumentException("Approve or reject explicitly.");
        if (input.Status == QualityRequestStatus.Rejected && string.IsNullOrWhiteSpace(input.ReviewNoteAr))
            throw new ArgumentException("Rejection reason is required.");
        var request = await store.CancellationAsync(requestId, ct)
            ?? throw new KeyNotFoundException("Cancellation request not found.");
        if (request.Status != QualityRequestStatus.Submitted)
            throw new InvalidOperationException("This cancellation request was already reviewed.");
        var course = await store.CourseAsync(request.CourseId, ct)
            ?? throw new KeyNotFoundException("Course not found.");
        request.Status = input.Status; request.ReviewNoteAr = Clean(input.ReviewNoteAr);
        request.ReviewedByUserId = actorId; request.ReviewedAtUtc = DateTimeOffset.UtcNow;
        if (input.Status == QualityRequestStatus.Approved)
        {
            course.Status = CourseStatus.Cancelled; course.IsArchived = true;
            course.CancelledAtUtc = DateTimeOffset.UtcNow;
            foreach (var certificate in await store.CertificatesAsync(course.Id, ct))
            {
                certificate.Status = CertificateStatus.Revoked;
                certificate.StatusReason = "CourseCancelled";
                certificate.StatusChangedAtUtc = DateTimeOffset.UtcNow;
            }
        }
        await store.SaveAsync(ct);
        if (input.Status == QualityRequestStatus.Approved)
        {
            foreach (var enrollment in await store.EnrollmentsAsync(course.Id, ct))
                await notifications.NotifyAsync(enrollment.UserId, NotificationKinds.CourseCancelled,
                    "أُلغيت دورة تدريبية", "Training course cancelled",
                    "أُلغيت الدورة: " + course.TitleAr + "، وأُلغيت شهاداتها.",
                    "Course cancelled and its certificates revoked: " + course.TitleAr,
                    "/member.html", ct);
        }
        return request;
    }

    /// <summary>وثائق مقترح الدورة (كتب التكليف والسير الذاتية للمحاضرين الخارجيين).</summary>
    public async Task<IReadOnlyList<OfficialDocumentRow>> CourseDocumentsAsync(Guid actorId, Guid courseId,
        CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        var permitted = actor.IsQuality || course.CreatedByUserId == actorId || course.InstructorUserId == actorId ||
                        course.FacultyId == actor.User.FacultyId;
        if (!permitted) throw new UnauthorizedAccessException();
        var rows = await store.DocumentsAsync("Course", courseId, ct);
        return rows.Select(x => new OfficialDocumentRow(x.Id, x.EntityType, x.EntityId, x.Kind,
            x.OriginalName, x.Sha256, x.UploadedByUserId, x.CreatedAtUtc)).ToList();
    }

    /// <summary>إرفاق الكتاب الرسمي والسيرة الذاتية لمحاضر خارجي في مقترح دورة (القسم 7).</summary>
    public async Task<OfficialDocumentRow> AttachCourseDocumentAsync(Guid actorId, Guid courseId, string kind,
        Stream content, string originalName, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.CreateCourses, ct);
        var allowed = new[] { "ExternalTrainerLetter", "ExternalTrainerCv" };
        if (!allowed.Contains(kind, StringComparer.Ordinal))
            throw new ArgumentException("Unsupported proposal document kind.");
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        if (!actor.IsAdmin && !actor.IsQuality &&
            (course.CreatedByUserId != actorId || (actor.IsDean && course.FacultyId != actor.User.FacultyId)))
            throw new UnauthorizedAccessException();
        if (course.Status is not (CourseStatus.Draft or CourseStatus.Submitted or CourseStatus.RevisionRequested))
            throw new InvalidOperationException("Documents can be attached before the proposal is approved.");
        var saved = await storage.SaveAsync(content, originalName, ct);
        var document = AddDocument("Course", courseId, kind, saved, actorId);
        await store.SaveAsync(ct);
        return new OfficialDocumentRow(document.Id, document.EntityType, document.EntityId, document.Kind,
            document.OriginalName, document.Sha256, document.UploadedByUserId, document.CreatedAtUtc);
    }

    public async Task<OfficialDocumentVault> VaultAsync(Guid actorId, string? query, string? kind,
        Guid? entityId, int page, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.ViewOfficialDocuments, ct);
        if (!actor.IsQuality) throw new UnauthorizedAccessException();
        page = page is < 1 or > 10000 ? 1 : page;
        var rows = await store.VaultAsync(query, kind, entityId, (page - 1) * 30, 30, ct);
        return new OfficialDocumentVault(rows.Select(x => new OfficialDocumentRow(x.Id, x.EntityType,
            x.EntityId, x.Kind, x.OriginalName, x.Sha256, x.UploadedByUserId, x.CreatedAtUtc)).ToList(),
            await store.VaultCountAsync(query, kind, entityId, ct), page);
    }

    public async Task<OfficialDocumentDownload> OpenDocumentAsync(Guid actorId, Guid documentId,
        CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.ViewOfficialDocuments, ct);
        var document = await store.DocumentAsync(documentId, ct)
            ?? throw new KeyNotFoundException("Document not found.");
        var permitted = actor.IsQuality;
        if (!permitted && document.EntityType == "TrainingNeed")
            permitted = (await store.NeedAsync(document.EntityId, ct))?.SubmittedByUserId == actorId;
        if (!permitted && document.EntityType == "CourseCancellationRequest")
            permitted = (await store.CancellationAsync(document.EntityId, ct))?.RequestedByUserId == actorId;
        if (!permitted && document.EntityType == "Course")
        {
            var course = await store.CourseAsync(document.EntityId, ct);
            permitted = course is not null &&
                (course.CreatedByUserId == actorId || course.InstructorUserId == actorId ||
                 course.FacultyId == actor.User.FacultyId);
        }
        if (!permitted) throw new UnauthorizedAccessException();
        var stream = await storage.OpenReadAsync(document.StorageKey, ct)
            ?? throw new KeyNotFoundException("Document file not found.");
        return new OfficialDocumentDownload(stream, document.OriginalName);
    }

    private OfficialDocument AddDocument(string type, Guid id, string kind, StoredOfficialDocument saved,
        Guid actorId)
    {
        var document = new OfficialDocument
        {
            EntityType = type, EntityId = id, Kind = kind, OriginalName = saved.OriginalName,
            StorageKey = saved.StorageKey, Sha256 = saved.Sha256, UploadedByUserId = actorId
        };
        store.AddDocument(document);
        return document;
    }

    private static void ValidateNeed(TrainingNeedInput input)
    {
        if (string.IsNullOrWhiteSpace(input.TitleAr) || input.TitleAr.Length > 500 ||
            input.TitleEn?.Length > 500 || string.IsNullOrWhiteSpace(input.DescriptionAr) ||
            input.DescriptionAr.Length > 4000 || input.DescriptionEn?.Length > 4000)
            throw new ArgumentException("Arabic need title and description are required.");
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
