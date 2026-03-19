using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPgVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "NameEmbedding",
                table: "skills",
                type: "vector(384)",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "DescriptionEmbedding",
                table: "projects",
                type: "vector(384)",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "DescriptionEmbedding",
                table: "experiences",
                type: "vector(384)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEmbedding",
                table: "skills");

            migrationBuilder.DropColumn(
                name: "DescriptionEmbedding",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "DescriptionEmbedding",
                table: "experiences");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
