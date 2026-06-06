using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace job_offer_service.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchCacheUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "search_caches",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "search_caches");
        }
    }
}
