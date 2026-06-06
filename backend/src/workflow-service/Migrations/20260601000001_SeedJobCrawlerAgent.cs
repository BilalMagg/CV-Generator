using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowService.Migrations
{
    /// <inheritdoc />
    public partial class SeedJobCrawlerAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "agents",
                columns: new[] { "Id", "AgentId", "Name", "Role", "BackgroundGradient", "SortOrder", "IsActive" },
                values: new object[]
                {
                    Guid.NewGuid(),
                    "job-crawler",
                    "Job Crawler",
                    "Live Job Scraper",
                    "linear-gradient(145deg, #1a0a0a 0%, #3d1515 50%, #8b2020 100%)",
                    6,
                    true
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "AgentId",
                keyValue: "job-crawler");
        }
    }
}
