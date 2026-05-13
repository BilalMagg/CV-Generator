using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace job_offer_service.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "job_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnterpriseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EnterpriseDescription = table.Column<string>(type: "text", nullable: true),
                    JobRole = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RawDescription = table.Column<string>(type: "text", nullable: false),
                    DescriptionVector = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    RequiredExperienceYears = table.Column<int>(type: "integer", nullable: true),
                    SeniorityLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LocationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EducationRequirements = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_offers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_benefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_benefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_benefits_job_offers_JobOfferId",
                        column: x => x.JobOfferId,
                        principalTable: "job_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_responsibilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_responsibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_responsibilities_job_offers_JobOfferId",
                        column: x => x.JobOfferId,
                        principalTable: "job_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_skills_job_offers_JobOfferId",
                        column: x => x.JobOfferId,
                        principalTable: "job_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_benefits_JobOfferId",
                table: "job_benefits",
                column: "JobOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_job_responsibilities_JobOfferId",
                table: "job_responsibilities",
                column: "JobOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_job_skills_JobOfferId",
                table: "job_skills",
                column: "JobOfferId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_benefits");

            migrationBuilder.DropTable(
                name: "job_responsibilities");

            migrationBuilder.DropTable(
                name: "job_skills");

            migrationBuilder.DropTable(
                name: "job_offers");
        }
    }
}
