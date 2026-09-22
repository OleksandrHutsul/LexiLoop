using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiLoop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFsrsLearningState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLearningEnabled",
                table: "VocabularyItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "ForeignToTranslationDifficulty",
                table: "LearningProgress",
                type: "double precision",
                nullable: false,
                defaultValue: 5.0);

            migrationBuilder.AddColumn<int>(
                name: "ForeignToTranslationIntervalDays",
                table: "LearningProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ForeignToTranslationLapseCount",
                table: "LearningProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ForeignToTranslationLastReviewedAt",
                table: "LearningProgress",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ForeignToTranslationReviewCount",
                table: "LearningProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ForeignToTranslationStability",
                table: "LearningProgress",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ForeignToTranslationState",
                table: "LearningProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "TranslationToForeignDifficulty",
                table: "LearningProgress",
                type: "double precision",
                nullable: false,
                defaultValue: 5.0);

            migrationBuilder.AddColumn<int>(
                name: "TranslationToForeignIntervalDays",
                table: "LearningProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TranslationToForeignLapseCount",
                table: "LearningProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TranslationToForeignLastReviewedAt",
                table: "LearningProgress",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TranslationToForeignReviewCount",
                table: "LearningProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "TranslationToForeignStability",
                table: "LearningProgress",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TranslationToForeignState",
                table: "LearningProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Preserve the pre-migration behavior for existing users. Their vocabulary
            // remains in the learning pool; the new direction-specific FSRS fields start
            // safely as unseen and are populated on the next review.
            migrationBuilder.Sql("UPDATE \"VocabularyItems\" SET \"IsLearningEnabled\" = TRUE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLearningEnabled",
                table: "VocabularyItems");

            migrationBuilder.DropColumn(
                name: "ForeignToTranslationDifficulty",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "ForeignToTranslationIntervalDays",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "ForeignToTranslationLapseCount",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "ForeignToTranslationLastReviewedAt",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "ForeignToTranslationReviewCount",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "ForeignToTranslationStability",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "ForeignToTranslationState",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "TranslationToForeignDifficulty",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "TranslationToForeignIntervalDays",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "TranslationToForeignLapseCount",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "TranslationToForeignLastReviewedAt",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "TranslationToForeignReviewCount",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "TranslationToForeignStability",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "TranslationToForeignState",
                table: "LearningProgress");
        }
    }
}
