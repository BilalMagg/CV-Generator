using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class AddHackathonAcademyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamSize",
                table: "hackathons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Technologies",
                table: "hackathons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "academic_activities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Result",
                table: "academic_activities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "academic_activities",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamSize",
                table: "hackathons");

            migrationBuilder.DropColumn(
                name: "Technologies",
                table: "hackathons");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "academic_activities");

            migrationBuilder.DropColumn(
                name: "Result",
                table: "academic_activities");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "academic_activities");
        }
    }
}
