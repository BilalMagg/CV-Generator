using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailReauth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastTokenError",
                table: "GmailConnections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTokenErrorAt",
                table: "GmailConnections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReauth",
                table: "GmailConnections",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTokenError",
                table: "GmailConnections");

            migrationBuilder.DropColumn(
                name: "LastTokenErrorAt",
                table: "GmailConnections");

            migrationBuilder.DropColumn(
                name: "NeedsReauth",
                table: "GmailConnections");
        }
    }
}
