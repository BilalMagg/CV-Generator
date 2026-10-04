using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PositionTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CompanyDescription = table.Column<string>(type: "text", nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ContactNotes = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    CvVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CvTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_drafts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_drafts");
        }
    }
}
