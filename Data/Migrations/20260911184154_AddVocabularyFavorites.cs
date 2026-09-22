using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiLoop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularyFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "VocabularyItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "VocabularyItems");
        }
    }
}
