using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserContentService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCategoryFromInterests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Achievements",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "interests");

            migrationBuilder.DropColumn(
                name: "ReferenceUrl",
                table: "experiences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Achievements",
                table: "projects",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "interests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceUrl",
                table: "experiences",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }
    }
}
