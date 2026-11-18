using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CV_Generator.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillsUniqueNameNormalized : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Deduplicates existing skills (keeps the earliest row per (UserId, normalized name))
        /// BEFORE creating a case-insensitive unique index, so the constraint can be created on
        /// the real data. Keep-row choice: earliest Id (oldest-created skill) wins; its sibling
        /// duplicates (and their category tags) are removed.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Skill entity maps to lowercase "skills" ([Table("skills")]) —
            // InitialCreate creates it unquoted, so quoted "Skills" would fail
            // with 'relation "Skills" does not exist' on every database.
            migrationBuilder.Sql("""
                DELETE FROM skills dup
                USING (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "UserId", lower(trim("Name")) ORDER BY "Id"
                           ) AS rn
                    FROM skills
                ) ranked
                WHERE ranked."Id" = dup."Id" AND ranked.rn > 1;
                """);

            migrationBuilder.Sql("""
                DELETE FROM "EntityCategoryTags"
                WHERE "SourceType" = 'skills'
                  AND "SourceId" NOT IN (SELECT "Id" FROM skills);
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Skills_UserId_NameNormalized"
                ON skills ("UserId", lower(trim("Name")));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX "IX_Skills_UserId_NameNormalized";""");
        }
    }
}