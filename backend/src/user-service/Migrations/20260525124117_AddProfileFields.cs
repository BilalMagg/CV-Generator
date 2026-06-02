using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorizedCountry",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesiredJobTitle",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DesiredSalaryMax",
                table: "users",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DesiredSalaryMin",
                table: "users",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentTypes",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Headline",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoticePeriod",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfessionalTitles",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemotePreference",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresVisaSponsorship",
                table: "users",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WillingToRelocate",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorizedCountry",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "users");

            migrationBuilder.DropColumn(
                name: "City",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DesiredJobTitle",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DesiredSalaryMax",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DesiredSalaryMin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmploymentTypes",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Headline",
                table: "users");

            migrationBuilder.DropColumn(
                name: "NoticePeriod",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ProfessionalTitles",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RemotePreference",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RequiresVisaSponsorship",
                table: "users");

            migrationBuilder.DropColumn(
                name: "WillingToRelocate",
                table: "users");
        }
    }
}
