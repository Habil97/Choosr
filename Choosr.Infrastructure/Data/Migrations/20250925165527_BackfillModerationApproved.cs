using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Choosr.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillModerationApproved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill: set Moderation=Approved (1) for any quizzes missing expected value
            migrationBuilder.Sql(@"UPDATE [Quizzes] SET [Moderation] = 1 WHERE [Moderation] IS NULL OR [Moderation] = 0");
            // Ensure default constraint is Approved (1) for new rows; use ADD DEFAULT depending on provider
            migrationBuilder.AlterColumn<int>(
                name: "Moderation",
                table: "Quizzes",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert default to no explicit default (provider will remove constraint)
            migrationBuilder.AlterColumn<int>(
                name: "Moderation",
                table: "Quizzes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: false,
                oldDefaultValue: 1);
        }
    }
}
