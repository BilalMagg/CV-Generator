using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace notification_service.Migrations
{
    /// <inheritdoc />
    public partial class AddMailboxEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cron",
                table: "EmailSchedules");

            migrationBuilder.DropColumn(
                name: "RecipientType",
                table: "EmailSchedules");

            migrationBuilder.RenameColumn(
                name: "RecipientValue",
                table: "EmailSchedules",
                newName: "CronExpression");

            migrationBuilder.AddColumn<string>(
                name: "RecipientIds",
                table: "EmailSchedules",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "EmailSchedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipientIds",
                table: "EmailSchedules");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "EmailSchedules");

            migrationBuilder.RenameColumn(
                name: "CronExpression",
                table: "EmailSchedules",
                newName: "RecipientValue");

            migrationBuilder.AddColumn<string>(
                name: "Cron",
                table: "EmailSchedules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientType",
                table: "EmailSchedules",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
