using LexiLoop.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiLoop.Data.Migrations;

[DbContext(typeof(LexiLoopDbContext))]
[Migration("20260909143000_AddLearningSessionVersion")]
public partial class AddLearningSessionVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Version",
            table: "LearningSessions",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Version", table: "LearningSessions");
    }
}
