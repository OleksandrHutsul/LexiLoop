using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiLoop.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDefaultVocabularyCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH "CreatedDefaults" AS
                (
                    INSERT INTO "Collections" ("UserId", "Name", "CreatedAt", "UpdatedAt")
                    SELECT
                        vocabulary."UserId",
                        'My Vocabulary',
                        MIN(vocabulary."CreatedAt"),
                        CURRENT_TIMESTAMP
                    FROM "VocabularyItems" AS vocabulary
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM "Collections" AS existing_collection
                        WHERE existing_collection."UserId" = vocabulary."UserId"
                    )
                    GROUP BY vocabulary."UserId"
                    RETURNING "Id", "UserId"
                )
                INSERT INTO "VocabularyItemCollections" ("VocabularyItemId", "CollectionId")
                SELECT vocabulary."Id", default_collection."Id"
                FROM "VocabularyItems" AS vocabulary
                INNER JOIN "CreatedDefaults" AS default_collection
                    ON default_collection."UserId" = vocabulary."UserId"
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally leave backfilled lists and memberships in place. Once users can
            // edit them, migration-created rows cannot be distinguished safely from user data.
        }
    }
}
