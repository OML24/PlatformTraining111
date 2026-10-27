using ZU.Training.Domain.Common;

namespace ZU.Training.Domain.Members;

/// <summary>
/// الملف الشخصي المهني المشترك بين مستخدمي المنصة (بيانات محلية، لا تشمل سجلات الحضور أو النتائج).
/// </summary>
public sealed class MemberProfile : Entity
{
    public Guid UserId { get; set; }
    public string? BioAr { get; set; }
    public string? BioEn { get; set; }
    public string? ExpertiseAr { get; set; }
    public string? ExpertiseEn { get; set; }
    public string? PhotoStorageKey { get; set; }
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? OfficeAr { get; set; }
}
