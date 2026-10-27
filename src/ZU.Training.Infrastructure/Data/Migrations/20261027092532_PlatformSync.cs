using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZU.Training.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlatformSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InitialEmailLastError",
                table: "user_accounts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InitialEmailStatus",
                table: "user_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "lectures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OnlineLink",
                table: "lectures",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTrainerOrganizationAr",
                table: "courses",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTrainerOrganizationEn",
                table: "courses",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHistorical",
                table: "courses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LegacyEvaluationConfirmed",
                table: "course_enrollments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LegacyImpactConfirmed",
                table: "course_enrollments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusChangedAtUtc",
                table: "certificates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "audit_events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "audit_events",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "audit_events",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "external_trainer_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerNameAr = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TrainerNameEn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    OrganizationAr = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NoteAr = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HandledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    HandledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedLoginName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_trainer_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_external_trainer_requests_user_accounts_HandledByUserId",
                        column: x => x.HandledByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_trainer_requests_user_accounts_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lecture_materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LectureId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecture_materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lecture_materials_lectures_LectureId",
                        column: x => x.LectureId,
                        principalTable: "lectures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lecture_materials_user_accounts_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BioAr = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    BioEn = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExpertiseAr = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExpertiseEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PhotoStorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    OfficeAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_member_profiles_user_accounts_UserId",
                        column: x => x.UserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TitleAr = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TitleEn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BodyAr = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BodyEn = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LinkUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_user_accounts_UserId",
                        column: x => x.UserId,
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_CreatedAtUtc",
                table: "audit_events",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_external_trainer_requests_HandledByUserId",
                table: "external_trainer_requests",
                column: "HandledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_external_trainer_requests_RequestedByUserId",
                table: "external_trainer_requests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_external_trainer_requests_Status",
                table: "external_trainer_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_lecture_materials_LectureId",
                table: "lecture_materials",
                column: "LectureId");

            migrationBuilder.CreateIndex(
                name: "IX_lecture_materials_UploadedByUserId",
                table: "lecture_materials",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_member_profiles_UserId",
                table: "member_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_CreatedAtUtc",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_trainer_requests");

            migrationBuilder.DropTable(
                name: "lecture_materials");

            migrationBuilder.DropTable(
                name: "member_profiles");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_CreatedAtUtc",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "InitialEmailLastError",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "InitialEmailStatus",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "lectures");

            migrationBuilder.DropColumn(
                name: "OnlineLink",
                table: "lectures");

            migrationBuilder.DropColumn(
                name: "ExternalTrainerOrganizationAr",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "ExternalTrainerOrganizationEn",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "IsHistorical",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "LegacyEvaluationConfirmed",
                table: "course_enrollments");

            migrationBuilder.DropColumn(
                name: "LegacyImpactConfirmed",
                table: "course_enrollments");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "audit_events");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "audit_events",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
