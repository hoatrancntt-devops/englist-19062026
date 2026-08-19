using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishForIT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryChapterUnlockAnchor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_hash",
                table: "story_chapters",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "unlock_after_lesson_code",
                table: "story_chapters",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_story_chapters_unlock_after_lesson_code",
                table: "story_chapters",
                column: "unlock_after_lesson_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_story_chapters_unlock_after_lesson_code",
                table: "story_chapters");

            migrationBuilder.DropColumn(
                name: "source_hash",
                table: "story_chapters");

            migrationBuilder.DropColumn(
                name: "unlock_after_lesson_code",
                table: "story_chapters");
        }
    }
}
