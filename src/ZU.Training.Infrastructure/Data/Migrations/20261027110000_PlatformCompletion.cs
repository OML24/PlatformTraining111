using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZU.Training.Infrastructure.Data;

#nullable disable

namespace ZU.Training.Infrastructure.Data.Migrations;

/// <summary>
/// استكمال المنصة: الشهادات وملفاتها، الإشعارات، مرفقات المحاضرات، الملف الشخصي المشترك،
/// طلبات المدربين الخارجيين، الدورات التاريخية، حالة إرسال بيانات الدخول، وصلاحيات الواجهات.
/// مكتوبة بـSQL صريح متكرر التنفيذ بأمان (idempotent) لتفادي أي التباس في أسماء الأعمدة.
/// </summary>
[DbContext(typeof(TrainingDbContext))]
[Migration("20261027110000_PlatformCompletion")]
public sealed class PlatformCompletion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- 1) توسيعات الجداول القائمة
            ALTER TABLE lectures ADD COLUMN IF NOT EXISTS "Mode" integer NOT NULL DEFAULT 0;
            ALTER TABLE lectures ADD COLUMN IF NOT EXISTS "OnlineLink" character varying(1000);

            ALTER TABLE courses ADD COLUMN IF NOT EXISTS "ExternalTrainerOrganizationAr" character varying(300);
            ALTER TABLE courses ADD COLUMN IF NOT EXISTS "ExternalTrainerOrganizationEn" character varying(300);
            ALTER TABLE courses ADD COLUMN IF NOT EXISTS "IsHistorical" boolean NOT NULL DEFAULT FALSE;

            ALTER TABLE course_enrollments ADD COLUMN IF NOT EXISTS "LegacyEvaluationConfirmed" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE course_enrollments ADD COLUMN IF NOT EXISTS "LegacyImpactConfirmed" boolean NOT NULL DEFAULT FALSE;

            ALTER TABLE certificates ADD COLUMN IF NOT EXISTS "StatusChangedAtUtc" timestamp with time zone;

            ALTER TABLE user_accounts ADD COLUMN IF NOT EXISTS "InitialEmailStatus" integer NOT NULL DEFAULT 0;
            ALTER TABLE user_accounts ADD COLUMN IF NOT EXISTS "InitialEmailLastError" character varying(1000);

            ALTER TABLE audit_events ADD COLUMN IF NOT EXISTS "UserAgent" character varying(400);
            ALTER TABLE audit_events ADD COLUMN IF NOT EXISTS "Details" character varying(2000);
            ALTER TABLE audit_events ALTER COLUMN "IpAddress" TYPE character varying(64);
            CREATE INDEX IF NOT EXISTS "IX_audit_events_CreatedAtUtc" ON audit_events ("CreatedAtUtc");

            -- 2) الإشعارات
            CREATE TABLE IF NOT EXISTS notifications (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "Kind" character varying(80) NOT NULL,
                "TitleAr" character varying(300) NOT NULL,
                "TitleEn" character varying(300),
                "BodyAr" character varying(2000),
                "BodyEn" character varying(2000),
                "LinkUrl" character varying(500),
                "ReadAtUtc" timestamp with time zone,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "IsArchived" boolean NOT NULL DEFAULT FALSE,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_notifications" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_notifications_user_accounts_UserId" FOREIGN KEY ("UserId")
                    REFERENCES user_accounts ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_notifications_UserId_CreatedAtUtc" ON notifications ("UserId", "CreatedAtUtc");

            -- 3) مرفقات المحاضرات
            CREATE TABLE IF NOT EXISTS lecture_materials (
                "Id" uuid NOT NULL,
                "LectureId" uuid NOT NULL,
                "OriginalName" character varying(300) NOT NULL,
                "StorageKey" character varying(100) NOT NULL,
                "Sha256" character varying(64) NOT NULL,
                "UploadedByUserId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "IsArchived" boolean NOT NULL DEFAULT FALSE,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_lecture_materials" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_lecture_materials_lectures_LectureId" FOREIGN KEY ("LectureId")
                    REFERENCES lectures ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_lecture_materials_user_accounts_UploadedByUserId" FOREIGN KEY ("UploadedByUserId")
                    REFERENCES user_accounts ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_lecture_materials_LectureId" ON lecture_materials ("LectureId");

            -- 4) الملف الشخصي المشترك
            CREATE TABLE IF NOT EXISTS member_profiles (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "BioAr" character varying(4000),
                "BioEn" character varying(4000),
                "ExpertiseAr" character varying(1000),
                "ExpertiseEn" character varying(1000),
                "PhotoStorageKey" character varying(100),
                "ContactEmail" character varying(320),
                "Phone" character varying(40),
                "OfficeAr" character varying(200),
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "IsArchived" boolean NOT NULL DEFAULT FALSE,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_member_profiles" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_member_profiles_user_accounts_UserId" FOREIGN KEY ("UserId")
                    REFERENCES user_accounts ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_member_profiles_UserId" ON member_profiles ("UserId");

            -- 5) طلبات المدربين الخارجيين
            CREATE TABLE IF NOT EXISTS external_trainer_requests (
                "Id" uuid NOT NULL,
                "RequestedByUserId" uuid NOT NULL,
                "TrainerNameAr" character varying(300) NOT NULL,
                "TrainerNameEn" character varying(300),
                "Email" character varying(320) NOT NULL,
                "OrganizationAr" character varying(300),
                "NoteAr" character varying(2000),
                "Status" integer NOT NULL DEFAULT 0,
                "HandledByUserId" uuid,
                "HandledAtUtc" timestamp with time zone,
                "CreatedLoginName" character varying(64),
                "CourseId" uuid,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "IsArchived" boolean NOT NULL DEFAULT FALSE,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_external_trainer_requests" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_external_trainer_requests_user_accounts_RequestedByUserId" FOREIGN KEY ("RequestedByUserId")
                    REFERENCES user_accounts ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_external_trainer_requests_user_accounts_HandledByUserId" FOREIGN KEY ("HandledByUserId")
                    REFERENCES user_accounts ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_external_trainer_requests_Status" ON external_trainer_requests ("Status");

            -- 6) فهارس مساندة للاستعلامات التشغيلية
            CREATE INDEX IF NOT EXISTS "IX_certificates_EnrollmentId" ON certificates ("EnrollmentId");
            CREATE INDEX IF NOT EXISTS "IX_certificates_CourseId" ON certificates ("CourseId");
            CREATE INDEX IF NOT EXISTS "IX_course_enrollments_UserId" ON course_enrollments ("UserId");
            CREATE INDEX IF NOT EXISTS "IX_attendance_records_EnrollmentId" ON attendance_records ("EnrollmentId");
            """);

        migrationBuilder.Sql(BuildPermissionsSql());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM role_permissions WHERE "PermissionId" IN (
                SELECT "Id" FROM permissions WHERE "Key" IN (
                    'page.admin','page.quality','page.dean','page.instructor','page.member',
                    'members.directory.view','admin.statistics.view','admin.audit.view','admin.tracks.manage',
                    'admin.emails.send','courses.history.import','courses.external_trainer.request',
                    'certificates.issue','certificates.revoke','courses.materials.manage',
                    'courses.statistics.view','admin.documents.vault'));
            DELETE FROM permissions WHERE "Key" IN (
                'page.admin','page.quality','page.dean','page.instructor','page.member',
                'members.directory.view','admin.statistics.view','admin.audit.view','admin.tracks.manage',
                'admin.emails.send','courses.history.import','courses.external_trainer.request',
                'certificates.issue','certificates.revoke','courses.materials.manage',
                'courses.statistics.view','admin.documents.vault');

            DROP TABLE IF EXISTS external_trainer_requests;
            DROP TABLE IF EXISTS member_profiles;
            DROP TABLE IF EXISTS lecture_materials;
            DROP TABLE IF EXISTS notifications;

            DROP INDEX IF EXISTS "IX_attendance_records_EnrollmentId";
            DROP INDEX IF EXISTS "IX_course_enrollments_UserId";
            DROP INDEX IF EXISTS "IX_certificates_CourseId";
            DROP INDEX IF EXISTS "IX_certificates_EnrollmentId";
            DROP INDEX IF EXISTS "IX_audit_events_CreatedAtUtc";

            ALTER TABLE audit_events DROP COLUMN IF EXISTS "Details";
            ALTER TABLE audit_events DROP COLUMN IF EXISTS "UserAgent";
            ALTER TABLE user_accounts DROP COLUMN IF EXISTS "InitialEmailLastError";
            ALTER TABLE user_accounts DROP COLUMN IF EXISTS "InitialEmailStatus";
            ALTER TABLE certificates DROP COLUMN IF EXISTS "StatusChangedAtUtc";
            ALTER TABLE course_enrollments DROP COLUMN IF EXISTS "LegacyImpactConfirmed";
            ALTER TABLE course_enrollments DROP COLUMN IF EXISTS "LegacyEvaluationConfirmed";
            ALTER TABLE courses DROP COLUMN IF EXISTS "IsHistorical";
            ALTER TABLE courses DROP COLUMN IF EXISTS "ExternalTrainerOrganizationEn";
            ALTER TABLE courses DROP COLUMN IF EXISTS "ExternalTrainerOrganizationAr";
            ALTER TABLE lectures DROP COLUMN IF EXISTS "OnlineLink";
            ALTER TABLE lectures DROP COLUMN IF EXISTS "Mode";
            """);
    }

    private static string BuildPermissionsSql()
    {
        var seededAt = "TIMESTAMPTZ '2026-10-26T00:00:00+00:00'";
        var permissions = new (string Code, string Key, string NameAr, string NameEn, int[] Roles)[]
        {
            ("0201", "page.admin", "لوحة الأدمن", "Admin console", []),
            ("0202", "page.quality", "لوحة الجودة والاعتماد", "Quality console", [2]),
            ("0203", "page.dean", "لوحة العميد", "Dean console", [3]),
            ("0204", "page.instructor", "لوحة المحاضر", "Instructor console", [4, 6]),
            ("0205", "page.member", "لوحة عضو هيئة التدريس", "Member console", [2, 3, 4, 5]),
            ("0206", "members.directory.view", "دليل الأعضاء والملفات المشتركة", "Member directory", [2, 3, 4, 5]),
            ("0207", "admin.statistics.view", "الإحصائيات الشاملة", "University-wide statistics", [2]),
            ("0208", "admin.audit.view", "سجل التدقيق", "Audit log", []),
            ("0209", "admin.tracks.manage", "إدارة محاور التدريب", "Manage training tracks", [2]),
            ("0210", "admin.emails.send", "إرسال بيانات الدخول", "Send credential emails", []),
            ("0211", "courses.history.import", "إدخال الدورات التاريخية", "Import historical courses", [2]),
            ("0212", "courses.external_trainer.request", "طلب حساب محاضر خارجي", "Request external trainer", [2]),
            ("0213", "certificates.issue", "إصدار الشهادات", "Issue certificates", [2]),
            ("0214", "certificates.revoke", "إلغاء الشهادات", "Revoke certificates", [2]),
            ("0215", "courses.materials.manage", "إدارة مرفقات المحاضرات", "Manage lecture materials", [2, 4, 6]),
            ("0216", "courses.statistics.view", "إحصائيات الدورات", "Course statistics", [2, 3, 4]),
            ("0217", "admin.documents.vault", "المستودع المركزي للوثائق", "Central document vault", [2])
        };

        var builder = new System.Text.StringBuilder();
        var grant = 0;
        foreach (var permission in permissions)
        {
            builder.AppendLine($"""
                INSERT INTO permissions ("Id","Key","NameAr","NameEn","IsActive","IsArchived","CreatedAtUtc","UpdatedAtUtc")
                VALUES ('8e9838fb-c639-4adc-a40a-a926df43{permission.Code}','{permission.Key}',
                        '{permission.NameAr}','{permission.NameEn}',TRUE,FALSE,{seededAt},{seededAt})
                ON CONFLICT ("Key") DO NOTHING;
                """);
            foreach (var role in permission.Roles)
            {
                grant++;
                builder.AppendLine($"""
                    INSERT INTO role_permissions ("Id","RoleId","PermissionId","IsActive","IsArchived","CreatedAtUtc","UpdatedAtUtc")
                    SELECT 'c0df69bb-93b9-43ce-8812-32145869{grant:D4}',
                           '5c98724b-cead-42f7-b20d-638817eb3c{role:D2}',
                           p."Id",TRUE,FALSE,{seededAt},{seededAt}
                    FROM permissions p
                    WHERE p."Key" = '{permission.Key}'
                    ON CONFLICT DO NOTHING;
                    """);
            }
        }
        return builder.ToString();
    }
}
