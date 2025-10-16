using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choosr.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadeOnStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuizChoiceStats_Quizzes_QuizId",
                table: "QuizChoiceStats");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizChoiceStats_Quizzes_QuizId",
                table: "QuizChoiceStats",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuizChoiceStats_Quizzes_QuizId",
                table: "QuizChoiceStats");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizChoiceStats_Quizzes_QuizId",
                table: "QuizChoiceStats",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
