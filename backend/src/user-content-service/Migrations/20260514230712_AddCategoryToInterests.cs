using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserContentService.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryToInterests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "interests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "interests");
        }
    }
}
