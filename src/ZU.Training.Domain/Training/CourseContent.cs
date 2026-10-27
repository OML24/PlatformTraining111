using ZU.Training.Domain.Common;

namespace ZU.Training.Domain.Training;

/// <summary>
/// ملف مرفق بمحاضرة (حقيبة تدريبية، عرض تقديمي، PDF). يُخزَّن على القرص ويُشار إليه بمفتاح.
/// </summary>
public sealed class LectureMaterial : Entity
{
    public Guid LectureId { get; set; }
    public required string OriginalName { get; set; }
    public required string StorageKey { get; set; }
    public required string Sha256 { get; set; }
    public Guid UploadedByUserId { get; set; }
}

public enum ExternalTrainerRequestStatus
{
    Pending = 0,
    AccountCreated = 1,
    Rejected = 2
}

/// <summary>
/// طلب الجودة إنشاء حساب مدرب خارجي وإحالته للأدمن (القسم 8 من المواصفات).
/// </summary>
public sealed class ExternalTrainerRequest : Entity
{
    public Guid RequestedByUserId { get; set; }
    public required string TrainerNameAr { get; set; }
    public string? TrainerNameEn { get; set; }
    public required string Email { get; set; }
    public string? OrganizationAr { get; set; }
    public string? NoteAr { get; set; }
    public ExternalTrainerRequestStatus Status { get; set; } = ExternalTrainerRequestStatus.Pending;
    public Guid? HandledByUserId { get; set; }
    public DateTimeOffset? HandledAtUtc { get; set; }
    public string? CreatedLoginName { get; set; }
    public Guid? CourseId { get; set; }
}
