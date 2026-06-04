using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowService.Migrations
{
    /// <inheritdoc />
    public partial class AddCvGenerationRunFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailSubject",
                table: "cv_generation_runs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "cv_generation_runs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateId",
                table: "cv_generation_runs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tone",
                table: "cv_generation_runs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailSubject",
                table: "cv_generation_runs");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "cv_generation_runs");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "cv_generation_runs");

            migrationBuilder.DropColumn(
                name: "Tone",
                table: "cv_generation_runs");
        }
    }
}
