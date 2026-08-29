using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryTrees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    KeywordsJson = table.Column<string>(type: "text", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryNodes_CategoryNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "CategoryNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntityCategoryTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityCategoryTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityCategoryTags_CategoryNodes_CategoryNodeId",
                        column: x => x.CategoryNodeId,
                        principalTable: "CategoryNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryNodes_ParentId",
                table: "CategoryNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryNodes_Path",
                table: "CategoryNodes",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryNodes_Scope_ParentId",
                table: "CategoryNodes",
                columns: new[] { "Scope", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_EntityCategoryTags_CategoryNodeId",
                table: "EntityCategoryTags",
                column: "CategoryNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityCategoryTags_UserId_SourceType_SourceId_CategoryNodeId",
                table: "EntityCategoryTags",
                columns: new[] { "UserId", "SourceType", "SourceId", "CategoryNodeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntityCategoryTags");

            migrationBuilder.DropTable(
                name: "CategoryNodes");
        }
    }
}
