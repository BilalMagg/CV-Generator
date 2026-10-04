using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentDocumentChunks");

            migrationBuilder.DropColumn(
                name: "DescriptionVector",
                table: "job_offers");

            // Extension drop sequenced last so no table/column still uses the
            // `vector` type when it runs (pgvector has no CASCADE here).
            migrationBuilder.Sql("DROP EXTENSION IF EXISTS \"vector\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"vector\";");

            migrationBuilder.AddColumn<Vector>(
                name: "DescriptionVector",
                table: "job_offers",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentDocumentChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "Content" }),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDocumentChunks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentDocumentChunks_SearchVector",
                table: "AgentDocumentChunks",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }
    }
}
