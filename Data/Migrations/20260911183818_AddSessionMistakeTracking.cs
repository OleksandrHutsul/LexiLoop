using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiLoop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionMistakeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnteredAnswer",
                table: "LearningSessionCards",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedAnswer",
                table: "LearningSessionCards",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TypedAnswerCorrect",
                table: "LearningSessionCards",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnteredAnswer",
                table: "LearningSessionCards");

            migrationBuilder.DropColumn(
                name: "ExpectedAnswer",
                table: "LearningSessionCards");

            migrationBuilder.DropColumn(
                name: "TypedAnswerCorrect",
                table: "LearningSessionCards");
        }
    }
}
