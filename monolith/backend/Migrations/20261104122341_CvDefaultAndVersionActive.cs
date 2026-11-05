using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class CvDefaultAndVersionActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Cvs",
                newName: "IsDefault");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CvVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: the newest version of every DEFAULT CV becomes active so picker
            // lists are not empty right after migration; everything else stays inactive
            // (history) and the user curates more versions from Documents whenever needed.
            migrationBuilder.Sql(@"
UPDATE ""CvVersions"" AS v
SET ""IsActive"" = true
WHERE v.""VersionNumber"" = (
    SELECT MAX(m.""VersionNumber"") FROM ""CvVersions"" AS m WHERE m.""CvId"" = v.""CvId"")
AND EXISTS (
    SELECT 1 FROM ""Cvs"" AS c WHERE c.""Id"" = v.""CvId"" AND c.""IsDefault"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CvVersions");

            migrationBuilder.RenameColumn(
                name: "IsDefault",
                table: "Cvs",
                newName: "IsActive");
        }
    }
}
