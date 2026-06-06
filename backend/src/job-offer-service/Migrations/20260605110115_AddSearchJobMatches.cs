using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace job_offer_service.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchJobMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_job_matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_job_matches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_search_job_matches_JobId",
                table: "search_job_matches",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_search_job_matches_SearchId",
                table: "search_job_matches",
                column: "SearchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_job_matches");
        }
    }
}
