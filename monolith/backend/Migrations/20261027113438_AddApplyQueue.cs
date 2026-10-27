using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class AddApplyQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplyQueueItemId",
                table: "bulk_apply_rows",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "apply_queue_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PositionTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: true),
                    CvVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BulkRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apply_queue_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_apply_queue_items_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bulk_apply_rows_ApplyQueueItemId",
                table: "bulk_apply_rows",
                column: "ApplyQueueItemId");

            migrationBuilder.CreateIndex(
                name: "IX_apply_queue_items_ApplicationId",
                table: "apply_queue_items",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_apply_queue_items_Status",
                table: "apply_queue_items",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_apply_queue_items_UserId",
                table: "apply_queue_items",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apply_queue_items");

            migrationBuilder.DropIndex(
                name: "IX_bulk_apply_rows_ApplyQueueItemId",
                table: "bulk_apply_rows");

            migrationBuilder.DropColumn(
                name: "ApplyQueueItemId",
                table: "bulk_apply_rows");
        }
    }
}
