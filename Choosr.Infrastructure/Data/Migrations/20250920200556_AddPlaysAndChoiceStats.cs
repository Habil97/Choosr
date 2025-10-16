using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choosr.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaysAndChoiceStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Plays",
                table: "Quizzes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "QuizChoiceStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuizId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Picks = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizChoiceStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizChoiceStats_QuizChoices_ChoiceId",
                        column: x => x.ChoiceId,
                        principalTable: "QuizChoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizChoiceStats_Quizzes_QuizId",
                        column: x => x.QuizId,
                        principalTable: "Quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizChoiceStats_ChoiceId",
                table: "QuizChoiceStats",
                column: "ChoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizChoiceStats_QuizId_ChoiceId",
                table: "QuizChoiceStats",
                columns: new[] { "QuizId", "ChoiceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizChoiceStats");

            migrationBuilder.DropColumn(
                name: "Plays",
                table: "Quizzes");
        }
    }
}
