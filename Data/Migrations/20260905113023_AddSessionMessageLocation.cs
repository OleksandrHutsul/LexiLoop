using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiLoop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionMessageLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ChatId",
                table: "LearningSessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "MessageId",
                table: "LearningSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "LearningSessions");
        }
    }
}
