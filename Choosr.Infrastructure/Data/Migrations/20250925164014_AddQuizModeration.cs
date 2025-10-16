using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choosr.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Moderation",
                table: "Quizzes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ModerationNotes",
                table: "Quizzes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_Moderation",
                table: "Quizzes",
                column: "Moderation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quizzes_Moderation",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "Moderation",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "ModerationNotes",
                table: "Quizzes");
        }
    }
}
