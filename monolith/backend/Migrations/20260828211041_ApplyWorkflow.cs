using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class ApplyWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "EmailSchedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentRefsJson",
                table: "EmailSchedules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CvVersionId",
                table: "EmailSchedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateSourceId",
                table: "EmailSchedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "companies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "schedule_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubjectTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyTemplate = table.Column<string>(type: "text", nullable: false),
                    VariableDefaultsJson = table.Column<string>(type: "text", nullable: true),
                    CvVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachmentRefsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_templates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schedule_templates");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "EmailSchedules");

            migrationBuilder.DropColumn(
                name: "AttachmentRefsJson",
                table: "EmailSchedules");

            migrationBuilder.DropColumn(
                name: "CvVersionId",
                table: "EmailSchedules");

            migrationBuilder.DropColumn(
                name: "TemplateSourceId",
                table: "EmailSchedules");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "companies");
        }
    }
}
