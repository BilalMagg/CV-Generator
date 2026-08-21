using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplicationService.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Data migration 1: bookmarked apps (IsSaved=true, still PENDING) become SAVED
            migrationBuilder.Sql("""
                UPDATE "applications"
                SET "Status" = 'SAVED'
                WHERE "IsSaved" = true AND "Status" = 'PENDING';
                """);

            // ── Data migration 2: remap old pipeline values to the new ones
            migrationBuilder.Sql("""
                UPDATE "applications" SET "Status" = 'APPLIED'   WHERE "Status" = 'PENDING';
                UPDATE "applications" SET "Status" = 'SCREENING' WHERE "Status" = 'REVIEWED';
                UPDATE "applications" SET "Status" = 'WITHDRAWN' WHERE "Status" = 'CANCELLED';

                UPDATE "application_status_history" SET "OldStatus" = 'APPLIED'   WHERE "OldStatus" = 'PENDING';
                UPDATE "application_status_history" SET "NewStatus" = 'APPLIED'   WHERE "NewStatus" = 'PENDING';
                UPDATE "application_status_history" SET "OldStatus" = 'SCREENING' WHERE "OldStatus" = 'REVIEWED';
                UPDATE "application_status_history" SET "NewStatus" = 'SCREENING' WHERE "NewStatus" = 'REVIEWED';
                UPDATE "application_status_history" SET "OldStatus" = 'WITHDRAWN' WHERE "OldStatus" = 'CANCELLED';
                UPDATE "application_status_history" SET "NewStatus" = 'WITHDRAWN' WHERE "NewStatus" = 'CANCELLED';
                """);

            migrationBuilder.DropColumn(
                name: "IsSaved",
                table: "applications");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AppliedAt",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "applications",
                type: "character varying(280)",
                maxLength: 280,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "applications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // ── Data migration 3: backfill fingerprints and origin defaults
            migrationBuilder.Sql("""
                UPDATE "applications"
                SET "Fingerprint" =
                    LOWER(REGEXP_REPLACE(TRIM("CompanyName"), '\s+', ' ', 'g')) || '|' ||
                    LOWER(REGEXP_REPLACE(TRIM("PositionTitle"), '\s+', ' ', 'g'))
                WHERE "Fingerprint" = '';

                UPDATE "applications"
                SET "Origin" = 'MANUAL'
                WHERE "Origin" = '';
                """);

            // ── Data migration 4: clear duplicate JobOfferId links so the unique index can be created
            migrationBuilder.Sql("""
                UPDATE "applications" a
                SET "JobOfferId" = NULL
                WHERE a."JobOfferId" IS NOT NULL
                  AND EXISTS (
                      SELECT 1 FROM "applications" b
                      WHERE b."CandidateId" = a."CandidateId"
                        AND b."JobOfferId" = a."JobOfferId"
                        AND b."Id" < a."Id");
                """);

            migrationBuilder.CreateTable(
                name: "application_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InitiatedBy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecipientContact = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ChannelMetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CvVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_attempts_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_applications_CandidateId_JobOfferId",
                table: "applications",
                columns: new[] { "CandidateId", "JobOfferId" },
                unique: true,
                filter: "\"JobOfferId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_applications_Fingerprint",
                table: "applications",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_application_attempts_ApplicationId_AttemptNumber",
                table: "application_attempts",
                columns: new[] { "ApplicationId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_attempts_Status",
                table: "application_attempts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_attempts");

            migrationBuilder.DropIndex(
                name: "IX_applications_CandidateId_JobOfferId",
                table: "applications");

            migrationBuilder.DropIndex(
                name: "IX_applications_Fingerprint",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "applications");

            // Best-effort reverse remap of the status pipeline
            migrationBuilder.Sql("""
                UPDATE "applications" SET "Status" = 'CANCELLED' WHERE "Status" = 'WITHDRAWN';
                UPDATE "applications" SET "Status" = 'PENDING'   WHERE "Status" = 'APPLIED';
                UPDATE "applications" SET "Status" = 'PENDING'   WHERE "Status" = 'SAVED';
                UPDATE "applications" SET "Status" = 'REVIEWED'  WHERE "Status" = 'SCREENING';

                UPDATE "application_status_history" SET "OldStatus" = 'PENDING'   WHERE "OldStatus" = 'APPLIED';
                UPDATE "application_status_history" SET "NewStatus" = 'PENDING'   WHERE "NewStatus" = 'APPLIED';
                UPDATE "application_status_history" SET "OldStatus" = 'PENDING'   WHERE "OldStatus" = 'SAVED';
                UPDATE "application_status_history" SET "NewStatus" = 'PENDING'   WHERE "NewStatus" = 'SAVED';
                UPDATE "application_status_history" SET "OldStatus" = 'REVIEWED'  WHERE "OldStatus" = 'SCREENING';
                UPDATE "application_status_history" SET "NewStatus" = 'REVIEWED'  WHERE "NewStatus" = 'SCREENING';
                UPDATE "application_status_history" SET "OldStatus" = 'CANCELLED' WHERE "OldStatus" = 'WITHDRAWN';
                UPDATE "application_status_history" SET "NewStatus" = 'CANCELLED' WHERE "NewStatus" = 'WITHDRAWN';
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "AppliedAt",
                table: "applications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSaved",
                table: "applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
