using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiLoop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningSessionSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CollectionId",
                table: "LearningSessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                table: "LearningSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"LearningSessions\" SET \"SourceName\" = 'All words';");

            migrationBuilder.AlterColumn<string>(
                name: "SourceName",
                table: "LearningSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Result",
                table: "LearningSessionCards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_CollectionId",
                table: "LearningSessions",
                column: "CollectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningSessions_Collections_CollectionId",
                table: "LearningSessions",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningSessions_Collections_CollectionId",
                table: "LearningSessions");

            migrationBuilder.DropIndex(
                name: "IX_LearningSessions_CollectionId",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "SourceName",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "Result",
                table: "LearningSessionCards");
        }
    }
}
