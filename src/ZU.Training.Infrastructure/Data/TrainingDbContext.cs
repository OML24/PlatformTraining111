using Microsoft.EntityFrameworkCore;
using ZU.Training.Domain.Common;
using ZU.Training.Domain.Communication;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Members;
using ZU.Training.Domain.Settings;
using ZU.Training.Domain.Training;

namespace ZU.Training.Infrastructure.Data;

public sealed class TrainingDbContext(DbContextOptions<TrainingDbContext> options) : DbContext(options)
{
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<SharedInitialPassword> SharedInitialPasswords => Set<SharedInitialPassword>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseReviewDecision> CourseReviewDecisions => Set<CourseReviewDecision>();
    public DbSet<TrainingTrack> TrainingTracks => Set<TrainingTrack>();
    public DbSet<Lecture> Lectures => Set<Lecture>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<CertificateTemplate> CertificateTemplates => Set<CertificateTemplate>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<EvaluationFormVersion> EvaluationFormVersions => Set<EvaluationFormVersion>();
    public DbSet<EvaluationQuestion> EvaluationQuestions => Set<EvaluationQuestion>();
    public DbSet<EvaluationOption> EvaluationOptions => Set<EvaluationOption>();
    public DbSet<EvaluationResponse> EvaluationResponses => Set<EvaluationResponse>();
    public DbSet<EvaluationAnswer> EvaluationAnswers => Set<EvaluationAnswer>();
    public DbSet<ImpactSubmission> ImpactSubmissions => Set<ImpactSubmission>();
    public DbSet<TrainingNeed> TrainingNeeds => Set<TrainingNeed>();
    public DbSet<CourseCancellationRequest> CourseCancellationRequests => Set<CourseCancellationRequest>();
    public DbSet<OfficialDocument> OfficialDocuments => Set<OfficialDocument>();
    public DbSet<FieldHint> FieldHints => Set<FieldHint>();
    public DbSet<PasswordPolicy> PasswordPolicies => Set<PasswordPolicy>();
    public DbSet<IntegrationSyncRun> IntegrationSyncRuns => Set<IntegrationSyncRun>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<LectureMaterial> LectureMaterials => Set<LectureMaterial>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<ExternalTrainerRequest> ExternalTrainerRequests => Set<ExternalTrainerRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(type => typeof(Entity).IsAssignableFrom(type.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.IsActive)).HasDefaultValue(true);
            modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.IsArchived)).HasDefaultValue(false);
        }

        modelBuilder.Entity<Faculty>(b =>
        {
            b.ToTable("faculties");
            b.HasIndex(x => x.ExternalCode).IsUnique();
            b.Property(x => x.ExternalCode).HasMaxLength(20);
            b.Property(x => x.NameAr).HasMaxLength(300);
            b.Property(x => x.NameEn).HasMaxLength(300);
        });
        modelBuilder.Entity<Department>(b =>
        {
            b.ToTable("departments");
            b.HasIndex(x => x.ExternalCode).IsUnique();
            b.Property(x => x.ExternalCode).HasMaxLength(20);
            b.Property(x => x.FacultyExternalCode).HasMaxLength(20);
            b.Property(x => x.NameAr).HasMaxLength(300);
            b.Property(x => x.NameEn).HasMaxLength(300);
            b.HasOne<Faculty>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<UserAccount>(b =>
        {
            b.ToTable("user_accounts");
            b.HasIndex(x => x.LoginName).IsUnique();
            b.HasIndex(x => x.EmployeeId).IsUnique();
            b.Property(x => x.LoginName).HasMaxLength(64);
            b.Property(x => x.NameAr).HasMaxLength(300);
            b.Property(x => x.NameEn).HasMaxLength(300);
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.PreferredLanguage).HasMaxLength(2);
            b.Property(x => x.InitialEmailLastError).HasMaxLength(1000);
            b.HasOne<Faculty>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SharedInitialPassword>().ToTable("shared_initial_passwords");
        modelBuilder.Entity<Role>(b =>
        {
            b.ToTable("roles");
            b.HasIndex(x => x.Key).IsUnique();
            b.Property(x => x.Key).HasMaxLength(80);
        });
        modelBuilder.Entity<Permission>(b =>
        {
            b.ToTable("permissions");
            b.HasIndex(x => x.Key).IsUnique();
            b.Property(x => x.Key).HasMaxLength(150);
        });
        modelBuilder.Entity<UserRole>(b =>
        {
            b.ToTable("user_roles");
            b.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RolePermission>(b =>
        {
            b.ToTable("role_permissions");
            b.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
            b.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<UserPermissionOverride>(b =>
        {
            b.ToTable("user_permission_overrides");
            b.HasIndex(x => new { x.UserId, x.PermissionId }).IsUnique();
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Course>(b =>
        {
            b.ToTable("courses");
            b.Property(x => x.TitleAr).HasMaxLength(500);
            b.Property(x => x.TitleEn).HasMaxLength(500);
            b.Property(x => x.ObjectivesAr).HasMaxLength(4000);
            b.Property(x => x.ObjectivesEn).HasMaxLength(4000);
            b.Property(x => x.TargetAudienceAr).HasMaxLength(1000);
            b.Property(x => x.TargetAudienceEn).HasMaxLength(1000);
            b.Property(x => x.PlannedHours).HasPrecision(7, 2);
            b.HasOne<TrainingTrack>().WithMany().HasForeignKey(x => x.TrainingTrackId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.ActualInstructorNameAr).HasMaxLength(300);
            b.Property(x => x.ActualInstructorNameEn).HasMaxLength(300);
            b.Property(x => x.ExternalTrainerOrganizationAr).HasMaxLength(300);
            b.Property(x => x.ExternalTrainerOrganizationEn).HasMaxLength(300);
            b.Property(x => x.ReviewNoteAr).HasMaxLength(4000);
            b.HasOne<Faculty>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.InstructorUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EvaluationFormVersion>().WithMany().HasForeignKey(x => x.EvaluationFormVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseReviewDecision>(b =>
        {
            b.ToTable("course_review_decisions");
            b.Property(x => x.NoteAr).HasMaxLength(4000);
            b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TrainingTrack>(b =>
        {
            b.ToTable("training_tracks");
            b.Property(x => x.NameAr).HasMaxLength(300);
            b.Property(x => x.NameEn).HasMaxLength(300);
            b.Property(x => x.ObjectivesAr).HasMaxLength(2000);
            b.Property(x => x.ObjectivesEn).HasMaxLength(2000);
        });
        modelBuilder.Entity<Lecture>(b =>
        {
            b.ToTable("lectures");
            b.HasIndex(x => new { x.CourseId, x.SequenceNumber }).IsUnique();
            b.Property(x => x.AttendanceCode).HasMaxLength(4);
            b.Property(x => x.OnlineLink).HasMaxLength(1000);
            b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseEnrollment>(b =>
        {
            b.ToTable("course_enrollments");
            b.HasIndex(x => new { x.CourseId, x.UserId }).IsUnique();
            b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AttendanceRecord>(b =>
        {
            b.ToTable("attendance_records");
            b.HasIndex(x => new { x.LectureId, x.EnrollmentId }).IsUnique();
            b.HasOne<Lecture>().WithMany().HasForeignKey(x => x.LectureId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CourseEnrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Exam>(b =>
        {
            b.ToTable("exams");
            b.HasIndex(x => x.CourseId).IsUnique().HasFilter("\"IsActive\" = TRUE");
            b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ExamResult>(b =>
        {
            b.ToTable("exam_results");
            b.HasIndex(x => new { x.ExamId, x.EnrollmentId }).IsUnique();
            b.HasOne<Exam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CourseEnrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CertificateTemplate>(b =>
        {
            b.ToTable("certificate_templates");
            b.HasIndex(x => x.Version).IsUnique();
            b.Property(x => x.Sha256).HasMaxLength(64);
        });
        modelBuilder.Entity<Certificate>(b =>
        {
            b.ToTable("certificates");
            b.HasIndex(x => x.ReferenceNumber).IsUnique();
            b.HasIndex(x => x.VerificationCode).IsUnique();
            b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CourseEnrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CertificateTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EvaluationFormVersion>(b =>
        {
            b.ToTable("evaluation_form_versions");
            b.HasIndex(x => x.Version).IsUnique();
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EvaluationQuestion>(b =>
        {
            b.ToTable("evaluation_questions");
            b.HasIndex(x => new { x.FormVersionId, x.Position }).IsUnique();
            b.Property(x => x.TextAr).HasMaxLength(1000);
            b.Property(x => x.TextEn).HasMaxLength(1000);
            b.HasOne<EvaluationFormVersion>().WithMany().HasForeignKey(x => x.FormVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EvaluationOption>(b =>
        {
            b.ToTable("evaluation_options");
            b.HasIndex(x => new { x.QuestionId, x.Position }).IsUnique();
            b.Property(x => x.TextAr).HasMaxLength(500);
            b.Property(x => x.TextEn).HasMaxLength(500);
            b.HasOne<EvaluationQuestion>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EvaluationResponse>(b =>
        {
            b.ToTable("evaluation_responses");
            b.HasIndex(x => new { x.FormVersionId, x.EnrollmentId }).IsUnique();
            b.HasOne<EvaluationFormVersion>().WithMany().HasForeignKey(x => x.FormVersionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CourseEnrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EvaluationAnswer>(b =>
        {
            b.ToTable("evaluation_answers");
            b.HasIndex(x => new { x.ResponseId, x.QuestionId }).IsUnique();
            b.HasOne<EvaluationResponse>().WithMany().HasForeignKey(x => x.ResponseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EvaluationQuestion>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<EvaluationOption>().WithMany().HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ImpactSubmission>(b =>
        {
            b.ToTable("impact_submissions");
            b.HasIndex(x => new { x.EnrollmentId, x.Version }).IsUnique();
            b.Property(x => x.OriginalName).HasMaxLength(300);
            b.Property(x => x.StorageKey).HasMaxLength(100);
            b.Property(x => x.Sha256).HasMaxLength(64);
            b.Property(x => x.ReviewNoteAr).HasMaxLength(4000);
            b.HasOne<CourseEnrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TrainingNeed>(b =>
        {
            b.ToTable("training_needs");
            b.Property(x => x.TitleAr).HasMaxLength(500);
            b.Property(x => x.TitleEn).HasMaxLength(500);
            b.Property(x => x.DescriptionAr).HasMaxLength(4000);
            b.Property(x => x.DescriptionEn).HasMaxLength(4000);
            b.Property(x => x.ReviewNoteAr).HasMaxLength(4000);
            b.Property(x => x.OfficialDocumentOriginalName).HasMaxLength(300);
            b.Property(x => x.OfficialDocumentSha256).HasMaxLength(64);
            b.HasOne<Faculty>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseCancellationRequest>(b =>
        {
            b.ToTable("course_cancellation_requests");
            b.Property(x => x.ReasonAr).HasMaxLength(4000);
            b.Property(x => x.ReviewNoteAr).HasMaxLength(4000);
            b.Property(x => x.OfficialDocumentOriginalName).HasMaxLength(300);
            b.Property(x => x.OfficialDocumentSha256).HasMaxLength(64);
            b.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<OfficialDocument>(b =>
        {
            b.ToTable("official_documents");
            b.Property(x => x.EntityType).HasMaxLength(100);
            b.Property(x => x.Kind).HasMaxLength(100);
            b.Property(x => x.OriginalName).HasMaxLength(300);
            b.Property(x => x.StorageKey).HasMaxLength(100);
            b.Property(x => x.Sha256).HasMaxLength(64);
            b.HasIndex(x => new { x.EntityType, x.EntityId });
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<FieldHint>(b =>
        {
            b.ToTable("field_hints");
            b.HasIndex(x => new { x.ScreenKey, x.FieldKey }).IsUnique();
            b.Property(x => x.ScreenKey).HasMaxLength(150);
            b.Property(x => x.FieldKey).HasMaxLength(150);
        });
        modelBuilder.Entity<LectureMaterial>(b =>
        {
            b.ToTable("lecture_materials");
            b.Property(x => x.OriginalName).HasMaxLength(300);
            b.Property(x => x.StorageKey).HasMaxLength(100);
            b.Property(x => x.Sha256).HasMaxLength(64);
            b.HasIndex(x => x.LectureId);
            b.HasOne<Lecture>().WithMany().HasForeignKey(x => x.LectureId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Notification>(b =>
        {
            b.ToTable("notifications");
            b.Property(x => x.Kind).HasMaxLength(80);
            b.Property(x => x.TitleAr).HasMaxLength(300);
            b.Property(x => x.TitleEn).HasMaxLength(300);
            b.Property(x => x.BodyAr).HasMaxLength(2000);
            b.Property(x => x.BodyEn).HasMaxLength(2000);
            b.Property(x => x.LinkUrl).HasMaxLength(500);
            b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<MemberProfile>(b =>
        {
            b.ToTable("member_profiles");
            b.HasIndex(x => x.UserId).IsUnique();
            b.Property(x => x.BioAr).HasMaxLength(4000);
            b.Property(x => x.BioEn).HasMaxLength(4000);
            b.Property(x => x.ExpertiseAr).HasMaxLength(1000);
            b.Property(x => x.ExpertiseEn).HasMaxLength(1000);
            b.Property(x => x.PhotoStorageKey).HasMaxLength(100);
            b.Property(x => x.ContactEmail).HasMaxLength(320);
            b.Property(x => x.Phone).HasMaxLength(40);
            b.Property(x => x.OfficeAr).HasMaxLength(200);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ExternalTrainerRequest>(b =>
        {
            b.ToTable("external_trainer_requests");
            b.Property(x => x.TrainerNameAr).HasMaxLength(300);
            b.Property(x => x.TrainerNameEn).HasMaxLength(300);
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.OrganizationAr).HasMaxLength(300);
            b.Property(x => x.NoteAr).HasMaxLength(2000);
            b.Property(x => x.CreatedLoginName).HasMaxLength(64);
            b.HasIndex(x => x.Status);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.HandledByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PasswordPolicy>().ToTable("password_policies");
        modelBuilder.Entity<IntegrationSyncRun>().ToTable("integration_sync_runs");
        modelBuilder.Entity<AuditEvent>(b =>
        {
            b.ToTable("audit_events");
            b.Property(x => x.IpAddress).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(400);
            b.Property(x => x.Details).HasMaxLength(2000);
            b.HasIndex(x => x.CreatedAtUtc);
        });

        var seededAt = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
        modelBuilder.Entity<Role>().HasData(
            new { Id = Guid.Parse("5c98724b-cead-42f7-b20d-638817eb3c01"), Key = "ADMIN", NameAr = "مسؤول النظام", NameEn = "Administrator", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("5c98724b-cead-42f7-b20d-638817eb3c02"), Key = "QA", NameAr = "الجودة", NameEn = "Quality Assurance", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("5c98724b-cead-42f7-b20d-638817eb3c03"), Key = "DEAN", NameAr = "عميد الكلية", NameEn = "Dean", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("5c98724b-cead-42f7-b20d-638817eb3c04"), Key = "INST", NameAr = "المحاضر", NameEn = "Instructor", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("5c98724b-cead-42f7-b20d-638817eb3c05"), Key = "MEMBER", NameAr = "عضو هيئة التدريس", NameEn = "Faculty Member", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("5c98724b-cead-42f7-b20d-638817eb3c06"), Key = "EXTERNAL_INSTRUCTOR", NameAr = "محاضر خارجي", NameEn = "External Instructor", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt });
        modelBuilder.Entity<Permission>().HasData(
            new { Id = Guid.Parse("8e9838fb-c639-4adc-a40a-a926df430101"), Key = PermissionKeys.ManageHints, NameAr = "إدارة تلميحات الحقول", NameEn = "Manage field hints", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("8e9838fb-c639-4adc-a40a-a926df430102"), Key = PermissionKeys.ManagePasswordPolicy, NameAr = "إدارة سياسة كلمات المرور", NameEn = "Manage password policy", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("8e9838fb-c639-4adc-a40a-a926df430103"), Key = PermissionKeys.ManageCertificateTemplates, NameAr = "إدارة قوالب الشهادات", NameEn = "Manage certificate templates", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("8e9838fb-c639-4adc-a40a-a926df430104"), Key = PermissionKeys.ManageUsers, NameAr = "إدارة المستخدمين", NameEn = "Manage users", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("8e9838fb-c639-4adc-a40a-a926df430105"), Key = PermissionKeys.ManagePermissions, NameAr = "إدارة الصلاحيات", NameEn = "Manage permissions", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt });
        modelBuilder.Entity<Permission>().HasData(new
        {
            Id = Guid.Parse("8e9838fb-c639-4adc-a40a-a926df430117"),
            Key = PermissionKeys.SyncUniversity, NameAr = "تحديث بيانات الجامعة", NameEn = "Sync university directory",
            CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt
        });
        var qualityPermissions = new (string Key, string Arabic, string English, int[] Roles)[]
        {
            (PermissionKeys.ViewTrainingNeeds, "عرض الاحتياجات التدريبية", "View training needs", [2, 3]),
            (PermissionKeys.SubmitTrainingNeeds, "رفع الاحتياجات التدريبية", "Submit training needs", [3]),
            (PermissionKeys.ReviewTrainingNeeds, "مراجعة الاحتياجات التدريبية", "Review training needs", [2]),
            (PermissionKeys.ViewCancellations, "عرض طلبات إلغاء الدورات", "View cancellation requests", [2, 3]),
            (PermissionKeys.RequestCancellation, "طلب إلغاء دورة", "Request course cancellation", [3]),
            (PermissionKeys.ReviewCancellation, "البت في طلب إلغاء دورة", "Review course cancellation", [2]),
            (PermissionKeys.ViewOfficialDocuments, "عرض الكتب الرسمية", "View official documents", [2, 3]),
            (PermissionKeys.SubmitImpact, "رفع الأثر التدريبي", "Submit training impact", [2, 3, 4, 5]),
            (PermissionKeys.ReviewImpact, "مراجعة الأثر التدريبي", "Review training impact", [2, 4, 6]),
            (PermissionKeys.ManageEvaluation, "إدارة نموذج التقييم", "Manage evaluation form", [2]),
            (PermissionKeys.SubmitEvaluation, "تعبئة التقييم", "Submit evaluation", [2, 3, 4, 5]),
            (PermissionKeys.ViewEvaluationStatistics, "عرض إحصاءات التقييم", "View evaluation statistics", [2, 4, 6]),
            (PermissionKeys.ChangeExamRequirement, "تعديل شرط امتحان الدورة", "Change course exam requirement", [2]),
            (PermissionKeys.ManageAllCourses, "إدارة جميع الدورات وتعطيلها", "Manage all courses", [])
        };
        modelBuilder.Entity<Permission>().HasData(qualityPermissions.Select((p, index) => new
        {
            Id = Guid.Parse($"8e9838fb-c639-4adc-a40a-a926df43{index + 0x118:X4}"),
            p.Key, NameAr = p.Arabic, NameEn = p.English, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt
        }));
        var qualityGrantIndex = 0;
        modelBuilder.Entity<RolePermission>().HasData(qualityPermissions.SelectMany((p, permissionIndex) => p.Roles.Select(role => new
        {
            Id = Guid.Parse($"c0df69bb-93b9-43ce-8812-32145868{++qualityGrantIndex:X4}"),
            RoleId = Guid.Parse($"5c98724b-cead-42f7-b20d-638817eb3c{role:X2}"),
            PermissionId = Guid.Parse($"8e9838fb-c639-4adc-a40a-a926df43{permissionIndex + 0x118:X4}"),
            CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt
        })));
        var coursePermissions = new (string Key, string Arabic, string English)[]
        {
            (PermissionKeys.ViewCourses, "عرض الدورات", "View courses"),
            (PermissionKeys.CreateCourses, "إنشاء الدورات", "Create courses"),
            (PermissionKeys.ApproveCourses, "اعتماد الدورات", "Approve courses"),
            (PermissionKeys.SelfEnroll, "التسجيل الذاتي", "Self enroll"),
            (PermissionKeys.SelectParticipants, "اختيار المشاركين", "Select participants"),
            (PermissionKeys.ManageLectures, "إدارة المحاضرات وأكوادها", "Manage lectures and codes"),
            (PermissionKeys.EnterAttendanceCode, "إدخال كود الحضور", "Enter attendance code"),
            (PermissionKeys.CorrectAttendance, "تصحيح الحضور", "Correct attendance"),
            (PermissionKeys.EndCourse, "إنهاء الدورة", "End course"),
            (PermissionKeys.RecordExam, "توثيق الامتحان ونتائجه", "Record exam and results"),
            (PermissionKeys.SearchUsers, "البحث في دليل المستخدمين", "Search user directory")
        };
        modelBuilder.Entity<Permission>().HasData(coursePermissions.Select((p, index) => new
        {
            Id = Guid.Parse($"8e9838fb-c639-4adc-a40a-a926df43{index + 0x106:X4}"),
            p.Key, NameAr = p.Arabic, NameEn = p.English, CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt
        }));
        var roleGrants = new (int Permission, int[] Roles)[]
        {
            (0, [2, 3, 4, 5, 6]),
            (1, [2, 3]),
            (2, [2]),
            (3, [2, 3, 4, 5]),
            (4, [2]),
            (5, [2, 3, 4, 5, 6]),
            (6, [2, 3, 4, 5]),
            (7, [2, 3, 4, 5, 6]),
            (8, [2, 3, 4, 5, 6]),
            (9, [2, 3, 4, 5, 6]),
            (10, [2, 3])
        };
        var grantIndex = 0;
        modelBuilder.Entity<RolePermission>().HasData(roleGrants.SelectMany(grant => grant.Roles.Select(role => new
        {
            Id = Guid.Parse($"c0df69bb-93b9-43ce-8812-32145867{++grantIndex:X4}"),
            RoleId = Guid.Parse($"5c98724b-cead-42f7-b20d-638817eb3c{role:X2}"),
            PermissionId = Guid.Parse($"8e9838fb-c639-4adc-a40a-a926df43{grant.Permission + 0x106:X4}"),
            CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt
        })));
        modelBuilder.Entity<PasswordPolicy>().HasData(new
        {
            Id = Guid.Parse("f47613db-7bd5-4d19-a8fa-9e67c247c2f7"),
            MinimumLength = 15,
            MaximumLength = 128,
            AllowSpaces = true,
            RequireMixedCharacterClasses = false,
            BlockCommonPasswords = true,
            EffectiveAtUtc = seededAt,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt
        });
        modelBuilder.Entity<TrainingTrack>().HasData(
            new { Id = Guid.Parse("b159551c-5440-46b2-9d6e-c87559dc5501"), NameAr = "التطوير التقني والرقمي", NameEn = "Technology and Digital Development", ObjectivesAr = "تنمية المهارات التقنية والرقمية", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("b159551c-5440-46b2-9d6e-c87559dc5502"), NameAr = "طرائق التدريس والقياس والتقويم", NameEn = "Teaching, Measurement and Assessment", ObjectivesAr = "تطوير الممارسات التدريسية والتقويم", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("b159551c-5440-46b2-9d6e-c87559dc5503"), NameAr = "البحث العلمي والنشر الدولي", NameEn = "Scientific Research and International Publishing", ObjectivesAr = "تعزيز البحث العلمي والنشر", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt },
            new { Id = Guid.Parse("b159551c-5440-46b2-9d6e-c87559dc5504"), NameAr = "القيادة المؤسسية والموارد البشرية", NameEn = "Institutional Leadership and Human Resources", ObjectivesAr = "تطوير القيادة والإدارة المؤسسية", CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt });
        var hintSeeds = new (string Screen, string Field, string Arabic, string English)[]
        {
            ("auth", "loginName", "أدخل الرقم الوظيفي المعتمد للدخول.", "Enter your employee number."),
            ("auth", "password", "أدخل كلمة المرور الحالية أو الأولية في أول دخول.", "Enter your current or initial password."),
            ("auth", "rememberMe", "يحفظ تسجيل الدخول لمدة أطول على هذا الجهاز.", "Keeps you signed in longer on this device."),
            ("auth", "oldPassword", "أدخل كلمة المرور المستخدمة حاليًا.", "Enter the password currently in use."),
            ("auth", "newPassword", "اختر كلمة جديدة توافق السياسة الحالية.", "Choose a password that meets the current policy."),
            ("auth", "confirmPassword", "أعد كتابة كلمة المرور الجديدة كما هي.", "Repeat the new password exactly."),
            ("settings", "hintScreen", "اسم الشاشة الذي يرتبط به التلميح، مثل auth أو course.", "Screen identifier, such as auth or course."),
            ("settings", "hintField", "اسم الحقل داخل الشاشة المختارة.", "Field identifier within the screen."),
            ("settings", "hintAr", "النص العربي الذي يظهر عند لمس الحقل.", "Arabic text shown when focusing the field."),
            ("settings", "hintEn", "ترجمة اختيارية؛ تظهر العربية عند تركها فارغة.", "Optional translation; Arabic is used if blank."),
            ("settings", "minimumLength", "أقل عدد أحرف لكلمة المرور الجديدة.", "Minimum length for new passwords."),
            ("settings", "maximumLength", "أكبر عدد أحرف لكلمة المرور الجديدة.", "Maximum length for new passwords."),
            ("settings", "allowSpaces", "حدد إذا كانت المسافات مسموحة في كلمة المرور.", "Allow spaces in passwords."),
            ("settings", "requireMix", "حدد إذا لزم وجود حروف وأرقام معًا.", "Require both letters and digits."),
            ("settings", "blockCommon", "يمنع كلمات المرور الأكثر شيوعًا.", "Blocks common passwords."),
            ("settings", "templateNameAr", "اسم إداري للقالب باللغة العربية.", "Administrative template name in Arabic."),
            ("settings", "templateNameEn", "الاسم الإنجليزي اختياري.", "English name is optional."),
            ("settings", "templateFile", "ارفع ملف PDF ثابتًا لا يتجاوز 20 ميغابايت.", "Upload a fixed PDF of up to 20 MB."),
            ("course", "titleAr", "اكتب الاسم الرسمي للدورة بالعربية.", "Enter the official Arabic course title."),
            ("course", "titleEn", "الاسم الإنجليزي اختياري.", "English title is optional."),
            ("course", "trainingTrack", "اختر محور التدريب الذي تتبعه الدورة.", "Choose the course training track."),
            ("course", "deliveryMode", "حدد وجاهيًا أو عن بعد أو مدمجًا.", "Select in person, online, or hybrid."),
            ("course", "objectivesAr", "اكتب أهداف الدورة ونواتج تعلمها بالعربية.", "Enter Arabic objectives and outcomes."),
            ("course", "objectivesEn", "ترجمة الأهداف اختيارية.", "English objectives are optional."),
            ("course", "audienceAr", "حدد الفئة المستهدفة بالعربية.", "Describe the target audience in Arabic."),
            ("course", "audienceEn", "ترجمة الفئة المستهدفة اختيارية.", "English audience is optional."),
            ("course", "plannedHours", "أدخل عدد الساعات المخطط لها.", "Enter the planned training hours."),
            ("course", "plannedStart", "أدخل التاريخ والوقت بصيغة سنة-شهر-يوم ساعة:دقيقة.", "Use YYYY-MM-DD HH:mm."),
            ("course", "plannedEnd", "يجب أن يكون بعد موعد البداية.", "Must be later than the start."),
            ("course", "faculty", "اختر كلية الدورة؛ اتركها فارغة للدورة العامة التي تنشئها الجودة.", "Choose a faculty or leave blank for a Quality general course."),
            ("course", "enrollmentMode", "التسجيل الذاتي للمؤهلين أو اختيار الأسماء من الجودة.", "Self registration or Quality selection."),
            ("course", "actualInstructor", "الاسم الذي يظهر للمتدربين وعلى الشهادة.", "Name shown to trainees and on certificates."),
            ("course", "instructorSearch", "ابحث عن عضو الهيئة بالاسم أو الرقم الوظيفي.", "Search by employee name or number."),
            ("course", "qualityOperated", "تدير الجودة دورة قصيرة باسم المحاضر الفعلي دون حساب له.", "Quality operates a short course for the named instructor."),
            ("course", "hasExam", "إذا حددته، يلزم نجاح الامتحان للشهادة.", "Exam pass is required for certificates when selected."),
            ("course", "evaluationDeadline", "تحدد الجودة موعد إغلاق تقييم الدورة.", "Quality sets the evaluation closing date."),
            ("admin", "userSearch", "ابحث باسم الشخص أو رقمه الوظيفي أو رقم دخوله.", "Search by name, employee number, or login."),
            ("admin", "employeeId", "رقم الموظف الوظيفي الفريد.", "Unique employee number."),
            ("admin", "externalLogin", "أربعة أرقام فريدة؛ اتركه فارغًا لتوليد رقم تلقائي.", "Unique four digit number; leave blank to generate one."),
            ("admin", "nameAr", "الاسم العربي المطلوب في الحساب.", "Required Arabic account name."),
            ("admin", "nameEn", "الاسم الإنجليزي اختياري.", "English account name is optional."),
            ("admin", "email", "البريد المعتمد للتواصل مع صاحب الحساب.", "Approved account contact email."),
            ("admin", "faculty", "الكلية التي ينتمي إليها الموظف عند توفر الربط الرسمي.", "Faculty when official mapping is available."),
            ("admin", "externalPassword", "كلمة مرور المحاضر الخارجي التي يحددها الأدمن.", "Administrator-set external trainer password."),
            ("admin", "newPassword", "تغيير كلمة المرور الحالية لأي حساب.", "Change the current password for any account."),
            ("admin", "sharedPassword", "كلمة أولية بديلة للحسابات قبل إكمال أول دخول.", "Fallback password for accounts before first login."),
            ("evaluation", "questionAr", "اكتب السؤال الرسمي بالعربية؛ سيظهر في النموذج الموحد.", "Enter the official Arabic question."),
            ("evaluation", "questionEn", "ترجمة السؤال اختيارية.", "English question is optional."),
            ("evaluation", "optionAr", "اكتب نص خيار الإجابة بالعربية.", "Enter the Arabic answer option."),
            ("evaluation", "optionEn", "ترجمة الخيار اختيارية.", "English option is optional."),
            ("impact", "file", "ارفع نسخة PDF أو DOCX جديدة لا تزيد عن 20 ميغابايت.", "Upload a revised PDF or DOCX up to 20 MB.")
        };
        modelBuilder.Entity<FieldHint>().HasData(hintSeeds.Select((hint, index) => new
        {
            Id = Guid.Parse($"e13984f2-88ba-4891-92f9-a2127594{index + 1:X4}"),
            ScreenKey = hint.Screen, FieldKey = hint.Field, TextAr = hint.Arabic, TextEn = hint.English,
            CreatedAtUtc = seededAt, UpdatedAtUtc = seededAt
        }));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAtUtc = now;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.UpdatedAtUtc = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
