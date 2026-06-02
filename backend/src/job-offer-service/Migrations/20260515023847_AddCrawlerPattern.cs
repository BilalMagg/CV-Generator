using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace job_offer_service.Migrations
{
    /// <inheritdoc />
    public partial class AddCrawlerPattern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "job_offers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "JobHash",
                table: "job_offers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "job_offers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "search_caches",
                columns: table => new
                {
                    SearchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Keyword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CrawledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpectedCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_caches", x => x.SearchId);
                });

            migrationBuilder.CreateTable(
                name: "user_quotas",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastCrawlDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_quotas", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_offers_JobHash",
                table: "job_offers",
                column: "JobHash",
                unique: true,
                filter: "\"JobHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_search_caches_Keyword_CrawledDate",
                table: "search_caches",
                columns: new[] { "Keyword", "CrawledDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_caches");

            migrationBuilder.DropTable(
                name: "user_quotas");

            migrationBuilder.DropIndex(
                name: "IX_job_offers_JobHash",
                table: "job_offers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "job_offers");

            migrationBuilder.DropColumn(
                name: "JobHash",
                table: "job_offers");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "job_offers");
        }
    }
}
