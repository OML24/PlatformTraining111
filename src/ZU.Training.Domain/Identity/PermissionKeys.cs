namespace ZU.Training.Domain.Identity;

public static class PermissionKeys
{
    public const string ManageHints = "admin.hints.manage";
    public const string ManagePasswordPolicy = "admin.password_policy.manage";
    public const string ManageCertificateTemplates = "admin.certificate_templates.manage";
    public const string ManageUsers = "admin.users.manage";
    public const string ManagePermissions = "admin.permissions.manage";
    public const string ViewCourses = "courses.view";
    public const string CreateCourses = "courses.create";
    public const string ApproveCourses = "courses.approve";
    public const string SelfEnroll = "courses.self_enroll";
    public const string SelectParticipants = "courses.participants.select";
    public const string ManageLectures = "courses.lectures.manage";
    public const string EnterAttendanceCode = "courses.attendance.enter_code";
    public const string CorrectAttendance = "courses.attendance.correct";
    public const string EndCourse = "courses.end";
    public const string RecordExam = "courses.exam.record";
    public const string SearchUsers = "directory.users.search";
    public const string SyncUniversity = "admin.university_sync.run";
    public const string ViewTrainingNeeds = "training_needs.view";
    public const string SubmitTrainingNeeds = "training_needs.submit";
    public const string ReviewTrainingNeeds = "training_needs.review";
    public const string ViewCancellations = "course_cancellations.view";
    public const string RequestCancellation = "course_cancellations.request";
    public const string ReviewCancellation = "course_cancellations.review";
    public const string ViewOfficialDocuments = "official_documents.view";
    public const string SubmitImpact = "impact.submit";
    public const string ReviewImpact = "impact.review";
    public const string ManageEvaluation = "evaluation.manage";
    public const string SubmitEvaluation = "evaluation.submit";
    public const string ViewEvaluationStatistics = "evaluation.statistics.view";
    public const string ChangeExamRequirement = "courses.exam_requirement.change";
    public const string ManageAllCourses = "admin.courses.manage";
    public const string ViewAdminConsole = "page.admin";
    public const string ViewQualityConsole = "page.quality";
    public const string ViewDeanConsole = "page.dean";
    public const string ViewInstructorConsole = "page.instructor";
    public const string ViewMemberConsole = "page.member";
    public const string ViewMemberDirectory = "members.directory.view";
    public const string ViewAllStatistics = "admin.statistics.view";
    public const string ViewAuditLog = "admin.audit.view";
    public const string ManageTracks = "admin.tracks.manage";
    public const string SendCredentialEmails = "admin.emails.send";
    public const string ImportHistoricalCourses = "courses.history.import";
    public const string RequestExternalTrainer = "courses.external_trainer.request";
    public const string IssueCertificates = "certificates.issue";
    public const string RevokeCertificates = "certificates.revoke";
    public const string ManageLectureMaterials = "courses.materials.manage";
    public const string ViewCourseStatistics = "courses.statistics.view";
    public const string ViewDocumentVault = "admin.documents.vault";

    public static IReadOnlyList<string> All { get; } =
    [
        ManageHints, ManagePasswordPolicy, ManageCertificateTemplates, ManageUsers, ManagePermissions, SyncUniversity,
        ViewCourses, CreateCourses, ApproveCourses, SelfEnroll, SelectParticipants,
        ManageLectures, EnterAttendanceCode, CorrectAttendance, EndCourse, RecordExam, SearchUsers,
        ViewTrainingNeeds, SubmitTrainingNeeds, ReviewTrainingNeeds, ViewCancellations,
        RequestCancellation, ReviewCancellation, ViewOfficialDocuments, SubmitImpact, ReviewImpact,
        ManageEvaluation, SubmitEvaluation, ViewEvaluationStatistics, ChangeExamRequirement,
        ManageAllCourses,
        ViewAdminConsole, ViewQualityConsole, ViewDeanConsole, ViewInstructorConsole, ViewMemberConsole,
        ViewMemberDirectory, ViewAllStatistics, ViewAuditLog, ManageTracks, SendCredentialEmails,
        ImportHistoricalCourses, RequestExternalTrainer, IssueCertificates, RevokeCertificates,
        ManageLectureMaterials, ViewCourseStatistics, ViewDocumentVault
    ];
}
