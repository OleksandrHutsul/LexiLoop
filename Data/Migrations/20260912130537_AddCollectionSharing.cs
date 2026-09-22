using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiLoop.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportedFromShareToken",
                table: "Collections",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "Collections",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_ShareToken",
                table: "Collections",
                column: "ShareToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_UserId_ImportedFromShareToken",
                table: "Collections",
                columns: new[] { "UserId", "ImportedFromShareToken" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Collections_ShareToken",
                table: "Collections");

            migrationBuilder.DropIndex(
                name: "IX_Collections_UserId_ImportedFromShareToken",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ImportedFromShareToken",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "Collections");
        }
    }
}
