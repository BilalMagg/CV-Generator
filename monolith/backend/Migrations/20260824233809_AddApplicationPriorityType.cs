using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationPriorityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InternshipType",
                table: "applications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternshipType",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "applications");
        }
    }
}
