using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class SeedAgentData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "agents",
                columns: new[] { "Id", "AgentId", "BackgroundGradient", "IsActive", "Name", "Role", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "job-extractor", "linear-gradient(135deg, #667eea 0%, #764ba2 100%)", true, "Job Extractor", "Extracts structured data from job descriptions and URLs", 1 },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "search-agent", "linear-gradient(135deg, #f093fb 0%, #f5576c 100%)", true, "Search Agent", "Finds similar CV content and matches your profile to job requirements", 2 },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "template-agent", "linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)", true, "Template Agent", "Generates cover letters and formats CVs using templates", 3 },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "cv-optimizer", "linear-gradient(135deg, #43e97b 0%, #38f9d7 100%)", true, "CV Optimizer", "Optimizes your CV content for specific job applications using AI", 4 },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "contact-agent", "linear-gradient(135deg, #fa709a 0%, #fee140 100%)", true, "Contact Agent", "Drafts professional outreach emails to recruiters and hiring managers", 5 },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "job-crawler", "linear-gradient(135deg, #a18cd1 0%, #fbc2eb 100%)", true, "Job Crawler", "Searches and discovers new job opportunities matching your profile", 6 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_agents_AgentId",
                table: "agents",
                column: "AgentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agents_AgentId",
                table: "agents");

            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "agents",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"));
        }
    }
}
