using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace notification_service.Migrations
{
    /// <inheritdoc />
    public partial class AddContactIsFavoriteAndContactId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "EmailMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "Contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_ContactId",
                table: "EmailMessages",
                column: "ContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailMessages_Contacts_ContactId",
                table: "EmailMessages",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailMessages_Contacts_ContactId",
                table: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_ContactId",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "Contacts");
        }
    }
}
