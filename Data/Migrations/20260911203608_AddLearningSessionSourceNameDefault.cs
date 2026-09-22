using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiLoop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningSessionSourceNameDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourceName",
                table: "LearningSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "All words",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourceName",
                table: "LearningSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldDefaultValue: "All words");
        }
    }
}
