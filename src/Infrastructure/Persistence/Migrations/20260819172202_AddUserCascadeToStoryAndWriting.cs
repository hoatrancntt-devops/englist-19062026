using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishForIT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCascadeToStoryAndWriting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hai bảng này ra đời không có khoá ngoại về users, nên mọi tài khoản đã xoá
            // trước đây đều để lại tiến độ đọc và bài viết mồ côi. Khoá ngoại không thêm
            // được khi chúng còn đó, và bản thân chúng cũng là rác cần dọn.
            migrationBuilder.Sql(
                "DELETE FROM story_progresses sp WHERE NOT EXISTS " +
                "(SELECT 1 FROM users u WHERE u.id = sp.user_id);");

            migrationBuilder.Sql(
                "DELETE FROM writing_attempts wa WHERE NOT EXISTS " +
                "(SELECT 1 FROM users u WHERE u.id = wa.user_id);");

            migrationBuilder.AddForeignKey(
                name: "fk_story_progresses_users_user_id",
                table: "story_progresses",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_writing_attempts_users_user_id",
                table: "writing_attempts",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_story_progresses_users_user_id",
                table: "story_progresses");

            migrationBuilder.DropForeignKey(
                name: "fk_writing_attempts_users_user_id",
                table: "writing_attempts");
        }
    }
}
