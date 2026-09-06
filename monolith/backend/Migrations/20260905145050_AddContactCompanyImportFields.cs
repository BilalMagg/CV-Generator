using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class AddContactCompanyImportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fax",
                table: "Contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                table: "Contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FoundedYear",
                table: "companies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "companies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sector",
                table: "companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Fax",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Mobile",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "FoundedYear",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "Sector",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "companies");
        }
    }
}
