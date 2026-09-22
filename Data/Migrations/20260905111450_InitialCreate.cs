using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LexiLoop.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InputMode = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentIndex = table.Column<int>(type: "integer", nullable: false),
                    AnswerRevealed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    NewItemsPerDay = table.Column<int>(type: "integer", nullable: false),
                    DefaultLearningMode = table.Column<int>(type: "integer", nullable: false),
                    ShowPronunciation = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNotificationTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    LastNotificationDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ForeignText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Translation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Pronunciation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningProgress",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VocabularyItemId = table.Column<long>(type: "bigint", nullable: false),
                    ForeignToTranslationLevel = table.Column<int>(type: "integer", nullable: false),
                    TranslationToForeignLevel = table.Column<int>(type: "integer", nullable: false),
                    ForeignToTranslationNextReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TranslationToForeignNextReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    IncorrectCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningProgress_VocabularyItems_VocabularyItemId",
                        column: x => x.VocabularyItemId,
                        principalTable: "VocabularyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningSessionCards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VocabularyItemId = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessionCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningSessionCards_LearningSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "LearningSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningSessionCards_VocabularyItems_VocabularyItemId",
                        column: x => x.VocabularyItemId,
                        principalTable: "VocabularyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VocabularyItemId = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewHistory_VocabularyItems_VocabularyItemId",
                        column: x => x.VocabularyItemId,
                        principalTable: "VocabularyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyItemCollections",
                columns: table => new
                {
                    VocabularyItemId = table.Column<long>(type: "bigint", nullable: false),
                    CollectionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyItemCollections", x => new { x.VocabularyItemId, x.CollectionId });
                    table.ForeignKey(
                        name: "FK_VocabularyItemCollections_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VocabularyItemCollections_VocabularyItems_VocabularyItemId",
                        column: x => x.VocabularyItemId,
                        principalTable: "VocabularyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Collections_UserId_Name",
                table: "Collections",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgress_ForeignToTranslationNextReviewAt",
                table: "LearningProgress",
                column: "ForeignToTranslationNextReviewAt");

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgress_TranslationToForeignNextReviewAt",
                table: "LearningProgress",
                column: "TranslationToForeignNextReviewAt");

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgress_VocabularyItemId",
                table: "LearningProgress",
                column: "VocabularyItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionCards_SessionId_Position",
                table: "LearningSessionCards",
                columns: new[] { "SessionId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessionCards_VocabularyItemId",
                table: "LearningSessionCards",
                column: "VocabularyItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_UserId_Status",
                table: "LearningSessions",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistory_ReviewedAt",
                table: "ReviewHistory",
                column: "ReviewedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistory_VocabularyItemId",
                table: "ReviewHistory",
                column: "VocabularyItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TelegramUserId",
                table: "Users",
                column: "TelegramUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyItemCollections_CollectionId",
                table: "VocabularyItemCollections",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyItems_UserId",
                table: "VocabularyItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyItems_UserId_ForeignText_Translation",
                table: "VocabularyItems",
                columns: new[] { "UserId", "ForeignText", "Translation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningProgress");

            migrationBuilder.DropTable(
                name: "LearningSessionCards");

            migrationBuilder.DropTable(
                name: "ReviewHistory");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "VocabularyItemCollections");

            migrationBuilder.DropTable(
                name: "LearningSessions");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "VocabularyItems");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
