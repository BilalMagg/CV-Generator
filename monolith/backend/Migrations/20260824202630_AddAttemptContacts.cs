using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class AddAttemptContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "application_attempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_attempts_ContactId",
                table: "application_attempts",
                column: "ContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_application_attempts_Contacts_ContactId",
                table: "application_attempts",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_application_attempts_Contacts_ContactId",
                table: "application_attempts");

            migrationBuilder.DropIndex(
                name: "IX_application_attempts_ContactId",
                table: "application_attempts");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "application_attempts");
        }
    }
}
